using System;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.UI;

namespace BetterExperience.Features;

internal class DebugInfoFeature : PluginFeature
{
	private class DebugInfoService : StoryService
	{
		private DebugInfoFeature host;

		private InteractionManager manager;

		private AnimatorServiceInfo animatorStateInfo;

		public DebugInfoService(DebugInfoFeature host)
		{
			this.host = host;
		}

		public override void OnStart()
		{
			base.OnStart();
			manager = Lookup<InteractionManager>();
			animatorStateInfo = new AnimatorServiceInfo(manager);
			Lookup<OverlayService>().RequestPanel(1280, 720).GameView.Add(animatorStateInfo);
			animatorStateInfo.ShowInfo = host.ShowAnimDebugInfo.Value;
			host.ShowAnimDebugInfo.SettingChanged += ShowAnimDebugInfo_SettingChanged;
			host.ShowAnimatorLogs.SettingChanged += ShowAnimatorLogs_SettingChanged;
			ShowAnimatorLogs_SettingChanged(null, null);
		}

		private void ShowAnimatorLogs_SettingChanged(object sender, EventArgs e)
		{
			manager._logger.EnableDebug = host.ShowAnimatorLogs.Value;
			ExtensibleAnimator.Logger.EnableDebug = host.ShowAnimatorLogs.Value;
		}

		private void ShowAnimDebugInfo_SettingChanged(object sender, EventArgs e)
		{
			animatorStateInfo.ShowInfo = host.ShowAnimDebugInfo.Value;
		}

		public override void OnStop()
		{
			host.ShowAnimDebugInfo.SettingChanged -= ShowAnimDebugInfo_SettingChanged;
			host.ShowAnimatorLogs.SettingChanged -= ShowAnimatorLogs_SettingChanged;
		}
	}

	public ConfigEntry<bool> ShowAnimDebugInfo { get; private set; }

	public ConfigEntry<bool> ShowAnimatorLogs { get; private set; }

	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		ShowAnimDebugInfo = config.Bind<bool>("Story", "AnimatorDebugInfo", false, "Scene: [Debug] Show animator state");
		ShowAnimatorLogs = config.Bind<bool>("Story", "AnimatorDebugLog", false, "Scene: [Debug] More animator logs");
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>().Expose(ShowAnimDebugInfo, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null);
		Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>().Expose(ShowAnimatorLogs, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null);
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new DebugInfoService(this));
	}
}
