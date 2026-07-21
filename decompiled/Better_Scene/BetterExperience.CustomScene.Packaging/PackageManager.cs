using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Packaging;

public class PackageManager : PluginService
{
	private class PackageLoader
	{
		private Logger logger = Logger.Create<PackageLoader>();

		private List<Package> paks = new List<Package>();

		private List<VirtIO> inmemoryPackages = new List<VirtIO>();

		private bool frozen;

		public List<Package> Result => paks;

		public void Freeze()
		{
			frozen = true;
		}

		public void AddInMemoryPackage(VirtIO io)
		{
			if (frozen)
			{
				throw new Exception("Cannot register new package after initizlization");
			}
			inmemoryPackages.Add(io);
		}

		public List<Package> LoadDiskDataAndResolve()
		{
			ReadOnDiskPackages(PackagesRoot);
			ResolvePackages();
			return Result;
		}

		private void ReadOnDiskPackages(DirectoryInfo di)
		{
			List<VirtIO> packages = new List<VirtIO>();
			DirectoryInfo[] directories = di.GetDirectories();
			foreach (DirectoryInfo dir in directories)
			{
				char directorySeparatorChar = Path.DirectorySeparatorChar;
				packages.Add(new FSVirtIo(dir, directorySeparatorChar.ToString()));
			}
			FileInfo[] files = di.GetFiles();
			foreach (FileInfo file in files)
			{
				if (file.FullName.ToLower().EndsWith(".zip"))
				{
					packages.Add(new ZipIO(new ZipArchive(new FileStream(file.FullName, FileMode.Open)), file.Name));
				}
			}
			packages.AddRange(inmemoryPackages);
			ReadManifests(packages);
			paks.Where((Package x) => x.ErrorDescription != null).ForEach(delegate(Package x)
			{
				x.ErrorDescription = null;
			});
		}

		private void ResolvePackages(bool verbose = true)
		{
			PackageResolver resolver = new PackageResolver(paks);
			foreach (Package pkg in paks)
			{
				if (!resolver.Resolve(pkg))
				{
					logger.Error("Failed to resolve package {0}: {1}", pkg.Id, pkg.ErrorDescription);
				}
				else
				{
					if (!verbose)
					{
						continue;
					}
					logger.Info("Resolved package dependency order: {0}", pkg.Id);
					logger.Info("---------");
					foreach (Package d in pkg.AllDependencies)
					{
						logger.Info("   {0} {1}:{2}", d.Manifest.type, d.Name, d.Version.ToString());
					}
					logger.Info("---------");
				}
			}
		}

		private void ReadManifests(List<VirtIO> packages)
		{
			foreach (VirtIO io in packages)
			{
				PackageManifest manifest = io.Persisted(() => (PackageManifest)null, "manifest.json");
				if (manifest == null)
				{
					logger.Error("Package without manifest {0}", io.ToString());
					continue;
				}
				if (manifest.id == null)
				{
					logger.Error("Package manifest without id {0}", io.ToString());
					continue;
				}
				if (manifest.version == null)
				{
					logger.Error("Package manifest without version {0}", io.ToString());
					continue;
				}
				if (manifest.name == null)
				{
					manifest.name = manifest.id;
				}
				logger.Info("Manifest loaded {3} ({0}) v {1} by {2}", manifest.id, manifest.version, manifest.author, manifest.name);
				try
				{
					Package pak = new Package(manifest, io);
					paks.Add(pak);
					io.Package = pak;
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Unable to create package {0}", manifest.id);
				}
			}
		}
	}

	private class PackageResolver
	{
		private List<Package> paks;

		private HashSet<Package> openSet = new HashSet<Package>();

		public PackageResolver(List<Package> paks)
		{
			this.paks = paks;
		}

