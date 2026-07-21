using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoratingFeature : PluginFeature
{
	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<float> appreancePull;

	private ConfigEntry<float> personalityPull;

	private ConfigEntry<bool> zeroLevelPull;

	private ConfigEntry<float> deterministicPull;

	private ConfigEntry<float> appearanceRatingWeight;

	private ConfigEntry<float> appearanceScoreWeight;

	private ConfigEntry<float> personalityRatingWeight;

	private ConfigEntry<float> personalityScoreWeight;

	public override bool Enabled => enableFeature.Value;

	public bool PullNonZeroLevels => !zeroLevelPull.Value;

	public float AppearancePull => appreancePull.Value;

	public float PersonalityPull => personalityPull.Value;

	public float AppearanceRatingWeight => appearanceRatingWeight.Value;

	public float AppearanceScoreWeight => appearanceScoreWeight.Value;

	public float PersonalityRatingWeight => personalityRatingWeight.Value;

	public float PersonalityScoreWeight => personalityScoreWeight.Value;

	public float DeterministicPull => deterministicPull.Value;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		enableFeature = config.Bind<bool>("Features", "AlternativeRatings", false, "Alternative Ratings: Enable feature [restart]");
		appreancePull = config.Bind<float>("AlternativeRating", "AppearancePull", 1f, "Alternative Ratings: Apprearance pull");
		personalityPull = config.Bind<float>("AlternativeRating", "PersonalityPull", 1f, "Alternative Ratings: Personality pull");
		zeroLevelPull = config.Bind<bool>("AlternativeRating", "ZeroPull", false, "Alternative Ratings: Level-1 only pulling");
		deterministicPull = config.Bind<float>("AlternativeRating", "DeterministicPull", 0.3f, "Alternative Ratings: Deterministic pulling factor (0..1)");
		appearanceRatingWeight = config.Bind<float>("AlternativeRating", "AppearanceRatingWeight", 0f, "Alternative Ratings: Appearance Rating Weight");
		appearanceScoreWeight = config.Bind<float>("AlternativeRating", "AppearanceScoreWeight", 1f, "Alternative Ratings: Appearance Score Weight");
		personalityRatingWeight = config.Bind<float>("AlternativeRating", "PersonalityRatingWeight", 1f, "Alternative Ratings: Personality Rating Weight");
		personalityScoreWeight = config.Bind<float>("AlternativeRating", "PersonalityScoreWeight", 0f, "Alternative Ratings: Personality Score Weight");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
		if (enableFeature.Value)
		{
			Lookup<PluginOptionsService>().Expose(appreancePull, base.Scope);
			Lookup<PluginOptionsService>().Expose(personalityPull, base.Scope);
			Lookup<PluginOptionsService>().Expose(zeroLevelPull, base.Scope);
			Lookup<PluginOptionsService>().Expose(deterministicPull, base.Scope);
			Lookup<PluginOptionsService>().Expose(appearanceRatingWeight, base.Scope);
			Lookup<PluginOptionsService>().Expose(appearanceScoreWeight, base.Scope);
			Lookup<PluginOptionsService>().Expose(personalityRatingWeight, base.Scope);
			Lookup<PluginOptionsService>().Expose(personalityScoreWeight, base.Scope);
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().SessionServices.Add(() => new AutoratingService
		{
			Feature = this
		});
	}
}
