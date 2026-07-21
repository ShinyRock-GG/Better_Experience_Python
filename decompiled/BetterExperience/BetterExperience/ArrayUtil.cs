using System;
using System.Collections.Generic;

namespace BetterExperience;

internal class ArrayUtil
{
	public static float[] TransformVectors(float[] a, float[] b, Func<float, float, float> transform)
	{
		float[] r = new float[Math.Min(a.Length, b.Length)];
		for (int i = 0; i < r.Length; i++)
		{
			r[i] = transform(a[i], b[i]);
		}
		return r;
	}

	public static int[] SelectIndex(float[] diff, Func<float, bool> predicate)
	{
		List<int> result = new List<int>();
		for (int i = 0; i < diff.Length; i++)
		{
			if (predicate(diff[i]))
			{
				result.Add(i);
			}
		}
		return result.ToArray();
	}
}