		public bool Resolve(Package pkg)
		{
			if (pkg.Dependencies != null)
			{
				return true;
			}
			if (pkg.Manifest.require.Count == 0)
			{
				pkg.Dependencies = new List<Package>();
				pkg.AllDependencies = ComputeFinalDeps(pkg);
				return true;
			}
			if (!openSet.Add(pkg))
			{
				pkg.ErrorDescription = "Circluar dependency detected";
				return false;
			}
			if (pkg.Manifest.plugins.Count > 0)
			{
				foreach (KeyValuePair<string, string> p in pkg.Manifest.plugins)
				{
					string pluginGuid = p.Key;
					string ver = p.Value;
					if (!Version.TryParse(p.Value, out var version))
					{
						pkg.ErrorDescription = "Unable to parse version string: " + p.Value;
						return false;
					}
					Dictionary<string, PluginInfo> plugins = Chainloader.PluginInfos;
					if (!plugins.TryGetValue(pluginGuid, out var plugin))
					{
						pkg.ErrorDescription = "Missing required bepinex plugin: " + pluginGuid;
						return false;
					}
					if (plugin.Metadata.Version.CompareTo(version) < 0)
					{
						pkg.ErrorDescription = "Incompatible bepinex plugin version: " + pluginGuid + " " + plugin.Metadata.Version?.ToString() + " < " + ver;
						return false;
					}
				}
			}
			List<Package> dependencies = new List<Package>();
			foreach (KeyValuePair<string, string> p2 in pkg.Manifest.require)
			{
				if (!Version.TryParse(p2.Value, out var ver2))
				{
					pkg.ErrorDescription = "Unable to parse version string" + p2.Value;
					return false;
				}
				Package dep = FindPackage(p2.Key, ver2);
				if (dep == null)
				{
					pkg.ErrorDescription = "Missing required package " + p2.Key + ":" + ver2;
					return false;
				}
				if (dep.ErrorDescription != null)
				{
					pkg.ErrorDescription = "Required package " + p2.Key + " has errors: " + dep.ErrorDescription;
					return false;
				}
				if (!Resolve(dep))
				{
					pkg.ErrorDescription = "Unable to resolve package dependency " + p2.Key;
					return false;
				}
				dependencies.Add(dep);
			}
			pkg.Dependencies = dependencies;
			pkg.AllDependencies = ComputeFinalDeps(pkg);
			pkg.ErrorDescription = null;
			openSet.Remove(pkg);
			return true;
		}

		private List<Package> ComputeFinalDeps(Package pkg)
		{
			List<Package> result = new List<Package>();
			Queue<Package> q = new Queue<Package>();
			q.Enqueue(pkg);
			while (q.Count > 0)
			{
				Package p = q.Dequeue();
				LoadExtensions(p, result);
				if (result.Contains(p))
				{
					result.Remove(p);
				}
				result.Add(p);
				p.Dependencies.ForEach(q.Enqueue);
			}
			return result;
		}

		private void LoadExtensions(Package p, List<Package> result)
		{
			foreach (Package ext in paks)
			{
				if (ext.Manifest.type == PackageType.extension && (ext.Manifest.extends == "any" || ext.Manifest.extends == p.Id))
				{
					if (result.Contains(ext))
					{
						result.Remove(ext);
					}
					result.Add(ext);
				}
			}
		}

		private Package FindPackage(string value, Version ver)
		{
			foreach (Package p in paks)
			{
				if (p.Id == value && p.Version.CompareTo(ver) >= 0)
				{
					return p;
				}
			}
			return null;
		}
	}

	private List<Package> paks = new List<Package>();

	private PackageLoader loader = new PackageLoader();

	public static DirectoryInfo PackagesRoot { get; } = new DirectoryInfo("./Packages");

	public IReadOnlyList<Package> StoryPackages => paks.Where((Package p) => p.Manifest.type == PackageType.story).ToList();

	public IReadOnlyList<Package> PluginPackages => paks.Where((Package p) => p.Manifest.type == PackageType.plugin).ToList();

	public Observable OnPackagesReady { get; } = new Observable();

	public Package FindPackage(string id)
	{
		foreach (Package p in paks)
		{
			if (p.Id == id)
			{
				return p;
			}
		}
		return null;
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<DispatcherService>().InvokeLater(PostStart);
	}

	private void PostStart()
	{
		DispatcherService dispatcher = Lookup<DispatcherService>();
		loader.Freeze();
		dispatcher.InvokeAsync<List<Package>>((Func<List<Package>>)loader.LoadDiskDataAndResolve, (Action<List<Package>>)delegate(List<Package> ps)
		{
			paks.Clear();
			paks.AddRange(ps);
			OnPackagesReady.Invoke();
		}, (Action<Exception>)null);
	}

	public void AddInMemoryPackage(VirtIO io)
	{
		loader.AddInMemoryPackage(io);
	}

	public VirtIO CreateMergedFS(Package package, List<Package> disabledExts)
	{
		List<VirtIO> ios = new List<VirtIO>();
		foreach (Package p in package.AllDependencies)
		{
			if (!disabledExts.Contains(p))
			{
				ios.Add(p.LocalFS);
			}
		}
		ios.Reverse();
		VirtIO lastIO = ios.Last();
		if (!(lastIO is FSVirtIo))
		{
			lastIO = null;
		}
		return new CachedIO(ios, lastIO);
	}
}
