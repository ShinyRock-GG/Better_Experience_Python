using BepInEx;
using BepInEx.Bootstrap;
using BetterExperience;

namespace Better_Cloth;

[BepInPlugin("f95.betterexperience.cloth", "Better Cloth Mod", "1.6.0")]
[BepInDependency("f95.betterexperience", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
	public void Awake()
	{
		BetterExperience.Plugin core = (BetterExperience.Plugin)(object)Chainloader.PluginInfos["f95.betterexperience"].Instance;
		core.AddService(new ClothFeature());
	}
}
