using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.Features.AlternativeGenetics.Pooling;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GenePool
{
	private Logger logger = new Logger();

	private Dictionary<string, int> geneIndexCache;

	private GeneSetFineTuningManager tuningManager;

	private PoolingStrategy strategy;

	private Dictionary<string, string> groupMapping;

	private bool geneOrderChecked;

	public GenePoolData Data { get; private set; }

	public PredictableRandom Random { get; private set; }

	public Logger Logger => logger;

	private float SimilarityThresold => Data.DiversitySimilarityThreshold;

	public int Level => (int)SimilarityThresold;

	public int Capacity => Data.InitialCapacity + Data.ExtendedCapacity;

	public int ForcedRecentCapacity { get; set; } = -1;

	public int RecentCapacity
	{
		get
		{
			if (ForcedRecentCapacity < 0)
			{
				return Capacity;
			}
			return ForcedRecentCapacity;
		}
	}

	public int GuaranteedRandoms
	{
		get
		{
			if (!Data.Active)
			{
				return 0;
			}
			return Data.GuaranteedRandoms;
		}
	}

	public int IterationLength => 10;

	public int GenerationCapacity => IterationLength - GuaranteedRandoms;

	private bool Empty => (from x in GetGeneration(GeneGeneration.Young)
		where x.MappedGuestId == null
		select x).ToArray().Length == 0;

	public int Remaining => (from x in GetGeneration(GeneGeneration.Young)
		where x.MappedGuestId == null
		select x).ToArray().Length + Data.DiversityPenalty;

	public GeneFactoryInfo GeneFactory { get; private set; }

	public MultithreadingFeature Multithreading { get; }

	public GeneSetFineTuningManager FineTuning => tuningManager;

	public event Action<GenePool> PostCompact = delegate
	{
	};

	public event Action<GenePool, PoolStateSnapshot, PoolStateSnapshot> PostRefresh = delegate
	{
	};

	public event Action<GenePool, GeneSet> PostProcessGeneSet = delegate
	{
	};

	public event Action<GenePool> OnNewEpochStart = delegate
	{
	};

	internal float[] CreateMomentumLock(float[] gradient)
	{
		if (Data.Settings.UseMomentum)
		{
			return FineTuning.CreateMomentumLock(Data.GeneOrder, gradient);
		}
		return new float[Data.GeneOrder.Count];
	}

	public GenePool(GenePoolData data, GeneFactoryInfo geneFactory, AlternativeGeneMapping mapping)
	{
		groupMapping = mapping;
		GeneFactory = geneFactory;
		Data = data;
		if (Data.Seed == 0)
		{
			Data.Seed = Environment.TickCount;
		}
		if (data.GeneOrder == null)
		{
			data.GeneOrder = new List<string>();
		}
		if (data.Generations == null)
		{
			data.Generations = new Dictionary<GeneGeneration, List<GeneSet>>();
		}
		tuningManager = new GeneSetFineTuningManager(this);
		Random = new PredictableRandom(Data.Seed);
		logger.Prefix = "[ Pool " + Data.Settings.Name + "]";
	}

	internal void Clear()
	{
		Data.Generations.Clear();
		Data.DiversitySimilarityThreshold = 0f;
		Data.DiversityPenalty = 0;
		Data.ExtendedCapacity = 0;
		tuningManager = new GeneSetFineTuningManager(this);
		Data.Error = 0f;
		Data.Iteration = 0;
		Data.Epoch = 0;
		geneOrderChecked = false;
	}

	internal (int, int) MigrateSettings(PoolSettings settings)
	{
		int success = 0;
		int failure = 0;
		PoolSettings current = Data.Settings;
		if (settings.EnforceSymmetry != current.EnforceSymmetry)
		{
			success++;
			current.EnforceSymmetry = settings.EnforceSymmetry;
			logger.Info("Pool migration: Changed EnforceSymmetry to {0}", current.EnforceSymmetry);
		}
		if (!object.Equals(settings.Name, current.Name))
		{
			current.Name = settings.Name;
			success++;
			logger.Info("Pool migration: Pool name changed to {0}", current.Name);
		}
		if (!settings.ForcedValue.ContentEquals(current.ForcedValue))
		{
			current.ForcedValue = settings.ForcedValue;
			success++;
			logger.Info("Pool migration: Changed forced value");
		}
		if (!object.Equals(settings.Hidden, current.Hidden))
		{
			current.Hidden = settings.Hidden;
			success++;
			logger.Info("Pool migration: Changed hidden flag");
		}
		if (!object.Equals(settings.FastPass, current.FastPass))
		{
			current.FastPass = settings.FastPass;
			success++;
			logger.Info("Pool migration: Changed fast pass flag");
		}
		if (!object.Equals(settings.UseMomentum, current.UseMomentum))
		{
			current.UseMomentum = settings.UseMomentum;
			success++;
			logger.Info("Pool migration: Changed use momentum flag");
		}
		if (!object.Equals(settings.Comparator, current.Comparator))
		{
			current.Comparator = settings.Comparator;
			success++;
			logger.Info("Pool migration: Changed comparator");
		}
		if (!object.Equals(settings.StandardizeGrupoGenes, current.StandardizeGrupoGenes))
		{
			current.StandardizeGrupoGenes = settings.StandardizeGrupoGenes;
			success++;
			logger.Info("Pool migration: Changed StandardizeGrupoGenes");
		}
		if (!settings.Groups.ContentEquals(current.Groups))
		{
			List<string> deadgroups = current.Groups.Except(settings.Groups).ToList();
			if (deadgroups.Count > 0)
			{
				logger.Error("Remove groups {0}", string.Join(";", deadgroups.ToArray()));
				List<string> deadGenes = new List<string>();
				foreach (string grp in deadgroups)
				{
					List<string> genes = GeneFactory.GroupToGenes[grp];
					foreach (string geneid in Data.GeneOrder)
					{
						if (genes.Contains(new GeneId(geneid).Item1))
						{
							deadGenes.Add(geneid);
						}
					}
				}
				RemoveFromGeneOrder(deadGenes);
			}
			current.Groups = settings.Groups;
			logger.Info("Pool migration: Changed groups");
		}
		current.FineTuning = settings.FineTuning;
		PoolingFactory factory = new PoolingFactory();
		PoolingStrategy initialStrategy = factory.Create(this);
		current.Pooling = settings.Pooling;
		PoolingStrategy newStretegy = factory.Create(this);
		if (initialStrategy.GetType() != newStretegy.GetType())
		{
			logger.Error("Pool cleared due to strategy change");
			Clear();
		}
		tuningManager = new GeneSetFineTuningManager(this);
		return (success, failure);
	}

	private void RemoveFromGeneOrder(List<string> deadgenes)
	{
		List<string> newGeneOrder = Data.GeneOrder.Where((string x) => !deadgenes.Contains(x)).ToList();
		int[] neworder = newGeneOrder.Select((string x) => Data.GeneOrder.IndexOf(x)).ToArray();
		foreach (List<GeneSet> set in Data.Generations.Values)
		{
			foreach (GeneSet gs in set)
			{
				GeneVector[] array = gs.Vectors.Keys.ToArray();
				foreach (GeneVector k in array)
				{
					float[] v = gs.Vectors[k];
					gs.Vectors[k] = MigrateVector(v, neworder);
				}
			}
		}
		Data.DiversitySimilarityThreshold = 0f;
		Data.GeneOrder = newGeneOrder;
	}

	internal GeneSet CreateGeneSet()
	{
		GeneSet result = new GeneSet();
		result.Id = Guid.NewGuid().ToString();
		result.Vectors = new Dictionary<GeneVector, float[]>();
		result.Vectors[GeneVector.Data] = new float[Data.GeneOrder.Count].Fill(float.NaN);
		result.Iteration = Data.Iteration;
		result.Epoch = Data.Epoch;
		return result;
	}

	private float[] MigrateVector(float[] vector, int[] neworder)
	{
		if (vector == null)
		{
			return null;
		}
		float[] result = new float[neworder.Length];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = vector[neworder[i]];
		}
		return result;
	}

	public string GetMappedGeneset(string guestId)
	{
		foreach (List<GeneSet> gss in Data.Generations.Values)
		{
			foreach (GeneSet gs in gss)
			{
				if (gs.MappedGuestId == guestId)
				{
					return gs.Id;
				}
			}
		}
		return null;
	}

	internal void MarkSurvivor(string mappedset)
	{
		Data.Survivors.Add(mappedset);
	}

	internal void AddSurvivor(string id, GeneSet geneset)
	{
		Data.Survivors.Add(geneset.Id);
		GetGeneration(GeneGeneration.Young).Add(geneset);
		geneset.MappedGuestId = id;
	}

	public List<GeneSet> GetGeneration(GeneGeneration generation)
	{
		return Data.Generations.GetValueOrAdd(generation, () => new List<GeneSet>());
	}

	internal bool Matches(GeneInfoEx gene)
	{
		string group = Extensions.GetValueOrDefault(groupMapping, gene.Id.Item1, gene.Group);
		return Data.Settings.Groups.Contains(group);
	}

	public int GeneIndex(string geneId)
	{
		if (geneIndexCache == null)
		{
			geneIndexCache = new Dictionary<string, int>();
			for (int i = 0; i < Data.GeneOrder.Count; i++)
			{
				geneIndexCache[Data.GeneOrder[i]] = i;
			}
		}
		if (!geneIndexCache.TryGetValue(geneId, out var index))
		{
			index = Data.GeneOrder.IndexOf(geneId);
			if (index < 0)
			{
				index = Data.GeneOrder.Count;
				Data.GeneOrder.Add(geneId);
				tuningManager.RegisterNewGene(geneId);
				ExpandVectors();
			}
			geneIndexCache[geneId] = index;
		}
		return index;
	}

	internal void SetGeneValue(GeneSet result, string geneId, float value, bool nosymmetry = false, bool guidance = true, float step = 0f)
	{
		if (!float.IsNaN(value))
		{
			value = Mathf.Clamp(Mathf.Abs(value), 0f, 1f);
		}
		if (step > 0f)
		{
			float x = value / step;
			value = Mathf.Round(x) * step;
		}
		int idx = GeneIndex(geneId);
		if (result.Vector == null)
		{
			result.Vectors[GeneVector.Data] = new float[Data.GeneOrder.Count];
			result.Vector.Fill(float.NaN);
		}
		else if (result.Vector.Length < Data.GeneOrder.Count)
		{
			float[] copy = new float[Data.GeneOrder.Count];
			copy.Fill(float.NaN);
			Array.Copy(result.Vector, copy, result.Vector.Length);
			result.Vectors[GeneVector.Data] = copy;
		}
		if (guidance)
		{
			value = tuningManager.ApplyValueGuidance(geneId, result.Vector[idx], value);
		}
		value = tuningManager.ApplyValueLimits(geneId, value);
		if (result.Vector[idx] == value)
		{
			return;
		}
		if (Data.Settings.EnforceSymmetry && !nosymmetry)
		{
			foreach (string symmetricGene in tuningManager.GetSymmetricGenes(geneId))
			{
				SetGeneValue(result, symmetricGene, value, nosymmetry: true);
			}
		}
		result.Vector[idx] = value;
	}

	internal void Refresh()
	{
		logger.Info("Refreshing pool");
		if (!Empty)
		{
			logger.Info("Refresh cancelled. Not empty");
			return;
		}
		PoolStateSnapshot snapshot = TakeStateSnapshot();
		int epoch = Data.Epoch;
		strategy.Populate();
		if (Data.Epoch > epoch)
		{
			this.OnNewEpochStart(this);
		}
		Data.Iteration++;
		this.PostRefresh(this, snapshot, TakeStateSnapshot());
		Data.Survivors.Clear();
		Data.Statistics.NonDeviantGenes = 0;
	}

	public void InvokePostProcessor(GeneSet gs)
	{
		this.PostProcessGeneSet(this, gs);
	}

	public List<Tuple<GeneSet, float>> FindInSet(List<GeneSet> set, GeneSet value, bool ignoreThreshold = false, float thresholdFactor = 1f, bool ignoreTuning = false, float forcedThreshold = -1f)
	{
		int geneLength = Data.GeneOrder.Count;
		List<Tuple<GeneSet, float>> finds = new List<Tuple<GeneSet, float>>();
		float threshold = ((forcedThreshold > 0f) ? forcedThreshold : SimilarityThresold);
		threshold *= thresholdFactor;
		foreach (GeneSet x in set)
		{
			float featureScore = SimilarityDistance(value, x, ignoreTuning);
			if (featureScore <= threshold || ignoreThreshold)
			{
				finds.Add(new Tuple<GeneSet, float>(x, featureScore));
			}
			else if (SimilarityThresold == 0f && value.Ancestors.Contains(x.Id))
			{
				finds.Add(new Tuple<GeneSet, float>(x, featureScore));
			}
		}
		finds.Sort((Tuple<GeneSet, float> a, Tuple<GeneSet, float> b) => a.Item2.CompareTo(b.Item2));
		return finds;
	}

	public float SimilarityDistance(GeneSet value, GeneSet x, bool ignoreTuning = false)
	{
		if (Data.Settings.Comparator == "cos")
		{
			return CosineDistance(value, x);
		}
		if (Data.Settings.Comparator == "eucos")
		{
			return EuclideanDistance(value, x) * CosineDistance(value, x);
		}
		return EuclideanDistance(value, x, ignoreTuning);
	}

	public float CosineDistance(GeneSet a, GeneSet b)
	{
		float num = ArrayUtil.TransformVectors(a.Vector, b.Vector, (float i, float j) => i * j).Sum();
		float norma = a.Vector.Select((float i) => i * i).Sum();
		norma = Mathf.Sqrt(norma);
		float normb = b.Vector.Select((float i) => i * i).Sum();
		normb = Mathf.Sqrt(normb);
		float value = 1f - num / (norma * normb);
		if ((double)value < 1E-05)
		{
			return 0f;
		}
		return value;
	}

	public float EuclideanDistance(GeneSet value, GeneSet x, bool ignoreTuning = false)
	{
		float[] diff = ArrayUtil.TransformVectors(value.Vector, x.Vector, (float a, float b) => Mathf.Pow(a - b, 2f));
		if (!ignoreTuning)
		{
			diff.InplaceMap((int index, float val) => tuningManager.GetSimilarityWeight(Data.GeneOrder[index]) * val);
		}
		return Mathf.Sqrt(diff.Sum());
	}

	internal GeneSet FindOneInSet(List<GeneSet> set, GeneSet value, Predicate<GeneSet> matchPredicate = null)
	{
		List<Tuple<GeneSet, float>> values = FindInSet(set, value);
		if (values.Count > 0)
		{
			foreach (Tuple<GeneSet, float> v in values)
			{
				if (matchPredicate == null || matchPredicate(v.Item1))
				{
					return v.Item1;
				}
			}
		}
		return null;
	}

	private GeneSet NextUnmapped(List<GeneSet> set)
	{
		List<GeneSet> remaining = set.FindAll((GeneSet gs) => gs.MappedGuestId == null).ToList();
		if (Data.DiversityPenalty > 0 && Random.NextInt(remaining.Count + Data.DiversityPenalty) > remaining.Count)
		{
			return null;
		}
		if (remaining.Count > 1)
		{
			return remaining.InplaceShuffle(Random.NextInt).FirstOrDefault();
		}
		return remaining.FirstOrDefault();
	}

	public void TryFixGeneOrder(ICollection<GeneInfoEx> genes)
	{
		if (geneOrderChecked)
		{
			return;
		}
		geneOrderChecked = true;
		HashSet<string> matches = new HashSet<string>();
		foreach (GeneInfoEx gene in genes)
		{
			if (Matches(gene))
			{
				matches.Add(gene.Id.ToString());
				if (float.IsNaN(gene.Value))
				{
					logger.Error("Gene {0} is nan", gene.Id);
				}
			}
		}
		if (matches.Count != Data.GeneOrder.Count)
		{
			UpdateToNewGeneOrder(matches);
		}
		strategy.Initialize();
	}

	private void UpdateToNewGeneOrder(ICollection<string> actualGenes)
	{
		int before = Data.GeneOrder.Count;
		List<string> deadgenes = Data.GeneOrder.Except(actualGenes).ToList();
		RemoveFromGeneOrder(deadgenes);
		if (deadgenes.Count > 0)
		{
			logger.Error("Removed {0} genes. {1}->{2}", deadgenes.Count, before, Data.GeneOrder.Count);
		}
		int initialcount = Data.GeneOrder.Count;
		foreach (string s in actualGenes)
		{
			GeneIndex(s);
		}
		if (initialcount < Data.GeneOrder.Count)
		{
			ExpandVectors();
		}
	}

	private void ExpandVectors()
	{
		foreach (List<GeneSet> gss in Data.Generations.Values)
		{
			foreach (GeneSet gs in gss)
			{
				GeneVector[] array = gs.Vectors.Keys.ToArray();
				foreach (GeneVector k in array)
				{
					float[] vector = new float[Data.GeneOrder.Count].Fill(float.NaN);
					gs.Vectors[k].CopyTo(vector, 0);
					gs.Vectors[k] = vector;
				}
			}
		}
	}

	private void AddToRecentSet(GeneSet gs)
	{
		List<GeneSet> recent = GetGeneration(GeneGeneration.Recent);
		if (recent.Count >= Capacity)
		{
			recent.RemoveAt(0);
		}
		recent.Add(gs);
	}

	private void ApplySetTo(GeneSet gs, GuestInstance guest)
	{
		if (GetGeneration(GeneGeneration.Young).Contains(gs))
		{
			gs.MappedGuestId = guest.Id;
		}
		List<GeneInfo> update = new List<GeneInfo>();
		for (int i = 0; i < gs.Vector.Length; i++)
		{
			if (Data.Settings.ForcedValue != null && Data.Settings.ForcedValue.Count > 0)
			{
				gs.Vector[i] = Data.Settings.ForcedValue[0];
			}
			float value = gs.Vector[i];
			if (!float.IsNaN(value))
			{
				GeneInfo gi = new GeneInfo
				{
					Id = new GeneId(Data.GeneOrder[i]),
					Value = value
				};
				update.Add(gi);
			}
		}
		guest.UpdateAll(update);
	}

	internal void Flush()
	{
		Data.Seed = Random.Seed;
	}

	public GeneSet ExtractGeneSet(ICollection<GeneInfoEx> set, bool norate = false)
	{
		GeneSet result = CreateGeneSet();
		bool accept = false;
		foreach (GeneInfoEx gene in set)
		{
			if (!Matches(gene))
			{
				continue;
			}
			float rating = gene.Rating;
			if (rating > 0f || norate)
			{
				SetGeneValue(result, gene.Id.ToString(), gene.Value, nosymmetry: false, guidance: false);
				accept = accept || rating > 0f;
				if (!norate)
				{
					if (result.Rating == null)
					{
						result.Vectors[GeneVector.Rating] = new float[Data.GeneOrder.Count];
					}
					int idx = Data.GeneOrder.IndexOf(gene.Id.ToString());
					if (idx >= result.Rating.Length)
					{
						float[] newvec = new float[Data.GeneOrder.Count];
						result.Rating.CopyTo(newvec, 0);
						result.Vectors[GeneVector.Rating] = newvec;
					}
					result.Rating[idx] = rating;
				}
			}
			else
			{
				SetGeneValue(result, gene.Id.ToString(), float.NaN, nosymmetry: false, guidance: false);
			}
		}
		if (accept || norate)
		{
			return result;
		}
		return null;
	}

	private PoolStateSnapshot TakeStateSnapshot()
	{
		return new PoolStateSnapshot
		{
			OldCount = GetGeneration(GeneGeneration.Old).Count,
			MatureCount = GetGeneration(GeneGeneration.Mature).Count,
			YoungCount = GetGeneration(GeneGeneration.Young).Count,
			Level = Level,
			Threshold = SimilarityThresold
		};
	}

	internal void CompleteGeneSet(GeneSet target)
	{
	}

	internal void Fit()
	{
	}

	internal void Restart()
	{
		Data.DiversityPenalty = 0;
		GetGeneration(GeneGeneration.Young).Clear();
		GetGeneration(GeneGeneration.Mature).Clear();
	}

	internal void FirePostProcessGeneSet(GeneSet gs)
	{
		this.PostProcessGeneSet(this, gs);
	}

	internal void FirePostCompact()
	{
		this.PostCompact(this);
	}

	internal void CreateStrategy()
	{
		strategy = new PoolingFactory().Create(this);
	}

	public GeneSet NextFixedSet()
	{
		if (Data.Enabled)
		{
			return NextUnmapped(GetGeneration(GeneGeneration.Young));
		}
		return null;
	}

	public GenerationInfo NextRandomSet(GeneSet target, int step)
	{
		return strategy.ProduceRandomGeneSet(target, step);
	}

	public void ApplySet(GeneSet gs, GuestInstance guest)
	{
		gs.MappedGuestId = guest.Id;
		ApplySetTo(gs, guest);
		AddToRecentSet(gs);
		if (!GetGeneration(GeneGeneration.Young).Contains(gs) && Data.DiversityPenalty > 0)
		{
			Data.DiversityPenalty--;
		}
	}

	internal Dictionary<string, float> ScoreRecentGuests(GeneSet value)
	{
		Dictionary<string, float> result = new Dictionary<string, float>();
		foreach (GeneSet rg in GetGeneration(GeneGeneration.Recent))
		{
			result[rg.MappedGuestId] = SimilarityDistance(rg, value);
		}
		return result;
	}

	public GeneSet[] GetSpecialDumpVectors()
	{
		return strategy.GetSpecialDumpVectors();
	}

	internal void NotifyPoolUpgraded()
	{
	}

	internal GeneSet CopyGeneSet(GeneSet g)
	{
		GeneSet gs = CreateGeneSet();
		g.Vector.CopyTo(gs.Vector, 0);
		return gs;
	}

	public VersionCachedValue<T> EpochCachedValue<T>(Func<T> producer) where T : class
	{
		return new VersionCachedValue<T>(() => Data.Epoch, producer);
	}

	public VersionCachedValue<T> IterationCachedValue<T>(Func<T> producer) where T : class
	{
		return new VersionCachedValue<T>(() => Data.Iteration, producer);
	}

	internal void CheckState()
	{
		if (Data.Enabled && Empty && Data.DiversityPenalty <= 0)
		{
			Refresh();
		}
	}

	internal int UnguidedGeneCount()
	{
		if (Data.GuidanceDisabled)
		{
			return Data.GeneOrder.Count;
		}
		return Data.GeneOrder.Count - FineTuning.GuidedCount;
	}
}
