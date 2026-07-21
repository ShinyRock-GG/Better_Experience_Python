using System;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.PyStory.AI;
using BetterExperience.PyStory.UI;
using UnityEngine;

namespace BetterExperience.PyStory;

public class PyStoryFeature : PluginFeature
{
	private ConfigEntry<KeyboardShortcut> useHotkey;

	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		base.Configure(config);
		useHotkey = config.Bind<KeyboardShortcut>("Story", "UseHotkey", new KeyboardShortcut(KeyCode.F, Array.Empty<KeyCode>()), "Story: 'Use' hotkey");
	}

	public override void OnInit()
	{
		base.OnInit();
		BetterExperience.Features.PluginOptions.PluginOptionsService options = Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>();
		options.Expose(useHotkey, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null);
	}

	public override void OnStart()
	{
		base.OnStart();
		CustomSceneFeature csf = Lookup<CustomSceneFeature>();
		CrashWindow crashWindow = new CrashWindow(Lookup<OverlayService>(), base.Scope);
		crashWindow.SetWindowVisible(v: false);
		TaskWindow window = new TaskWindow();
		csf.EditorUiPanel.GameView.Add(window);
		csf.EditorUiPanel.GameView.Add(crashWindow);
		ScopeSupport csscope = csf.Scope;
		StoryManager storymanager = csscope.Lookup<StoryManager>();
		storymanager.StoryServices.Add(() => new DialogueManager
		{
			RootVisualElement = csf.UiPanel.GameView
		});
		storymanager.StoryServices.Add(() => new StimulusTrackingService());
		storymanager.StoryServices.Add(() => new SimpleAi());
		storymanager.StoryServices.Add(() => new ObjectInteractionService
		{
			UseHotkey = useHotkey
		});
		storymanager.StoryServices.Add(() => new PyStoryRuntimeService
		{
			CrashWindow = crashWindow
		});
	}
}
