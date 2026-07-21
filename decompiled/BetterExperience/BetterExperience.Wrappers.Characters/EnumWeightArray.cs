using System;

namespace BetterExperience.Wrappers.Characters;

public class EnumWeightArray<T> where T : Enum
{
	private float[] weights;

	private T[] keys;

	public float this[int n]
	{
		get
		{
			return weights[n];
		}
		set
		{
			weights[n] = value;
		}
	}

	public float this[T n]
	{
		get
		{
			return weights[(int)(object)n];
		}
		set
		{
			weights[(int)(object)n] = value;
		}
	}

	public T[] Keys => keys;

	public EnumWeightArray()
	{
		keys = (T[])Enum.GetValues(typeof(T));
		weights = new float[keys.Length];
	}

	public T MaxKey()
	{
		T k = Keys[0];
		float m = weights[0];
		T[] array = Keys;
		foreach (T i2 in array)
		{
			float t = this[i2];
			if (t > m)
			{
				m = t;
				k = i2;
			}
		}
		return k;
	}

	public void Clear()
	{
		for (int i = 0; i < weights.Length; i++)
		{
			weights[i] = 0f;
		}
	}
}
