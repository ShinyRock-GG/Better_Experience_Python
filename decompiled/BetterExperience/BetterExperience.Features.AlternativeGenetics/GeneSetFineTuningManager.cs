using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GeneSetFineTuningManager
{
	private GenePool pool;

	private Dictionary<Regex, GeneFineTuning> patterns = new Dictionary<Regex, GeneFineTuning>();

	private Dictionary<string, GeneFineTuning> groupTunings = new Dictionary<string, GeneFineTuning>();

	private Dictionary<string, GeneFineTuning> tunings = new Dictionary<string, GeneFineTuning>();

	private ISet<string> checkedSet = new HashSet<string>();

	private bool fastGuidance = true;

	private Dictionary<string, string> groupToGGmap = new Dictionary<string, string>();

	public int GuidedCount { get; private set; }

	public int TuningCount => tunings.Count;

	internal float ApplyGeneDeviationLimit(string geneId, float value)
	{
		if (tunings.TryGetValue(geneId, out var tuning) && tuning.Guidance.HasValue)
		{
			return 1f;
		}
		return value;
	}

	public GeneSetFineTuningManager(GenePool pool)
	{
		this.pool = pool;
		foreach (KeyValuePair<string, GeneFineTuning> kv in pool.Data.Settings.FineTuning)
		{
			if (kv.Key.StartsWith("@"))
			{
				groupTunings[kv.Key.Substring(1)] = kv.Value;
				continue;
			}
			string expr = kv.Key.Split(new char[1] { '*' }).InplaceMap(Regex.Escape).Join(".*");
			Regex rex = new Regex(expr);
			patterns[rex] = kv.Value;
		}
		pool.Data.GeneOrder.ForEach(RegisterNewGene);
	}

	internal void RegisterNewGene(string geneId)
	{
		if (!checkedSet.Add(geneId))
		{
			return;
		}
		List<GeneFineTuning> matches = new List<GeneFineTuning>();
		foreach (KeyValuePair<Regex, GeneFineTuning> kv in patterns)
		{
			if (kv.Key.IsMatch(geneId))
			{
				matches.Add(kv.Value);
			}
		}
		string group = pool.GeneFactory.GeneToGroup[new GeneId(geneId).Item1];
		GeneFineTuning groupTuning = groupTunings.GetValueOrDefault(group, () => (GeneFineTuning)null);
		if (groupTuning != null)
		{
			matches.Add(groupTuning);
		}
		if (matches.Count != 0)
		{
			if (matches.Count > 1)
			{
				throw new Exception("Gene " + geneId + " matches multiple tuning groups");
			}
			tunings.Add(geneId, matches[0]);
			if (HasGuidance(geneId))
			{
				GuidedCount++;
			}
		}
	}

	private T? ReadTuning<T>(string geneId, Func<GeneFineTuning, T?> extractor) where T : struct
	{
		if (tunings.TryGetValue(geneId, out var tuning))
		{
			return extractor(tuning);
		}
		return null;
	}

	private T ReadTuning<T>(string geneId, Func<GeneFineTuning, T> extractor) where T : class
	{
		if (tunings.TryGetValue(geneId, out var tuning))
		{
			return extractor(tuning);
		}
		return null;
	}

	public float ApplyValueLimits(string geneId, float current)
	{
		if (pool.Data.GuidanceDisabled)
		{
			return current;
		}
		(float, float)? minmax = ReadTuning<(float, float)>(geneId, (Func<GeneFineTuning, (float, float)?>)((GeneFineTuning tuning) => tuning.MinMax));
		if (minmax.HasValue)
		{
			return Mathf.Clamp(current, minmax.Value.Item1, minmax.Value.Item2);
		}
		return current;
	}

	public float ApplyValueGuidance(string geneId, float current, float proposed)
	{
		if (float.IsNaN(current))
		{
			return proposed;
		}
		if (float.IsNaN(proposed))
		{
			return current;
		}
		if (pool.Data.GuidanceDisabled)
		{
			return proposed;
		}
		float goal = ReadTuning(geneId, (GeneFineTuning t) => t.Guidance) ?? float.NaN;
		if (float.IsNaN(goal))
		{
			return proposed;
		}
		if (fastGuidance)
		{
			return (current + goal) / 2f;
		}
		if (Mathf.Abs(goal - current) < Mathf.Abs(goal - proposed))
		{
			return current;
		}
		return proposed;
	}

	public bool HasGuidance(string gene)
	{
		if (pool.Data.GuidanceDisabled)
		{
			return false;
		}
		if (ReadTuning(gene, (GeneFineTuning t) => t.Guidance).HasValue)
		{
			return true;
		}
		if (ReadTuning<(float, float)>(gene, (Func<GeneFineTuning, (float, float)?>)((GeneFineTuning t) => t.MinMax)).HasValue)
		{
			return true;
		}
		return false;
	}

	internal float GetDeviationFactorTuning(string strid, float v)
	{
		return ReadTuning(strid, (GeneFineTuning t) => t.DeviationFactor) ?? v;
	}

	internal float GetEvolutionFactorTuning(string strid, float v)
	{
		return ReadTuning(strid, (GeneFineTuning t) => t.EvolutionFactor) ?? v;
	}

	public float GetSimilarityWeight(string geneId)
	{
		return ReadTuning(geneId, (GeneFineTuning t) => t.SimilarityWeight) ?? 1f;
	}

	public float GetInitialValue(string geneId, float value)
	{
		return ReadTuning(geneId, (GeneFineTuning t) => t.InitialValue) ?? value;
	}

	public IEnumerable<string> GetSymmetricGenes(string gene)
	{
		string symmetricGene = null;
		if (gene.Contains("_L#"))
		{
			symmetricGene = gene.Replace("_L#", "_R#");
		}
		else if (gene.Contains("_R#"))
		{
			symmetricGene = gene.Replace("_R#", "_L#");
		}
		if (symmetricGene == null)
		{
			return Enumerable.Empty<string>();
		}
		return Enumerable.Repeat(symmetricGene, 1);
	}

	internal float[] CreateMomentumLock(List<string> geneOrder, float[] gradient)
	{
		Dictionary<string, List<string>> groupToGene = new Dictionary<string, List<string>>();
		foreach (string gid in geneOrder)
		{
			string group = ReadTuning(gid, (GeneFineTuning t) => t.MomentumGroup);
			if (group != null)
			{
				groupToGene.GetValueOrAdd(group, () => new List<string>()).Add(gid);
			}
		}
		float[] result = new float[geneOrder.Count];
		for (int i = 0; i < geneOrder.Count; i++)
		{
			result[i] = float.NaN;
		}
		if (groupToGene.Count != 0)
		{
			foreach (KeyValuePair<string, List<string>> kv in groupToGene)
			{
				float signum = (pool.Random.NextBool() ? (-1f) : 1f);
				if (gradient != null)
				{
					signum = 0f;
					foreach (string gene in kv.Value)
					{
						float v = gradient[geneOrder.IndexOf(gene)];
						if (Mathf.Abs(v) > Mathf.Abs(signum))
						{
							signum = v;
						}
					}
					signum = Math.Sign(signum);
				}
				foreach (string gene2 in kv.Value)
				{
					result[geneOrder.IndexOf(gene2)] = signum;
				}
			}
		}
		return result;
	}
}
