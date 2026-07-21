using System;
using System.Collections.Generic;

namespace BetterExperience.CustomScene.Packaging;

public interface VirtIO
{
	Package Package { get; set; }

	IEnumerable<VirtIOEntry> Enumerate();

	List<string> ListFiles();

	List<string> ListDirs();

	VirtIO Dir(string name);

	byte[] Read(string name);

	void Write(string name, byte[] data);

	void DeleteOrUpdateFile(string name, byte[] data);

	T Persisted<T>(Func<T> factory, string name);

	void Persist<T>(T value, string name);

	void PersistOrDelete<T>(T value, string name);
}
