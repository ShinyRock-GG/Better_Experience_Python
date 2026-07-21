using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PerceptionScoring
{
	public float PerceptionScale { get; }

	public GenePool Pool { get; }

	public PerceptionScoring(float peceptionScale, GenePool pool)
	{
		PerceptionScale = peceptionScale;
		Pool = pool;
	}

	public int[] Translate(float[] input)
	{
		return input.Select((float x) => Mathf.RoundToInt(x / PerceptionScale)).ToArray();
	}

	public float Score(GeneSet geneSet, GeneSet genes)
	{
		return Score(geneSet, new GeneSet[1] { genes })[0];
	}

	public float[] Score(GeneSet geneSet, IList<GeneSet> testSet)
	{
		int[] input = Translate(geneSet.Vector);
		float[] score = new float[testSet.Count];
		for (int i = 0; i < testSet.Count; i++)
		{
			int[] test = Translate(testSet[i].Vector);
			float activations = 0f;
			float sum = 0f;
			for (int j = 0; j < input.Length; j++)
			{
				float w = GetWeight(j);
				if (input[j] - test[j] == 0)
				{
					activations += w;
				}
				sum += w;
			}
			score[i] = activations / sum;
		}
		return score;
	}

	public virtual float GetWeight(int i)
	{
		return Pool.FineTuning.GetSimilarityWeight(Pool.Data.GeneOrder[i]);
	}
}
