using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Packaging;

internal class CachedIO : VirtIO
{
	private Dictionary<string, VirtIOEntry> entries = new Dictionary<string, VirtIOEntry>();

	private VirtIO writebleIO;

	private IReadOnlyList<VirtIO> ios;

	public virtual Package Package { get; set; }

	public CachedIO(IReadOnlyList<VirtIO> ios, VirtIO wrIO)
	{
		writebleIO = wrIO;
		this.ios = ios;
		foreach (VirtIO io in ios)
		{
			Merge(io);
		}
	}

	public CachedIO(Dictionary<string, VirtIOEntry> dict)
	{
		dict.ForEach(delegate(KeyValuePair<string, VirtIOEntry> kv)
		{
			entries.Add(kv.Key, kv.Value);
		});
	}

	private void Merge(VirtIO io)
	{
		foreach (VirtIOEntry e in io.Enumerate())
		{
			string key = Path.Combine(e.Path, e.Name);
			if (!entries.TryGetValue(key, out var entry))
			{
				entry = (entries[key] = new VirtIOEntry());
				entry.Path = e.Path;
				entry.Name = e.Name;
			}
			entry.Accessors.AddRange(e.Accessors);
		}
		if (writebleIO != null)
		{
			foreach (VirtIOEntry e2 in entries.Values)
			{
				e2.WriteAccessor = new VirtIOAccessor(writebleIO.Dir(e2.Path), e2.Name);
			}
			return;
		}
		Logger.Global.Error("NO WRIO");
	}

	public void DeleteOrUpdateFile(string name, byte[] data)
	{
		throw new NotImplementedException();
	}

	public VirtIO Dir(string name)
	{
		return writebleIO.Dir(name);
	}

	public List<string> ListDirs()
	{
		throw new NotImplementedException();
	}

	public List<string> ListFiles()
	{
		return entries.Keys.ToList();
	}

	public void Persist<T>(T value, string name)
	{
		throw new NotImplementedException();
	}

	public virtual T Persisted<T>(Func<T> factory, string name)
	{
		byte[] bytes = Read(name);
		if (bytes == null)
		{
			return factory();
		}
		string text = Encoding.UTF8.GetString(bytes);
		return GlobalPersistenceService.Deserialize<T>(text);
	}

	public void PersistOrDelete<T>(T value, string name)
	{
		throw new NotImplementedException();
	}

	public byte[] Read(string name)
	{
		if (entries.TryGetValue(name, out var e))
		{
			return e.Accessors.Last().Read();
		}
		return null;
	}

	public void Write(string name, byte[] data)
	{
		throw new NotImplementedException();
	}

	public IEnumerable<VirtIOEntry> Enumerate()
	{
		return entries.Values;
	}

	public override string ToString()
	{
		if (writebleIO != null)
		{
			return "aggregate:" + writebleIO;
		}
		if (ios.Count > 0)
		{
			return "aggregate:" + ios.Last().ToString();
		}
		return "aggregate:null";
	}
}
