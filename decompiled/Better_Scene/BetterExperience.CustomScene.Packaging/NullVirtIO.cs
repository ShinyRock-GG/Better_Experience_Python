using System;
using System.Collections.Generic;

namespace BetterExperience.CustomScene.Packaging;

internal class NullVirtIO : VirtIO
{
	public static readonly List<string> EMPTY_LIST = new List<string>();

	public Package Package { get; set; }

	public void DeleteOrUpdateFile(string name, byte[] data)
	{
	}

	public VirtIO Dir(string name)
	{
		return this;
	}

	public List<string> ListDirs()
	{
		return EMPTY_LIST;
	}

	public List<string> ListFiles()
	{
		return EMPTY_LIST;
	}

	public void Persist<T>(T value, string name)
	{
		throw new InvalidOperationException();
	}

	public T Persisted<T>(Func<T> factory, string name)
	{
		throw new InvalidOperationException();
	}

	public void PersistOrDelete<T>(T value, string name)
	{
		throw new NotImplementedException();
	}

	public byte[] Read(string name)
	{
		throw new InvalidOperationException();
	}

	public IEnumerable<VirtIOEntry> Enumerate()
	{
		throw new NotImplementedException();
	}

	public void Write(string name, byte[] data)
	{
		throw new InvalidOperationException();
	}

	public override string ToString()
	{
		return "VirtIO:null";
	}
}
