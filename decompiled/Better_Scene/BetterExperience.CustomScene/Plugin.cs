using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BetterExperience.Features;

namespace BetterExperience.CustomScene;

[BepInPlugin("f95.betterexperience.cs", "Better Scene Mod", "1.6.0")]
[BepInDependency("f95.betterexperience", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
	public void Awake()
	{
		BetterExperience.Plugin core = (BetterExperience.Plugin)(object)Chainloader.PluginInfos["f95.betterexperience"].Instance;
		CustomSceneFeature cs = core.AddService(new CustomSceneFeature(((BaseUnityPlugin)this).Config));
		cs.Scope.Provide<ConfigFile>(((BaseUnityPlugin)this).Config);
		cs.Scope.AddService(new AnimateUndressFeature());
		cs.Scope.AddService(new AnimateGotoFeature());
		cs.Scope.AddService(new AnimatePostureChangeFeature());
		cs.Scope.AddService(new ActorControllerTuningFeature());
		cs.Scope.AddService(new IKFeature());
		cs.Scope.AddService(new IKHeelsFeature());
		cs.Scope.AddService(new RelIK2Feature());
		cs.Scope.AddService(new DebugInfoFeature());
		core.AddService(new ProxyVolumeFeature());

		// Neutralize the reused scene's global +EV post-exposure wash-out the instant
		// any scene loads -- this fires before BE's Python runtime even exists, so it
		// covers the pre-model loading window that no Python hook can reach.
		SceneWashoutFix.Register(((BaseUnityPlugin)this).Config);
	}
}
