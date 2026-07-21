using BepInEx.Configuration;
using BetterExperience.Features.AlternativeGenetics;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class AlternativeGeneticsFeature : PluginFeature
{
	private ConfigEntry<bool> featureEnabled;

	private ConfigEntry<bool> overlayEnabled;

	private ConfigEntry<bool> slutnessSwapEnabled;

	private ConfigEntry<bool> autoratingDisabled;

	public override bool Enabled => featureEnabled.Value;

	public override void Configure(ConfigFile config)
	{
		featureEnabled = config.Bind<bool>("Features", "AlternativeGenetics", false, "Alternative Genetics: Enable feature");
		overlayEnabled = config.Bind<bool>("AlternativeGenetics", "ShowOverlay", true, "Alternative Genetics: Enable pool state overlay (requires restart)");
		slutnessSwapEnabled = config.Bind<bool>("AlternativeGenetics", "SwapModellingSlutness", false, "Alternative Genetics: Swap 'Modeling' and 'Slutness' ratings (requires restart)");
		autoratingDisabled = config.Bind<bool>("AlternativeGenetics", "DisableAutorating", true, "Alternative Genetics: Disable autorating system (requires restart)");
	}

	public override void OnInit()
	{
		Lookup<PluginOptionsService>().Expose(featureEnabled, base.Scope);
		Lookup<PluginOptionsService>().Expose(overlayEnabled, base.Scope);
		Lookup<PluginOptionsService>().Expose(slutnessSwapEnabled, base.Scope);
		Lookup<PluginOptionsService>().Expose(autoratingDisabled, base.Scope);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().SessionServices.Add(() => new AlternativeGeneticsService
		{
			ShowOverlay = overlayEnabled.Value,
			SwapSlutnesAndModeling = slutnessSwapEnabled.Value,
			DisableAutorating = autoratingDisabled.Value
		});
	}
}
