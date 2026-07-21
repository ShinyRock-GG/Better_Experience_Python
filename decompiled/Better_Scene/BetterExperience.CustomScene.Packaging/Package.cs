using System;
using System.Collections.Generic;

namespace BetterExperience.CustomScene.Packaging;

public class Package
{
	private PackageManifest manifest;

	private VirtIO virtIo;

	public string Id => manifest.id;

	public string Name => manifest.name;

	public Version Version { get; private set; }

	public string Location { get; set; }

	public VirtIO LocalFS => virtIo;

	public PackageManifest Manifest => manifest;

	public List<Package> Dependencies { get; set; }

	public List<Package> AllDependencies { get; set; }

	public string ErrorDescription { get; set; }

	public Package(PackageManifest manifest, VirtIO io)
	{
		this.manifest = manifest;
		virtIo = io;
		Version = Version.Parse(manifest.version);
	}
}
