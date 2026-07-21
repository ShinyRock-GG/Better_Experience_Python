using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Packaging;

internal class FSVirtIo : VirtIO
{
	private DirectoryInfo dir;

	private string localPath;

	public static DirectoryInfo PACKAGES_ROOT { get; } = new DirectoryInfo("./Packages");

	public Package Package { get; set; }

	public FSVirtIo(DirectoryInfo dir, string localDir)
	{
		this.dir = dir;
		localPath = localDir;
	}

	public void DeleteOrUpdateFile(string name, byte[] data)
	{
		Logger.Global.Info("DeleteOrUpdate {0} {1}", dir.FullName, name);
		string path = Path.Combine(dir.FullName, name);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		else
		{
			Write(name, data);
		}
	}

	public VirtIO Dir(string name)
	{
		while (true)
		{
			string text = name;
			char directorySeparatorChar = Path.DirectorySeparatorChar;
			if (!text.StartsWith(directorySeparatorChar.ToString()))
			{
				break;
			}
			name = name.Substring(1);
		}
		string path = Path.Combine(dir.FullName, name);
		return new FSVirtIo(new DirectoryInfo(path), Path.Combine(localPath, name))
		{
			Package = Package
		};
	}

	public VirtIO Dir(params string[] name)
	{
		throw new NotImplementedException();
	}

	public List<string> ListDirs()
	{
		if (dir.Exists)
		{
			return (from x in dir.GetDirectories()
				select x.Name).ToList();
		}
		return NullVirtIO.EMPTY_LIST;
	}

	public List<string> ListFiles()
	{
		if (dir.Exists)
		{
			return (from x in dir.GetFiles()
				select x.Name).ToList();
		}
		return NullVirtIO.EMPTY_LIST;
	}

	public byte[] Read(string name)
	{
		if (dir.Exists)
		{
			string path = Path.Combine(dir.FullName, name);
			if (new FileInfo(path).Exists)
			{
				return File.ReadAllBytes(path);
			}
			return null;
		}
		throw new InvalidOperationException();
	}

	public void Write(string name, byte[] data)
	{
		if (!dir.Exists)
		{
			dir.Create();
		}
		string path = Path.Combine(dir.FullName, name);
		File.WriteAllBytes(path, data);
	}

	public void Persist<T>(T value, string name)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		MeasureTime mt = MeasureTime.Create(Logger.Global, (Func<long, string>)((long t) => $"{name} marshalling {t}ms"), true);
		string text = GlobalPersistenceService.Serialize(value);
		mt.Dispose();
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		Write(name, bytes);
	}

	public T Persisted<T>(Func<T> factory, string name)
	{
		if (ListFiles().Contains(name))
		{
			byte[] bytes = Read(name);
			string text = Encoding.UTF8.GetString(bytes);
			return GlobalPersistenceService.Deserialize<T>(text);
		}
		return factory();
	}

	public void PersistOrDelete<T>(T value, string name)
	{
		string text = GlobalPersistenceService.Serialize(value);
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		DeleteOrUpdateFile(name, bytes);
	}

	public IEnumerable<VirtIOEntry> Enumerate()
	{
		List<VirtIOEntry> entries = new List<VirtIOEntry>();
		foreach (string f in ListFiles())
		{
			VirtIOEntry e = new VirtIOEntry();
			e.Path = localPath;
			e.Name = f;
			e.Accessors.Add(new VirtIOAccessor(this, e.Name));
			entries.Add(e);
		}
		foreach (string d in ListDirs())
		{
			VirtIO dfs = Dir(d);
			foreach (VirtIOEntry de in dfs.Enumerate())
			{
				VirtIOEntry e2 = new VirtIOEntry();
				e2.Name = de.Name;
				e2.Path = de.Path;
				e2.Accessors.AddRange(de.Accessors);
				entries.Add(e2);
			}
		}
		return entries;
	}

	public override string ToString()
	{
		string relDir = dir.FullName.Replace(PackageManager.PackagesRoot.FullName, "");
		if (Path.DirectorySeparatorChar != '/')
		{
			relDir = relDir.Replace(Path.DirectorySeparatorChar, '/');
		}
		return "VirtIO:fs:" + relDir;
	}
}
