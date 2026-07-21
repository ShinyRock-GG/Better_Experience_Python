using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class NoMeansNoFeature : PluginFeature
{
	// MemoriaDeCharacterTemporal is [Obsolete(error:true)] in SMA 23.1.
	// Feature body disabled for compatibility; the service is a no-op.
	private class DisableConfirmationDialogService : SessionService
	{
	}

	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableNoMeansNo", true, "Enable NoMeansNo: disables 'Are you sure you want me to leave?' question");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.guest);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().InterviewServices.Add(() => new DisableConfirmationDialogService());
	}
}
