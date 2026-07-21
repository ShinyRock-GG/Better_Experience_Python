using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Packaging;

internal class ZipIO : CachedIO
{
	private class ZipFileVirtIO : VirtIO
	{
		private ZipArchiveEntry entry;

		private string location;

		public Package Package { get; set; }

		public ZipFileVirtIO(ZipArchiveEntry e, string location)
		{
			entry = e;
			this.location = location;
		}

		public void DeleteOrUpdateFile(string name, byte[] data)
		{
			throw new NotImplementedException();
		}

		public VirtIO Dir(string name)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<VirtIOEntry> Enumerate()
		{
			throw new NotImplementedException();
		}

		public List<string> ListDirs()
		{
			throw new NotImplementedException();
		}

		public List<string> ListFiles()
		{
			throw new NotImplementedException();
		}

		public void Persist<T>(T value, string name)
		{
			throw new NotImplementedException();
		}

		public T Persisted<T>(Func<T> factory, string name)
		{
			byte[] bytes = Read(name);
			if (bytes != null)
			{
				string text = Encoding.UTF8.GetString(bytes);
				return GlobalPersistenceService.Deserialize<T>(text);
			}
			return factory();
		}

		public void PersistOrDelete<T>(T value, string name)
		{
			throw new NotImplementedException();
		}

		public byte[] Read(string name)
		{
			lock (entry.Archive)
			{
				using Stream s = entry.Open();
				using MemoryStream ms = new MemoryStream();
				s.CopyTo(ms);
				return ms.ToArray();
			}
		}

		public void Write(string name, byte[] data)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			string path = "VirtIO:zipfs:" + location + ":" + entry.FullName.Substring(0, entry.FullName.Length - entry.Name.Length);
			if (path.EndsWith("/"))
			{
				path = path.Substring(0, path.Length - 1);
			}
			return path;
		}
	}

	private ZipArchive zipFile;

	private string location;

	public override Package Package
	{
		get
		{
			return base.Package;
		}
		set
		{
			base.Package = value;
			foreach (VirtIOEntry vio in Enumerate())
			{
				foreach (VirtIOAccessor a in vio.Accessors)
				{
					if (a.IO is ZipFileVirtIO z)
					{
						z.Package = value;
					}
				}
				if (vio.WriteAccessor != null && vio.WriteAccessor.IO is ZipFileVirtIO z2)
				{
					z2.Package = value;
				}
			}
		}
	}

	public ZipIO(ZipArchive zipFile, string location)
		: base(CreateZipEntries(zipFile, location))
	{
		this.zipFile = zipFile;
		this.location = location;
	}

	private static Dictionary<string, VirtIOEntry> CreateZipEntries(ZipArchive archive, string location)
	{
		Dictionary<string, VirtIOEntry> result = new Dictionary<string, VirtIOEntry>();
		foreach (ZipArchiveEntry e in archive.Entries)
		{
			VirtIOEntry entry = new VirtIOEntry();
			entry.Name = e.Name;
			char directorySeparatorChar = Path.DirectorySeparatorChar;
			entry.Path = directorySeparatorChar + e.FullName.Substring(0, e.FullName.Length - e.Name.Length).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
			entry.Accessors.Add(new VirtIOAccessor(new ZipFileVirtIO(e, location), e.Name));
			result.Add(e.FullName, entry);
		}
		return result;
	}

	public override T Persisted<T>(Func<T> factory, string name)
	{
		ZipArchiveEntry entry = zipFile.GetEntry(name);
		if (entry == null)
		{
			Logger.Global.Error("Missing zip entry {0}", name);
			return factory();
		}
		using MemoryStream ms = new MemoryStream();
		using Stream zs = entry.Open();
		zs.CopyTo(ms);
		byte[] bytes = ms.ToArray();
		string text = Encoding.UTF8.GetString(bytes);
		return GlobalPersistenceService.Deserialize<T>(text);
	}

	public override string ToString()
	{
		return "VirtIO:zipfs:" + location;
	}
}
