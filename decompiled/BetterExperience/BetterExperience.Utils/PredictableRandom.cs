using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterExperience.Utils;

internal class PredictableRandom
{
	private System.Random rnd = new System.Random();

	public int Seed { get; private set; }

	public PredictableRandom(int seed)
	{
		Seed = seed;
	}

	private System.Random NextRandom()
	{
		return rnd;
	}

	public float NextFloat(float max)
	{
		return (float)NextRandom().NextDouble() * max;
	}

	public int NextInt(int max)
	{
		return NextRandom().Next(max);
	}

	public bool NextBool()
	{
		return NextRandom().Next(2) == 0;
	}

	public T ListChoice<T>(IList<T> set)
	{
		return set[NextInt(set.Count)];
	}

	public T Choice<T>(params T[] set)
	{
		return set[NextInt(set.Length)];
	}

	public float NextNormalStdDev(float mean, float stddev)
	{
		return BoxMuller32(mean, stddev);
	}

	private double BoxMuller64(double mean, double stddev)
	{
		double u1 = 1.0 - NextRandom().NextDouble();
		double u2 = 1.0 - NextRandom().NextDouble();
		double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(Math.PI * 2.0 * u2);
		double randNormal = mean + stddev * randStdNormal;
		return (float)randNormal;
	}

	private float BoxMuller32(float mean, float stddev)
	{
		float u1 = 1f - NextFloat(1f);
		float u2 = 1f - NextFloat(1f);
		float randStdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin((float)Math.PI * 2f * u2);
		return mean + stddev * randStdNormal;
	}

	public float NextNormalRange(float a, float b)
	{
		float mean = (a + b) / 2f;
		float space = Mathf.Abs(b - a);
		float stddev = space / 4f;
		return NextNormalStdDev(mean, stddev);
	}
}
