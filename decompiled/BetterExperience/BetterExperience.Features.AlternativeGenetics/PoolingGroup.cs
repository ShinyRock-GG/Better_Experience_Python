using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BetterExperience.Features.AlternativeGenetics.Pooling;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolingGroup
{
	public class GeneSetGroup
	{
		public int Step { get; set; }

		public int MaxSteps { get; set; }

		public float Score { get; set; }

		public Dictionary<string, GeneSet> Samples { get; internal set; }

		public Dictionary<string, GenerationInfo> Infos { get; internal set; }
	}

	private MultithreadingFeature parallel;

	private Task<GeneSetGroup> aotGroup;

	public Dictionary<string, GenePool> Pools { get; private set; } = new Dictionary<string, GenePool>();

	public PoolingGroupData Data { get; private set; }

	public PoolingGroup(PoolingGroupData groupdata, MultithreadingFeature multithreading)
	{
		Data = groupdata;
		parallel = multithreading;
	}

	public void MigrateSettings(PoolingGroupSettings settings)
	{
		if (settings == null)
		{
			settings = new PoolingGroupSettings();
		}
		if (Data.Settings.Profile != settings.Profile)
		{
			return;
		}
		Data.Settings = settings;
		foreach (GenePool pool in Pools.Values)
		{
			pool.ForcedRecentCapacity = Data.Settings.RecentCapacity;
		}
	}

	public void Create()
	{
		foreach (GenePool pool in Pools.Values)
		{
			pool.CreateStrategy();
		}
	}

	internal void UpdateStatistics(string id, Dictionary<GeneId, GeneInfoEx> genes)
	{
		foreach (GenePool pool in Pools.Values)
		{
			UpdateStatistics(id, genes, pool);
		}
	}

	private void UpdateStatistics(string id, Dictionary<GeneId, GeneInfoEx> genes, GenePool pool)
	{
		GeneSet geneset = pool.ExtractGeneSet(genes.Values);
		if (geneset == null)
		{
			return;
		}
		List<GeneSet> yg = pool.GetGeneration(GeneGeneration.Young);
		Predicate<GeneSet> skipSurvivors = (GeneSet set) => !pool.Data.Survivors.Contains(set.Id);
		GeneSet mappedset = pool.FindOneInSet(yg, geneset, skipSurvivors);
		if (mappedset == null && pool.Data.DiversitySimilarityThreshold == 0f)
		{
			string gsid = pool.GetMappedGeneset(id);
			List<GeneSet> results = yg.Where((GeneSet x) => x.Id == gsid).ToList();
			if (results.Count > 0)
			{
				mappedset = results[0];
			}
		}
		if (mappedset != null)
		{
			mappedset.Vectors[GeneVector.Rating] = geneset.Rating;
		}
		if (mappedset != null && !geneset.IsIncomplete() && !mappedset.IsIncomplete())
		{
			if (mappedset.MappedGuestId != id)
			{
				pool.MarkSurvivor(mappedset.Id);
			}
		}
		else
		{
			pool.AddSurvivor(id, geneset);
		}
	}

	internal GeneSetGroup Apply(GuestInstance guest, Dictionary<GeneId, GeneInfoEx>.ValueCollection genes)
	{
		foreach (GenePool pool in Pools.Values)
		{
			pool.TryFixGeneOrder(genes);
			pool.CheckState();
		}
		GeneSetGroup result = GetNextGroup();
		foreach (KeyValuePair<string, GeneSet> kv in result.Samples)
		{
			GenePool pool2 = Pools[kv.Key];
			if (!pool2.Data.Enabled)
			{
				continue;
			}
			if (pool2.Data.Settings.StandardizeGrupoGenes)
			{
				for (int i = 0; i < pool2.Data.GeneOrder.Count; i++)
				{
					string gid = pool2.Data.GeneOrder[i];
					if (gid.Contains("_Grupo_") && pool2.GeneFactory.Eve.TryGetValue(new GeneId(gid), out var eveGene))
					{
						kv.Value.Vector[i] = eveGene.Value;
					}
				}
			}
			Pools[kv.Key].ApplySet(kv.Value, guest);
		}
		aotGroup = parallel.TrySubmit(ProduceGroup);
		return result;
	}

	private GeneSetGroup GetNextGroup()
	{
		try
		{
			if (aotGroup == null)
			{
				return ProduceGroup();
			}
			GeneSetGroup t = aotGroup.Result;
			aotGroup = null;
			foreach (KeyValuePair<string, GeneSet> s in t.Samples)
			{
				GenePool pool = Pools[s.Key];
				GeneSet sample = s.Value;
				if (pool.Data.Epoch != sample.Epoch)
				{
					return ProduceGroup();
				}
			}
			return t;
		}
		catch (Exception innerException)
		{
			throw new Exception("Unable to produce GS for " + Data.Settings.Name, innerException);
		}
	}

	private GeneSetGroup ProduceGroup()
	{
		Dictionary<string, GeneSet> fixedSets = new Dictionary<string, GeneSet>();
		Dictionary<string, GeneSet> randomSets = new Dictionary<string, GeneSet>();
		MultithreadingFeature.ParallelFork fork = parallel.CreateFork();
		Dictionary<string, GenerationInfo> infos = new Dictionary<string, GenerationInfo>();
		foreach (KeyValuePair<string, GenePool> kv in Pools)
		{
			string poolid = kv.Key;
			GenePool pool = kv.Value;
			GeneSet gs = pool.NextFixedSet();
			if (gs != null)
			{
				fixedSets[poolid] = gs;
				continue;
			}
			randomSets[poolid] = pool.CreateGeneSet();
			fork.Run(() => pool.NextRandomSet(randomSets[poolid], 0), delegate(GenerationInfo r)
			{
				infos[poolid] = r;
			});
		}
		fork.Join();
		int step = 0;
		GenerationScore score = new GenerationScore();
		if (fixedSets.Count == 0 && Data.Settings.Threshold > 0f)
		{
			for (step = 0; step < Data.Settings.GenerationAttempts; step++)
			{
				score = ComputeMergedScore(randomSets);
				if (score.IsAcceptable(Data.Settings.Threshold))
				{
					break;
				}
				fork = parallel.Fork(Pools, (KeyValuePair<string, GenePool> keyValuePair) => keyValuePair.Value.NextRandomSet(randomSets[keyValuePair.Key], step), delegate(KeyValuePair<string, GenePool> keyValuePair, GenerationInfo r)
				{
					infos[keyValuePair.Key] = r;
				});
				fork.Join();
			}
		}
		Dictionary<string, GeneSet> merged = new Dictionary<string, GeneSet>();
		fixedSets.ForEach(delegate(KeyValuePair<string, GeneSet> keyValuePair)
		{
			merged.Add(keyValuePair.Key, keyValuePair.Value);
		});
		randomSets.ForEach(delegate(KeyValuePair<string, GeneSet> keyValuePair)
		{
			merged.Add(keyValuePair.Key, keyValuePair.Value);
		});
		return new GeneSetGroup
		{
			Step = step,
			MaxSteps = Data.Settings.GenerationAttempts,
			Score = score.TotalScore,
			Samples = merged,
			Infos = infos
		};
	}

	private GenerationScore ComputeMergedScore(Dictionary<string, GeneSet> input)
	{
		Dictionary<string, float> scores = new Dictionary<string, float>();
		float separateMinimalScore = 0f;
		parallel.Fork(input, (KeyValuePair<string, GeneSet> kv) => ComputeScore(Pools[kv.Key], kv.Value), delegate(Dictionary<string, float> r)
		{
			HandlePartialScore(r, scores, ref separateMinimalScore);
		}).Join();
		int unguidedPoolCount = Pools.Values.Where((GenePool x) => x.Data.GeneOrder.Count == x.UnguidedGeneCount()).Count();
		float[] nums = scores.Values.ToArray();
		if (Data.Settings.PerceptionThreshold > 0f && unguidedPoolCount > 0)
		{
			nums.InplaceMap((float x) => x / (float)unguidedPoolCount);
			separateMinimalScore /= unguidedPoolCount;
		}
		float exactScore = ((nums.Length != 0 && nums.Where(float.IsNaN).Count() <= 0) ? nums.Min() : float.NaN);
		return new GenerationScore
		{
			TotalScore = (float.IsNaN(separateMinimalScore) ? exactScore : ((exactScore + separateMinimalScore) / 2f)),
			MinimalScore = separateMinimalScore,
			ExactScore = exactScore
		};
	}

	private Dictionary<string, float> ComputeScore(GenePool pool, GeneSet set)
	{
		if (pool.UnguidedGeneCount() != pool.Data.GeneOrder.Count)
		{
			return new Dictionary<string, float>();
		}
		if (Data.Settings.PerceptionThreshold > 0f)
		{
			PerceptionScoring perceptionScoring = new PerceptionScoring(Data.Settings.PerceptionThreshold, pool);
			Dictionary<string, float> score = new Dictionary<string, float>();
			{
				foreach (GeneSet gs in pool.GetGeneration(GeneGeneration.Recent))
				{
					score[gs.Id] = 1f - perceptionScoring.Score(set, gs);
				}
				return score;
			}
		}
		return pool.ScoreRecentGuests(set);
	}

	private void HandlePartialScore(Dictionary<string, float> partialScores, Dictionary<string, float> scores, ref float separateMinimalScore)
	{
		if (partialScores.Count > 0)
		{
			float[] values = partialScores.Values.Where((float x) => !float.IsNaN(x)).ToArray();
			if (values.Length != 0)
			{
				separateMinimalScore += values.Min();
			}
		}
		if (scores.Count == 0)
		{
			foreach (KeyValuePair<string, float> ps in partialScores)
			{
				scores[ps.Key] = ps.Value;
			}
			return;
		}
		HashSet<string> mergedSet = new HashSet<string>();
		foreach (string k in scores.Keys)
		{
			mergedSet.Add(k);
		}
		foreach (string k2 in partialScores.Keys)
		{
			mergedSet.Add(k2);
		}
		foreach (string key in mergedSet)
		{
			float current = float.NaN;
			scores.TryGetValue(key, out current);
			float next = float.NaN;
			partialScores.TryGetValue(key, out next);
			scores[key] = current + next;
		}
	}
}
