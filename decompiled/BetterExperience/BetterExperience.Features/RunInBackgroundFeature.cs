using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features;

internal class RunInBackgroundFeature : PluginFeature
{
	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		// The description IS the settings-menu label (PluginOptionsService reads
		// Description.Description), so it has to describe the SYMPTOM. "RunInBackground: Enable
		// feature" names the flag and tells a player nothing about what it fixes.
		enableFeature = config.Bind<bool>("Features", "RunInBackground", false,
			"Keep running when alt-tabbed (without this the game freezes on focus loss)");
	}

	/// <summary>
	/// Nothing in this options system auto-enumerates config entries — each is surfaced by an
	/// explicit Expose call. That is how this setting managed to exist, work, and be
	/// live-updatable for its entire life while remaining invisible in game: the only way to
	/// change it was to hand-edit f95.betterexperience.cfg and restart.
	/// </summary>
	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		logger.Info("BG prio {0}", Application.backgroundLoadingPriority);
		Application.backgroundLoadingPriority = ThreadPriority.High;
		Application.runInBackground = enableFeature.Value;
		enableFeature.SettingChanged += delegate
		{
			Application.runInBackground = enableFeature.Value;
		};
	}
}
