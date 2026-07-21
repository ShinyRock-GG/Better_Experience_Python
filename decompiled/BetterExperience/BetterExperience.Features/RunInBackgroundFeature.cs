using BepInEx.Configuration;
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
		enableFeature = config.Bind<bool>("Features", "RunInBackground", false, "RunInBackground: Enable feature");
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
