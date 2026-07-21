using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class SelectionPooling : AbstractPoolingStrategy<SelectionConfiguration>
{
	private class SelectionOpportunities
	{
		public List<(GeneSet, GeneSet, float[])> GuidedEvolution { get; set; } = new List<(GeneSet, GeneSet, float[])>();

		public List<(GeneSet, GeneSet)> Crossover { get; set; } = new List<(GeneSet, GeneSet)>();
	}

	private VersionCachedValue<GeneSet> averageSet;

	private VersionCachedValue<SelectionOpportunities> selectionOps;

	private GeneSet Initializer { get; set; }

	public SelectionPooling(GenePool pool, SelectionConfiguration settings)
		: base(pool, settings)
	{
		SelectionPooling selectionPooling = this;
		averageSet = pool.EpochCachedValue(() => selectionPooling.CreateWeightedAverageSet(pool.GetGeneration(GeneGeneration.Old)));
		selectionOps = pool.EpochCachedValue(CreateOpportunities);
	}

	public override GeneSet[] GetSpecialDumpVectors()
	{
		GeneSet copy = base.Pool.CopyGeneSet(averageSet.Value);
		copy.Id = "wmean";
		List<GeneSet> genesets = new List<GeneSet>();
		genesets.Add(copy);
		int i = 0;
		foreach (var item in selectionOps.Value.GuidedEvolution)
		{
			GeneSet a = item.Item1;
			GeneSet b = item.Item2;
			float[] g = item.Item3;
			GeneSet gs = base.Pool.CreateGeneSet();
			g.CopyTo(gs.Vector, 0);
			gs.Id = "grad_" + i++;
			genesets.Add(gs);
		}
		return genesets.ToArray();
	}

	public override void Initialize()
	{
		Dictionary<BetterExperience.Wrappers.Pools.GeneId, BetterExperience.Wrappers.Pools.GeneInfoEx>.ValueCollection eve = base.Pool.GeneFactory.Eve.Values;
		if (base.Settings.Initializer == VectorInitializer.eve || base.Settings.Initializer == VectorInitializer.center_eve)
		{
			Initializer = base.Pool.ExtractGeneSet(eve, norate: true);
			if (base.Settings.Initializer == VectorInitializer.center_eve)
			{
				Initializer.Vector.InplaceMap((float x) => (0.5f + x) / 2f);
			}
		}
		else
		{
			Initializer = base.Pool.CreateGeneSet();
			Initializer.InitVector(GeneVector.Data, 0.5f);
		}
		for (int i = 0; i < base.Pool.Data.GeneOrder.Count; i++)
		{
			Initializer.Vector[i] = base.Pool.FineTuning.GetInitialValue(base.Pool.Data.GeneOrder[i], Initializer.Vector[i]);
		}
		Initializer.InitVector(GeneVector.Rating, 0.1f);
	}

	public override void Populate()
	{
		MoveYoungToMature();
		MoveMatureToOld();
		base.Data.DiversityPenalty = 10 - base.Pool.GetGeneration(GeneGeneration.Mature).Count;
	}

	private void MoveMatureToOld()
	{
		List<GeneSet> mgen = base.Pool.GetGeneration(GeneGeneration.Mature);
		List<GeneSet> ogen = base.Pool.GetGeneration(GeneGeneration.Old);
		bool incrementEpoch = false;
		if (mgen.Count >= base.Pool.Capacity)
		{
			ogen.AddRange(mgen);
			mgen.Clear();
			incrementEpoch = true;
		}
		if (incrementEpoch)
		{
			CompactOld();
			base.Data.Epoch++;
		}
	}

	private void CompactOld()
	{
		List<GeneSet> ogen = base.Pool.GetGeneration(GeneGeneration.Old);
		GeneSet[] alive;
		if (base.Settings.FixedSize == 0)
		{
			alive = (from x in ogen.Select(delegate(GeneSet x)
				{
					float num = x.Rating.Average();
					return (score: (float)x.Epoch + num * base.Settings.RateToLifetimeFactor, gs: x);
				})
				where x.score >= (float)base.Pool.Data.Epoch
				select x.gs).ToArray();
		}
		else
		{
			base.Pool.Logger.Info("Generating fixed set");
			List<GeneSet> tmp = (from x in ogen
				select (score: x.Rating.Average(), gs: x) into x
				orderby (score: x.score, Epoch: x.gs.Epoch) descending
				select x.gs).ToList();
			List<GeneSet> gss = new List<GeneSet>();
			for (int i = 0; i < base.Settings.FixedSize && i < tmp.Count; i++)
			{
				gss.Add(tmp[i]);
			}
			base.Pool.Logger.Info("{0} survivors", tmp.Count);
			ogen.Where((GeneSet x) => x.Epoch == base.Pool.Data.Epoch).ForEach(delegate(GeneSet x)
			{
				if (!tmp.Contains(x))
				{
					tmp.Add(x);
				}
			});
			alive = tmp.ToArray();
			base.Pool.Logger.Info("{0} final generation", tmp.Count);
		}
		ogen.Clear();
		ogen.AddRange(alive);
		if (ogen.Count > 0)
		{
			base.Pool.Data.Error = ogen.Select((GeneSet x) => x.Rating.Average()).Average() * 10f;
		}
		else
		{
			base.Pool.Data.Error = 0f;
		}
	}

	public override GenerationInfo ProduceRandomGeneSet(GeneSet gs, int step)
	{
		List<GeneSet> ogen = base.Pool.GetGeneration(GeneGeneration.Old);
		SelectionTechinque techinque = SelectTechinque(ogen);
		float weight = 1f + (float)(step / base.Settings.StepsToUpdateWeight) * base.Settings.WeightUpdateScale;
		float crossweight = (float)(step / base.Settings.StepsToUpdateWeight) * base.Settings.WeightUpdateScale;
		switch (techinque)
		{
		case SelectionTechinque.Random:
			if (base.Settings.Distribution == VectorDistribution.normal)
			{
				RandomInitializer(gs, weight);
			}
			else
			{
				RandomUniform(gs);
			}
			break;
		case SelectionTechinque.AverageRandom:
			RandomAverage(gs, weight);
			break;
		case SelectionTechinque.Morph:
			Morph(gs, weight);
			break;
		case SelectionTechinque.Crossover:
			Crossover(gs, crossweight);
			break;
		case SelectionTechinque.MorphCrossover:
			Crossover(gs, weight);
			break;
		case SelectionTechinque.GuidedEvolution:
			GuidedEvolution(gs, weight);
			break;
		default:
			throw new NotImplementedException();
		}
		return new GenerationInfo
		{
			Text = "Technique " + techinque.ToString() + " weight " + weight
		};
	}

	private void Crossover(GeneSet target, float weight)
	{
		(GeneSet, GeneSet) pair = base.Pool.Random.ListChoice(selectionOps.Value.Crossover);
		Crossover(target, pair.Item1, pair.Item2);
		Randomize(target, target, weight);
	}

	private void GuidedEvolution(GeneSet target, float weight)
	{
		(GeneSet, GeneSet, float[]) pair = base.Pool.Random.ListChoice(selectionOps.Value.GuidedEvolution);
		Randomize(target, pair.Item1, weight, pair.Item3);
	}

	private void Crossover(GeneSet target, GeneSet a, GeneSet b)
	{
		int c = base.Pool.Data.GeneOrder.Count;
		int x = c / 4;
		int count = base.Pool.Random.NextInt(c / 2);
		int splitSite = x + count;
		target.InitVector(GeneVector.Rating, 0f);
		int[] order = Enumerable.Range(0, c).ToArray();
		order.InplaceShuffle(base.Pool.Random.NextInt);
		for (int j = 0; j < target.Vector.Length; j++)
		{
			int i = order[j];
			float value = ((i < splitSite) ? a.Vector[i] : b.Vector[i]);
			float rating = ((i < splitSite) ? a.Rating[i] : b.Rating[i]);
			base.Pool.SetGeneValue(target, base.Pool.Data.GeneOrder[i], value, nosymmetry: false, guidance: true, base.Settings.Step);
			target.Rating[i] = rating;
		}
	}

	private void Morph(GeneSet gs, float weight)
	{
		Randomize(gs, base.Pool.Random.ListChoice(base.Pool.GetGeneration(GeneGeneration.Old)), weight);
	}

	private void RandomInitializer(GeneSet gs, float weight)
	{
		Randomize(gs, Initializer, weight);
	}

	private void RandomAverage(GeneSet gs, float weight)
	{
		Randomize(gs, averageSet.Value, weight);
	}

	private void Randomize(GeneSet target, GeneSet source, float weight, float[] direction = null)
	{
		float[] momentum = base.Pool.CreateMomentumLock(direction);
		for (int i = 0; i < target.Vector.Length; i++)
		{
			float specificWeight = base.Pool.FineTuning.GetDeviationFactorTuning(base.Pool.Data.GeneOrder[i], 1f);
			float rate = source.Rating[i] - 0.1f;
			float sigma = Mathf.Lerp(base.Settings.Sigma, base.Settings.FragileSigma, rate);
			float value = base.Pool.Random.NextNormalStdDev(source.Vector[i], sigma * weight * specificWeight);
			if (direction != null && direction[i] != 0f && !float.IsNaN(direction[i]))
			{
				float targetUpdate = Mathf.Abs(value - source.Vector[i]) * (float)Math.Sign(direction[i]);
				value = source.Vector[i] + targetUpdate;
			}
			if (momentum[i] != 0f && !float.IsNaN(momentum[i]))
			{
				value = source.Vector[i] + Mathf.Abs(value - source.Vector[i]) * momentum[i];
			}
			base.Pool.SetGeneValue(target, base.Pool.Data.GeneOrder[i], value, nosymmetry: false, guidance: true, base.Settings.Step);
		}
	}

	private void RandomUniform(GeneSet target)
	{
		for (int i = 0; i < target.Vector.Length; i++)
		{
			float value = base.Pool.Random.NextFloat(1f);
			base.Pool.SetGeneValue(target, base.Pool.Data.GeneOrder[i], value, nosymmetry: false, guidance: true, base.Settings.Step);
		}
	}

	private SelectionTechinque SelectTechinque(List<GeneSet> ogen)
	{
		if (ogen.Count > 1)
		{
			if (selectionOps.Value.Crossover.Count > 0 && selectionOps.Value.GuidedEvolution.Count > 0)
			{
				if (base.Settings.AlwaysUseMorph)
				{
					return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Morph, SelectionTechinque.Crossover, SelectionTechinque.MorphCrossover, SelectionTechinque.GuidedEvolution);
				}
				return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Crossover, SelectionTechinque.MorphCrossover, SelectionTechinque.GuidedEvolution);
			}
			if (selectionOps.Value.Crossover.Count > 0)
			{
				if (base.Settings.AlwaysUseMorph)
				{
					return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Morph, SelectionTechinque.Crossover, SelectionTechinque.MorphCrossover, SelectionTechinque.AverageRandom);
				}
				return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Crossover, SelectionTechinque.MorphCrossover, SelectionTechinque.AverageRandom);
			}
			if (selectionOps.Value.GuidedEvolution.Count > 0)
			{
				if (base.Settings.AlwaysUseMorph)
				{
					return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Morph, SelectionTechinque.GuidedEvolution, SelectionTechinque.GuidedEvolution);
				}
				return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.GuidedEvolution);
			}
			return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Morph, SelectionTechinque.AverageRandom);
		}
		if (ogen.Count == 1)
		{
			if (base.Settings.Distribution == VectorDistribution.normal)
			{
				return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.AverageRandom, SelectionTechinque.Morph);
			}
			return base.Pool.Random.Choice<SelectionTechinque>(SelectionTechinque.Random, SelectionTechinque.Morph);
		}
		return SelectionTechinque.Random;
	}

	private SelectionOpportunities CreateOpportunities()
	{
		SelectionOpportunities opportunities = new SelectionOpportunities();
		List<GeneSet> ogen = base.Pool.GetGeneration(GeneGeneration.Old);
		List<(float, GeneSet)> rateAndGene = ogen.Select((GeneSet x) => (rating: x.Rating.Average(), gs: x)).ToList();
		if (base.Settings.CumulativeGradient)
		{
			opportunities.GuidedEvolution = CreateCumulativeGradient(rateAndGene);
		}
		else
		{
			opportunities.GuidedEvolution = CreateIndividualGradient(rateAndGene);
		}
		opportunities.Crossover = CreateCrossoverOpportunities(rateAndGene);
		return opportunities;
	}

	private List<(GeneSet, GeneSet)> CreateCrossoverOpportunities(List<(float rating, GeneSet gs)> rateAndGene)
	{
		List<(GeneSet, GeneSet)> tmp = new List<(GeneSet, GeneSet)>();
		for (int i = 0; i < rateAndGene.Count; i++)
		{
			(float, GeneSet) gs1 = rateAndGene[i];
			float r1 = gs1.Item1;
			for (int j = i + 1; j < rateAndGene.Count; j++)
			{
				(float, GeneSet) gs2 = rateAndGene[j];
				var (r2, _) = gs2;
				if (r1 >= base.Settings.CrossoverThreshold && r2 >= base.Settings.CrossoverThreshold)
				{
					tmp.Add((gs1.Item2, gs2.Item2));
				}
			}
		}
		return tmp;
	}

	private List<(GeneSet, GeneSet, float[])> CreateIndividualGradient(List<(float rating, GeneSet gs)> rateAndGene)
	{
		List<(GeneSet, GeneSet, float[])> tmp = new List<(GeneSet, GeneSet, float[])>();
		foreach (var gs1 in rateAndGene)
		{
			float[] averageGradient = averageSet.Value.Vector.Subtract(gs1.gs.Vector).InplaceMap((int i, float x) => (!TestPerception(x, averageSet.Value.Vector[i])) ? 0f : x);
			var (r1, _) = gs1;
			foreach (var gs2 in rateAndGene)
			{
				float r2 = gs2.rating;
				if (!(r2 > r1) || base.Settings.CumulativeGradient)
				{
					continue;
				}
				float[] gradient = gs2.gs.Vector.Subtract(gs1.gs.Vector);
				gradient.InplaceMap((int index, float x) => (Math.Sign(x) != Math.Sign(averageGradient[index])) ? ((averageGradient[index] + x) / 2f) : averageGradient[index]);
				gradient.InplaceMap((int i, float x) => (!TestPerception(x, gs2.gs.Vector[i])) ? 0f : x);
				int grads = gradient.Where((float x) => x != 0f).Count();
				if (grads > 0)
				{
					tmp.Add((gs1.gs, gs2.gs, gradient));
					if (r2 < 1f)
					{
						tmp.Add((gs2.gs, gs1.gs, gradient));
					}
				}
			}
		}
		FixGradientEstimation(tmp);
		return FilterGradientsByThreshold(tmp);
	}

	private List<(GeneSet, GeneSet, float[])> CreateCumulativeGradient(List<(float rating, GeneSet gs)> rateAndGene)
	{
		List<(GeneSet, GeneSet, float[])> result = new List<(GeneSet, GeneSet, float[])>();
		foreach (var gsr in rateAndGene)
		{
			(GeneSet, float[]) rgrad = CreateCumulativeGradient(gsr, rateAndGene);
			if (rgrad.Item2 != null)
			{
				result.Add((rgrad.Item1, rgrad.Item1, rgrad.Item2));
			}
		}
		return result;
	}

	private (GeneSet, float[]) CreateCumulativeGradient((float rating, GeneSet gs) gsAndRating, List<(float rating, GeneSet gs)> ratedPool)
	{
		GeneSet gs = gsAndRating.gs;
		float rating = gsAndRating.rating;
		List<GeneSet> below = (from x in ratedPool
			where x.rating < rating
			select x.gs).ToList();
		List<GeneSet> above = (from x in ratedPool
			where x.rating > rating
			select x.gs).ToList();
		if (below.Count == 0 && above.Count == 0)
		{
			return (gs, null);
		}
		if (below.Count == 0)
		{
			GeneSet betterset = CreateWeightedAverageSet(above);
			float[] grad = betterset.Vector.Subtract(gs.Vector);
			grad.InplaceMap((int num, float x) => (!TestPerception(x, betterset.Vector[num])) ? 0f : x);
			return (gs, grad);
		}
		if (above.Count == 0)
		{
			GeneSet worseset = CreateWeightedAverageSet(below);
			float[] grad2 = gs.Vector.Subtract(worseset.Vector);
			grad2.InplaceMap((int num, float x) => (!TestPerception(x, gs.Vector[num])) ? 0f : x);
			return (gs, grad2);
		}
		GeneSet betterset2 = CreateWeightedAverageSet(above);
		GeneSet worseset2 = CreateWeightedAverageSet(below);
		float[] bettergrad = betterset2.Vector.Subtract(gs.Vector);
		bettergrad.InplaceMap((int num, float x) => (!TestPerception(x, betterset2.Vector[num])) ? 0f : x);
		float[] worsegrad = gs.Vector.Subtract(worseset2.Vector);
		worsegrad.InplaceMap((int num, float x) => (!TestPerception(x, gs.Vector[num])) ? 0f : x);
		float[] rgrad = new float[worsegrad.Length];
		for (int i = 0; i < rgrad.Length; i++)
		{
			float worseWeight = gs.Rating[i] - worseset2.Rating[i];
			float betterWeight = betterset2.Rating[i];
			rgrad[i] = (worsegrad[i] * worseWeight + bettergrad[i] * betterWeight) / (worseWeight + betterWeight);
			if (float.IsNaN(rgrad[i]))
			{
				base.Pool.Logger.Error("NAN!! {0} {1} {2} {3}", worsegrad[i], bettergrad[i], worseWeight, betterWeight);
			}
		}
		return (gs, rgrad);
	}

	private List<(GeneSet, GeneSet, float[])> FilterGradientsByThreshold(List<(GeneSet, GeneSet, float[])> input)
	{
		List<(GeneSet, GeneSet, float[])> accepted = new List<(GeneSet, GeneSet, float[])>();
		foreach (var op in input)
		{
			bool accept = true;
			foreach (var acceptedOp in accepted)
			{
				if (acceptedOp.Item1 == op.Item1)
				{
					float[] acceptedGrad = acceptedOp.Item3;
					float[] testGrad = op.Item3;
					float sum = acceptedGrad.Subtract(testGrad).InplaceMap((float x) => (!TestPerception(x, 0.5f)) ? 0f : x).InplaceMap(Mathf.Abs)
						.Sum();
					if (sum == 0f)
					{
						accept = false;
						break;
					}
				}
			}
			if (accept)
			{
				accepted.Add(op);
			}
		}
		return accepted;
	}

	private void FixGradientEstimation(List<(GeneSet, GeneSet, float[])> input)
	{
		Dictionary<GeneSet, float[]> changes = new Dictionary<GeneSet, float[]>();
		foreach (var item in input)
		{
			GeneSet target = item.Item1;
			GeneSet b = item.Item2;
			float[] gradient = item.Item3;
			float[] counts = changes.GetValueOrAdd(target, () => new float[gradient.Length]);
			for (int i = 0; i < gradient.Length; i++)
			{
				if (gradient[i] != 0f)
				{
					counts[i] += 1f;
				}
			}
		}
		foreach (float[] change in changes.Values)
		{
			float avg = change.Average();
			change.InplaceMap((float x) => (x >= avg) ? 1 : 0);
		}
		foreach (var item2 in input)
		{
			GeneSet target2 = item2.Item1;
			GeneSet b2 = item2.Item2;
			float[] gradient2 = item2.Item3;
			float[] patch = changes[target2];
			gradient2.IMul(patch);
		}
	}

	private bool TestPerception(float x, float target)
	{
		if (!(Mathf.Abs(x) >= base.Settings.PerceptionThreshold))
		{
			return Mathf.Min(target, 1f - target) <= base.Settings.PerceptionThreshold;
		}
		return true;
	}

	protected void MoveYoungToMature()
	{
		List<GeneSet> young = base.Pool.GetGeneration(GeneGeneration.Young);
		List<GeneSet> mature = base.Pool.GetGeneration(GeneGeneration.Mature);
		foreach (GeneSet gs in young)
		{
			if (base.Data.Survivors.Contains(gs.Id))
			{
				mature.Add(gs);
			}
		}
		young.Clear();
	}

	private GeneSet CreateWeightedAverageSet(List<GeneSet> subset)
	{
		float[] weightedAverage = new float[base.Pool.Data.GeneOrder.Count];
		float[] ratingAverage = new float[base.Pool.Data.GeneOrder.Count];
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
					float rate = gs.Rating[i];
					weightedSum += gs.Vector[i] * rate;
					totalWeight += rate;
					marks++;
				}
				j++;
			}
			if (totalWeight > 0f)
			{
				weightedAverage[i] = weightedSum / totalWeight;
				ratingAverage[i] = totalWeight / (float)marks;
			}
		}
		GeneSet result = base.Pool.CreateGeneSet();
		for (int k = 0; k < weightedAverage.Length; k++)
		{
			base.Pool.SetGeneValue(result, base.Pool.Data.GeneOrder[k], weightedAverage[k], nosymmetry: false, guidance: true, base.Settings.Step);
		}
		result.Vectors[GeneVector.Data] = weightedAverage;
		result.Vectors[GeneVector.Rating] = ratingAverage;
		return result;
	}
}
