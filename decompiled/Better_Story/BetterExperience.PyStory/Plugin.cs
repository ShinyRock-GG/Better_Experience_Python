using BepInEx;
using BepInEx.Bootstrap;

namespace BetterExperience.PyStory;

[BepInPlugin("f95.betterexperience.pycs", "Better Story Mod", "1.6.0")]
[BepInDependency("f95.betterexperience", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("f95.betterexperience.cs", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
	public static Plugin Instance { get; private set; }

	public static string version => Chainloader.PluginInfos["f95.betterexperience.pycs"].Metadata.Version.ToString();

	public void Awake()
	{
		Instance = this;
		BetterExperience.Plugin core = (BetterExperience.Plugin)(object)Chainloader.PluginInfos["f95.betterexperience"].Instance;
		core.AddService(new PyStoryFeature());
		core.AddService(new ScriptPluginFeature());
	}
}
