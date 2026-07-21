using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterExperience.Utils;

public static class Extensions
{
	public static V GetValueOrAdd<K, V>(this IDictionary<K, V> dic, K key, Func<V> producer)
	{
		if (!dic.TryGetValue(key, out var value))
		{
			value = (dic[key] = producer());
		}
		return value;
	}

	public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dic, K key, V def)
	{
		if (!dic.TryGetValue(key, out var value))
		{
			return def;
		}
		return value;
	}

	public static V GetValueOrDefault<K, V>(this IDictionary<K, V> dic, K key, Func<V> def)
	{
		if (!dic.TryGetValue(key, out var value))
		{
			return def();
		}
		return value;
	}

	public static bool ContentEquals<K, V>(this IDictionary<K, V> dic, IDictionary<K, V> other)
	{
		if (object.Equals(dic, other))
		{
			return true;
		}
		if (dic == null || other == null)
		{
			return false;
		}
		List<K> k1 = (from result in dic.Keys.ToList()
			orderby result
			select result).ToList();
		List<K> k2 = (from result in other.Keys.ToList()
			orderby result
			select result).ToList();
		if (!k1.ContentEquals(k2))
		{
			return false;
		}
		foreach (K k3 in dic.Keys)
		{
			dic.TryGetValue(k3, out var v1);
			other.TryGetValue(k3, out var v2);
			if (!object.Equals(v1, v2))
			{
				return false;
			}
		}
		return true;
	}

	public static void ForEach<K, V>(this IDictionary<K, V> dic, Action<K, V> callback)
	{
		foreach (KeyValuePair<K, V> kv in dic)
		{
			callback(kv.Key, kv.Value);
		}
	}

	public static void AddOrReplace<K, V>(this IDictionary<K, V> dic, K key, V value)
	{
		dic.Remove(key);
		dic.Add(key, value);
	}

	public static T ElementAtOrDefault<T>(this IList<T> col, int index, T def)
	{
		if (col.Count > index)
		{
			return col[index];
		}
		return def;
	}

	public static bool ContentEquals<T>(this ICollection<T> first, ICollection<T> second)
	{
		if (object.Equals(first, second))
		{
			return true;
		}
		if (first == null || second == null)
		{
			return false;
		}
		if (first.Count != second.Count)
		{
			return false;
		}
		if (first.Count == 0)
		{
			return true;
		}
		return first.SequenceEqual(second);
	}

	public static void ForEach<T>(this ICollection<T> col, Action<T> callback)
	{
		foreach (T t in col)
		{
			callback(t);
		}
	}

	public static void RemoveIf<T>(this IList<T> col, Func<T, bool> test)
	{
		for (int i = 0; i < col.Count; i++)
		{
			if (test(col[i]))
			{
				col.RemoveAt(i);
				i--;
			}
		}
	}

	public static string Join(this string[] str, string separator)
	{
		return string.Join(separator, str);
	}

	public static T[] InplaceMap<T>(this T[] arr, Func<T, T> transformer)
	{
		for (int i = 0; i < arr.Length; i++)
		{
			arr[i] = transformer(arr[i]);
		}
		return arr;
	}

	public static T[] InplaceMap<T>(this T[] arr, Func<int, T, T> transformer)
	{
		for (int i = 0; i < arr.Length; i++)
		{
			arr[i] = transformer(i, arr[i]);
		}
		return arr;
	}

	public static T[] InplaceShuffle<T>(this T[] order, Func<int, int> randomizer)
	{
		for (int i = 0; i < order.Length; i++)
		{
			int range = order.Length - i;
			int offset = randomizer(range);
			T a = order[i];
			T b = order[i + offset];
			order[i] = b;
			order[i + offset] = a;
		}
		return order;
	}

	public static T[] Fill<T>(this T[] array, T value)
	{
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = value;
		}
		return array;
	}

	public static T[] Fill<T>(this T[] array, Func<T> value)
	{
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = value();
		}
		return array;
	}

	public static T[,] Fill<T>(this T[,] array, T value)
	{
		for (int i = 0; i < array.GetLength(0); i++)
		{
			for (int j = 0; j < array.GetLength(1); j++)
			{
				array[i, j] = value;
			}
		}
		return array;
	}

	public static IList<T> InplaceShuffle<T>(this IList<T> order, Func<int, int> randomizer)
	{
		for (int i = 0; i < order.Count; i++)
		{
			int range = order.Count - i;
			int offset = randomizer(range);
			T a = order[i];
			T b = order[i + offset];
			order[i] = b;
			order[i + offset] = a;
		}
		return order;
	}

	public static IList<T> BucketShuffle<T, K>(this IList<T> list, Func<int, int> randomizer, Func<T, K> bucket) where K : IComparable
	{
		IList<T> order = list.ToList().InplaceShuffle(randomizer);
		return list.OrderBy(bucket).ThenBy((T x) => order.IndexOf(x)).ToList();
	}

	public static float[] Subtract(this float[] a, float[] b)
	{
		float[] result = new float[Math.Min(a.Length, b.Length)];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = a[i] - b[i];
		}
		return result;
	}

	public static float[] Add(this float[] a, float[] b)
	{
		float[] result = new float[Math.Min(a.Length, b.Length)];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = a[i] + b[i];
		}
		return result;
	}

	public static float[][] Add(this float[][] a, float[][] b)
	{
		float[][] result = new float[Math.Min(a.Length, b.Length)][];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = a[i].Add(b[i]);
		}
		return result;
	}

	public static float[][] IAdd(this float[][] a, float[][] b)
	{
		for (int i = 0; i < a.Length; i++)
		{
			for (int j = 0; j < a[i].Length; j++)
			{
				a[i][j] += b[i][j];
			}
		}
		return a;
	}

	public static float[][] IDiv(this float[][] a, float b)
	{
		for (int i = 0; i < a.Length; i++)
		{
			for (int j = 0; j < a[i].Length; j++)
			{
				a[i][j] /= b;
			}
		}
		return a;
	}

	public static float[][,] IAdd(this float[][,] a, float[][,] b)
	{
		for (int i = 0; i < a.Length; i++)
		{
			for (int j = 0; j < a[i].GetLength(0); j++)
			{
				for (int k = 0; k < a[i].GetLength(1); k++)
				{
					a[i][j, k] += b[i][j, k];
				}
			}
		}
		return a;
	}

	public static float[][,] IDiv(this float[][,] a, float b)
	{
		for (int i = 0; i < a.Length; i++)
		{
			for (int j = 0; j < a[i].GetLength(0); j++)
			{
				for (int k = 0; k < a[i].GetLength(1); k++)
				{
					a[i][j, k] /= b;
				}
			}
		}
		return a;
	}

	public static float[] Normalized(this float[] a)
	{
		if (a.Length == 0)
		{
			return new float[0];
		}
		float t = 0f;
		for (int i = 0; i < a.Length; i++)
		{
			t += a[i] * a[i];
		}
		float magnitude = Mathf.Sqrt(t);
		float[] r = new float[a.Length];
		for (int j = 0; j < r.Length; j++)
		{
			r[j] = a[j] / magnitude;
		}
		return r;
	}

	public static float[] Mul(this float[] a, float scalar)
	{
		float[] r = new float[a.Length];
		for (int i = 0; i < a.Length; i++)
		{
			r[i] = a[i] * scalar;
		}
		return r;
	}

	public static float[] Mul(this float[] a, float[] b)
	{
		float[] r = new float[Math.Min(a.Length, b.Length)];
		for (int i = 0; i < r.Length; i++)
		{
			r[i] = a[i] * b[i];
		}
		return r;
	}

	public static float[] IMul(this float[] a, float[] b)
	{
		int k = Math.Min(a.Length, b.Length);
		for (int i = 0; i < k; i++)
		{
			a[i] *= b[i];
		}
		return a;
	}

	public static float SquareMagnitude(this float[] a)
	{
		if (a.Length == 0)
		{
			return 0f;
		}
		float sum = 0f;
		for (int i = 0; i < a.Length; i++)
		{
			sum += a[i] * a[i];
		}
		return sum;
	}

	public static float[] AsFloatArray(this Vector3 a)
	{
		return new float[3] { a.x, a.y, a.z };
	}

	public static Vector3 AsVector3(this float[] a)
	{
		if (a.Length == 3)
		{
			return new Vector3(a[0], a[1], a[2]);
		}
		return Vector3.zero;
	}

	public static float[] AsFloatArray(this Quaternion a)
	{
		return new float[4] { a.x, a.y, a.z, a.w };
	}

	public static Quaternion AsQuaternion(this float[] a)
	{
		if (a.Length == 4)
		{
			return new Quaternion(a[0], a[1], a[2], a[3]);
		}
		return Quaternion.identity;
	}

	public static void AddRange<K, T>(this IDictionary<K, T> dict, IEnumerable<KeyValuePair<K, T>> other)
	{
		other.ForEach(delegate(KeyValuePair<K, T> kv)
		{
			dict.Add(kv.Key, kv.Value);
		});
	}

	public static string TrimToNull(this string value)
	{
		if (value == null)
		{
			return null;
		}
		value = value.Trim();
		if (!(value == ""))
		{
			return value;
		}
		return null;
	}
}
