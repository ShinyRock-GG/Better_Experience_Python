using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class RollingMeanPooling : PoolingStrategy
{
	private GenePool pool;

	private RollingMeanConfiguration settings;

	private VersionCachedValue<GeneSet> activeAverage;

	private VersionCachedValue<float[]> activeGradient;

	private PredictableRandom Random => pool.Random;

	private Logger logger => pool.Logger;

	private GenePoolData Data => pool.Data;

	public RollingMeanPooling(GenePool pool, RollingMeanConfiguration settings)
	{
		this.pool = pool;
		this.settings = settings;
		activeAverage = pool.EpochCachedValue(CreateAverage);
		activeGradient = pool.IterationCachedValue(() => CreateGradient(activeAverage.Value.Vector));
	}

	private GeneSet CreateAverage()
	{
		List<GeneSet> og = pool.GetGeneration(GeneGeneration.Old);
		if (og.Count <= 1)
		{
			return og[0];
		}
		return CreateWeightedAverageSet(og);
	}

	public void Populate()
	{
		List<GeneSet> mature = pool.GetGeneration(GeneGeneration.Mature);
		List<GeneSet> old = pool.GetGeneration(GeneGeneration.Old);
		Data.DiversitySimilarityThreshold = 0f;
		MoveYoungToMature();
		if (mature.Count >= pool.Data.InitialCapacity)
		{
			List<GeneSet> bgen = pool.GetGeneration(GeneGeneration.Buffer);
			MoveMatureToBuffer();
			GeneSet nextgen = CreateWeightedAverageSet(bgen, useRatingAsWeight: true);
			GeneSet average = CreateWeightedAverageSet(old);
			nextgen.Vectors.Remove(GeneVector.Rating);
			old.Add(nextgen);
			while (old.Count > settings.Capacity)
			{
				GeneSet replacement = CreateWeightedAverageSet(old.GetRange(0, 2));
				old.RemoveAt(0);
				old[0] = replacement;
			}
			float rating = Mathf.InverseLerp(0.1f, 0f, CreateAbsGradient(average.Vector).Select(Mathf.Abs).Max());
			pool.Data.Error = rating * 10f;
			GenerateMovingWeightedStdDev(average, bgen, nextgen.StdDev);
			mature.Clear();
			pool.Data.Epoch++;
		}
		Data.DiversityPenalty = pool.Data.InitialCapacity - mature.Count;
	}

	private void MoveMatureToBuffer()
	{
		List<GeneSet> bgen = pool.GetGeneration(GeneGeneration.Buffer);
		if (bgen.Count > pool.Capacity)
		{
			List<GeneSet> scored = (from gs in bgen
				select (gs: gs, iter: gs.Iteration, score: gs.Rating.Average()) into x
				orderby x.score, x.iter
				select x.gs).ToList();
			while (bgen.Count > pool.Capacity)
			{
				GeneSet todel = scored[0];
				scored.RemoveAt(0);
				bgen.Remove(todel);
			}
		}
		List<GeneSet> mgen = pool.GetGeneration(GeneGeneration.Mature);
		bgen.AddRange(mgen);
		mgen.Clear();
	}

	private void MoveYoungToMature()
	{
		List<GeneSet> young = pool.GetGeneration(GeneGeneration.Young);
		List<GeneSet> mature = pool.GetGeneration(GeneGeneration.Mature);
		List<GeneSet> old = pool.GetGeneration(GeneGeneration.Old);
		float[] average = CreateWeightedAverageSet(old).Vector;
		foreach (GeneSet gs in young)
		{
			if (!Data.Survivors.Contains(gs.Id) || gs.Rating == null)
			{
				continue;
			}
			for (int i = 0; i < gs.Vector.Length; i++)
			{
				if (gs.Rating[i] == 0f && average.Length > i)
				{
					gs.Vector[i] = average[i];
				}
			}
			mature.Add(gs);
		}
		young.Clear();
	}

	private void GenerateMovingWeightedStdDev(GeneSet average, List<GeneSet> samples, float[] stdDev)
	{
		for (int i = 0; i < stdDev.Length; i++)
		{
			float sum = 0f;
			float weight = 0f;
			foreach (GeneSet gs in samples)
			{
				float dv = average.Vector[i] - gs.Vector[i];
				float rate = gs.Rating[i];
				sum += rate * dv * dv;
				weight += rate;
			}
			int n = samples.Count;
			float nom = sum;
			float denom = (float)(n - 1) * weight / (float)n;
			stdDev[i] = Mathf.Sqrt(nom / denom);
		}
	}

	private GeneSet CreateWeightedAverageSet(List<GeneSet> subset, bool useRatingAsWeight = false)
	{
		float[] weightedAverage = new float[pool.Data.GeneOrder.Count];
		float[] ratingAverage = new float[pool.Data.GeneOrder.Count];
		weightedAverage.Fill(float.NaN);
		ratingAverage.Fill(float.NaN);
		for (int i = 0; i < weightedAverage.Length; i++)
		{
			float weightedSum = 0f;
			float totalWeight = 0f;
			int marks = 0;
			int j = 0;
			foreach (GeneSet gs in subset)
			{
				if (gs.Vector.Length > i && !float.IsNaN(gs.Vector[i]))
				{
					float rate = ((!useRatingAsWeight) ? (1f + (float)j * settings.GenerationWeight) : ((gs.Rating != null) ? gs.Rating[i] : 0.1f));
					weightedSum += gs.Vector[i] * rate;
					totalWeight += rate;
					marks++;
				}
				j++;
			}
			if (weightedSum > 0f)
			{
				weightedAverage[i] = weightedSum / totalWeight;
				ratingAverage[i] = totalWeight / (float)marks;
			}
		}
		GeneSet result = new GeneSet();
		result.Iteration = pool.Data.Iteration;
		for (int k = 0; k < weightedAverage.Length; k++)
		{
			pool.SetGeneValue(result, pool.Data.GeneOrder[k], weightedAverage[k], nosymmetry: false, guidance: true, settings.Step);
		}
		result.Vectors[GeneVector.Data] = weightedAverage;
		result.Vectors[GeneVector.Rating] = ratingAverage;
		float[] wstddev = new float[pool.Data.GeneOrder.Count];
		for (int l = 0; l < wstddev.Length; l++)
		{
			float weightedSum2 = 0f;
			float totalWeight2 = 0f;
			int n = 0;
			int j2 = 0;
			foreach (GeneSet gs2 in subset)
			{
				if (!float.IsNaN(gs2.Vector[l]) && !float.IsNaN(weightedAverage[l]))
				{
					float rate2 = ((!useRatingAsWeight) ? (1f + (float)j2 * settings.GenerationWeight) : ((gs2.Rating != null) ? gs2.Rating[l] : 0.1f));
					float dv = weightedAverage[l] - gs2.Vector[l];
					weightedSum2 += rate2 * dv * dv;
					totalWeight2 += rate2;
					n++;
				}
				j2++;
			}
			switch (n)
			{
			case 0:
				wstddev[l] = float.NaN;
				continue;
			case 1:
				wstddev[l] = Mathf.Sqrt(weightedSum2 / totalWeight2);
				continue;
			}
			float nom = weightedSum2;
			float denom = (float)(n - 1) * totalWeight2 / (float)n;
			wstddev[l] = Mathf.Sqrt(nom / denom);
		}
		result.Vectors[GeneVector.StdDev] = wstddev;
		return result;
	}

	private float[] CreateGradient(float[] averageSample)
	{
		return CreateAbsGradient(averageSample).InplaceMap((float x) => (settings.GradLockThreshold > 0f && Mathf.Abs(x) >= settings.GradLockThreshold) ? Math.Sign(x) : 0);
	}

	private float[] CreateAbsGradient(float[] averageSample)
	{
		List<GeneSet> ogen = pool.GetGeneration(GeneGeneration.Old);
		float[] gradlock = new float[Data.GeneOrder.Count];
		if (ogen.Count > 1)
		{
			GeneSet last = ogen[ogen.Count - 1];
			for (int i = 0; i < Data.GeneOrder.Count; i++)
			{
				float diff = last.Vector[i] - averageSample[i];
				if (!float.IsNaN(diff))
				{
					gradlock[i] = diff;
				}
			}
		}
		return gradlock;
	}

	public GenerationInfo ProduceRandomGeneSet(GeneSet gs, int attempt)
	{
		float stepFactor = 1f + (float)(attempt / settings.StepsToUpdateWeight) * settings.WeightUpdateScale;
		float[] momentumLock = ((!pool.Data.Settings.UseMomentum) ? new float[pool.Data.GeneOrder.Count].Fill(float.NaN) : pool.FineTuning.CreateMomentumLock(pool.Data.GeneOrder, null));
		GeneSet mean = activeAverage.Value;
		float[] averageSample = mean.Vector;
		float[] gradient = activeGradient.Value;
		List<GeneSet> ogen = pool.GetGeneration(GeneGeneration.Old);
		if (ogen.Count > 0)
		{
			int upgradeDistance = Data.Iteration - ogen[ogen.Count - 1].Iteration;
			if (upgradeDistance % 2 == 0)
			{
				gradient.Fill(0f);
			}
		}
		for (int i = 0; i < gs.Vector.Length; i++)
		{
			string gene = pool.Data.GeneOrder[i];
			float delta = Mathf.Min(averageSample[i], 1f - averageSample[i]);
			float effectiveDeviationWeight = ((gradient[i] == 0f) ? settings.SigmaStdDevWeight : 0f);
			float denom = Mathf.Max(1E-05f, effectiveDeviationWeight + settings.SigmaDeltaWeight);
			float nom = mean.StdDev[i] * effectiveDeviationWeight + delta * settings.DeltaToStdDev * settings.SigmaDeltaWeight;
			float specificRate = nom / denom;
			float collapsePoint = float.NaN;
			if (settings.CollapseThreshold > 0f && delta < settings.CollapseThreshold)
			{
				if (pool.Random.NextFloat(1f) <= settings.CollapseRetainProb)
				{
					collapsePoint = ((!(averageSample[i] < 0.5f)) ? (averageSample[i] + delta / 2f) : (averageSample[i] - delta / 2f));
					if (gradient[i] != 0f && Mathf.Sign(collapsePoint - 0.5f) != gradient[i])
					{
						gradient[i] = 0f;
					}
				}
				else
				{
					collapsePoint = ((!(averageSample[i] < 0.5f)) ? (1f - settings.CollapseThreshold) : settings.CollapseThreshold);
					delta = settings.CollapseThreshold;
					specificRate = delta;
				}
			}
			if (settings.Step > 0f)
			{
				delta = Mathf.Max(settings.Step, delta, 0.05f);
			}
			specificRate = Mathf.Min(delta, specificRate);
			float value = averageSample[i];
			float stddev = specificRate * settings.SigmaScale * stepFactor;
			float change = ((!float.IsNaN(collapsePoint)) ? (Random.NextNormalStdDev(collapsePoint, stddev * settings.CollapsedSigmaScale) - value) : (Random.NextNormalStdDev(value, stddev) - value));
			if (!float.IsNaN(momentumLock[i]))
			{
				change = Mathf.Abs(change) * momentumLock[i];
			}
			if (settings.BiasFactor > 0f && delta <= settings.BiasFactor && Mathf.Sign(averageSample[i] - 0.5f) != Mathf.Sign(change))
			{
				change *= settings.BiasFactor;
			}
			value += change;
			if (gradient[i] != 0f)
			{
				if (Mathf.Sign(gradient[i]) != Mathf.Sign(value - averageSample[i]))
				{
					value = averageSample[i] + gradient[i] * Mathf.Abs(value - averageSample[i]) * settings.GradientScale;
				}
				else if (settings.Step > 0f)
				{
					value = averageSample[i] + gradient[i] * settings.Step;
				}
			}
			pool.SetGeneValue(gs, gene, value, nosymmetry: false, guidance: true, settings.Step);
		}
		pool.FirePostProcessGeneSet(gs);
		pool.CompleteGeneSet(gs);
		return new GenerationInfo();
	}

	private void InitializePool()
	{
		GeneSet gs = pool.CreateGeneSet();
		gs.InitVector(GeneVector.Data, 0.5f);
		gs.InitVector(GeneVector.StdDev, settings.InitializerStdDev);
		if (settings.Initializer == VectorInitializer.eve || settings.Initializer == VectorInitializer.center_eve)
		{
			Dictionary<GeneId, GeneInfoEx> eve = pool.GeneFactory.Eve;
			GeneSet evegs = pool.ExtractGeneSet(eve.Values, norate: true);
			for (int i = 0; i < gs.Vector.Length; i++)
			{
				if (settings.Initializer == VectorInitializer.eve)
				{
					gs.Vector[i] = evegs.Vector[i];
				}
				else
				{
					gs.Vector[i] = (0.5f + evegs.Vector[i]) / 2f;
				}
			}
		}
		pool.GetGeneration(GeneGeneration.Old).Add(gs);
	}

	public GeneSet[] GetSpecialDumpVectors()
	{
		GeneSet average = CreateWeightedAverageSet(pool.GetGeneration(GeneGeneration.Old));
		average.Id = "wmean";
		GeneSet ggs = pool.CreateGeneSet();
		ggs.Vectors[GeneVector.Data] = CreateGradient(average.Vector);
		ggs.Id = "grad";
		return new GeneSet[2] { average, ggs };
	}

	public void Initialize()
	{
		if (pool.GetGeneration(GeneGeneration.Old).Count == 0)
		{
			InitializePool();
		}
	}
}
