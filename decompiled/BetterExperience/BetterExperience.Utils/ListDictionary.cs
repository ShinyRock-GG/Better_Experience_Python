using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace BetterExperience.Utils;

public class ListDictionary<K, V> : IDictionary<K, V>, ICollection<KeyValuePair<K, V>>, IEnumerable<KeyValuePair<K, V>>, IEnumerable
{
	private List<KeyValuePair<K, V>> backingList = new List<KeyValuePair<K, V>>();

	public V this[K key]
	{
		get
		{
			if (TryGetValue(key, out var value))
			{
				return value;
			}
			throw new KeyNotFoundException();
		}
		set
		{
			Add(key, value);
		}
	}

	public ICollection<K> Keys => backingList.Select((KeyValuePair<K, V> x) => x.Key).ToList();

	public ICollection<V> Values => backingList.Select((KeyValuePair<K, V> x) => x.Value).ToList();

	public int Count => backingList.Count;

	public bool IsReadOnly => false;

	public void Add(K key, V value)
	{
		Add(new KeyValuePair<K, V>(key, value));
	}

	public void Add(KeyValuePair<K, V> item)
	{
		Remove(item.Key);
		backingList.Add(item);
	}

	public void Clear()
	{
		backingList.Clear();
	}

	public bool Contains(KeyValuePair<K, V> item)
	{
		if (TryGetValue(item.Key, out var value))
		{
			object obj = item.Value;
			if (value.Equals(obj))
			{
				return true;
			}
		}
		return false;
	}

	public bool ContainsKey(K key)
	{
		V any;
		return TryGetValue(key, out any);
	}

	public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
	{
		throw new NotImplementedException();
	}

	public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
	{
		return backingList.GetEnumerator();
	}

	public bool Remove(K key)
	{
		int toRemove = -1;
		for (int i = 0; i < backingList.Count; i++)
		{
			if (object.Equals(backingList[i].Key, key))
			{
				toRemove = i;
				break;
			}
		}
		if (toRemove >= 0)
		{
			backingList.RemoveAt(toRemove);
			return true;
		}
		return false;
	}

	public bool Remove(KeyValuePair<K, V> item)
	{
		if (TryGetValue(item.Key, out var value))
		{
			object obj = item.Value;
			if (value.Equals(obj))
			{
				Remove(item.Key);
				return true;
			}
		}
		return false;
	}

	public bool TryGetValue(K key, out V value)
	{
		foreach (KeyValuePair<K, V> kv in backingList)
		{
			if (kv.Key.Equals(key))
			{
				value = kv.Value;
				return true;
			}
		}
		value = default(V);
		return false;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return backingList.GetEnumerator();
	}
}
