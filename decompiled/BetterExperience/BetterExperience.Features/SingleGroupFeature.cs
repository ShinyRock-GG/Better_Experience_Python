using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class SingleGroupFeature : PluginFeature
{
	// PiscinasDeEventosDeEntrevista is [Obsolete(error:true)] in SMA 23.1
	// ("Now on, there will be only a single pool") — feature is a no-op since SMA 23.1 enforces this natively.
	public class SingleGroupModeService : SessionService
	{
		public override void OnStart()
		{
			base.OnStart();
		}
	}

	public static class HarmonyPatches
	{
	}

	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "SingleGroupMode", false, "Enable Single Group Mode: Use single gene pool regardless of level");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().SessionServices.Add(() => new SingleGroupModeService());
	}
}
