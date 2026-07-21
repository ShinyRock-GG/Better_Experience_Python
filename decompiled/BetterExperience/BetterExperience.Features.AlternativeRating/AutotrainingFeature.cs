using BepInEx.Configuration;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.AlternativeRating;

internal class AutotrainingFeature : PluginFeature
{
	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		enableFeature = config.Bind<bool>("Features", "EnableAutoTraining", false, "Allows to corrupt save game, and skip days or weeks with End or Home button.");
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().SessionServices.Add(() => new AutoTraining());
	}
}
