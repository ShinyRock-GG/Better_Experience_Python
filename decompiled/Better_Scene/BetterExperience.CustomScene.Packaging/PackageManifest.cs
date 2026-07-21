using System.Collections.Generic;

namespace BetterExperience.CustomScene.Packaging;

public class PackageManifest
{
	public string id { get; set; }

	public string name { get; set; }

	public string version { get; set; }

	public string author { get; set; } = "anon";

	public PackageType type { get; set; }

	public string mainScene { get; set; }

	public string extends { get; set; }

	public Dictionary<string, string> require { get; set; } = new Dictionary<string, string>();

	public Dictionary<string, string> plugins { get; set; } = new Dictionary<string, string>();

	public string description { get; set; }

	public StoryType storyType { get; set; }

	public Dictionary<string, string> options { get; set; } = new Dictionary<string, string>();
}
