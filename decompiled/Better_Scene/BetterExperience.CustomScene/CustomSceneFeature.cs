using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.ConstructionKit;
using BetterExperience.CustomScene.Monkey;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Features;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene;

public class CustomSceneFeature : PluginFeature
{
	private StoryManager storyteller;

	private StoryLauncherWindow storyLauncherController;

	private Button storyBtn;

	private MainMenuTracker menuTracker;

	private PackageManager packageManager;

	private ConfigFile config;

	private ConfigEntry<bool> enableGuestSelfCollision;

	private ConfigEntry<string> storedDisabledExtensions;

	private ConfigEntry<string> lastStory;

	private ConfigEntry<bool> drawArmature;

	public static ConfigEntry<bool> VerboseSceneLogging { get; private set; }

	private ConfigEntry<KeyboardShortcut> startCustomPoseHotkey;

	public ConfigFile PluginConfig => config;

	public override bool Enabled => true;

	public UITKManagedPanel EditorUiPanel { get; private set; }

	public UITKManagedPanel UiPanel { get; private set; }

	public CustomSceneFeature(ConfigFile config)
	{
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		this.config = config;
		enableGuestSelfCollision = config.Bind<bool>("Features", "EnableGuestSelfCollision", false, "Guest Self Collision: Enable feature [restart required]");
		storedDisabledExtensions = config.Bind<string>("Story", "DisabledExtensions", "", "Disabled Extensions list");
		lastStory = config.Bind<string>("Story", "LastStory", "", "Last played story id");
		drawArmature = config.Bind<bool>("Story", "DrawArmature", false, "Scene: [Debug] Visualize armature");
		VerboseSceneLogging = config.Bind<bool>("Story", "VerboseSceneLogging", false, "Scene: [Debug] Verbose asset loading logs");
		startCustomPoseHotkey = config.Bind<KeyboardShortcut>("Story", "CustomPoseHotkey", new KeyboardShortcut(KeyCode.Tab, new KeyCode[1] { KeyCode.LeftControl }), "Scene: [Debug] Start custom pose hotkey");
		Armature.DrawArmature.Value = drawArmature.Value;
		drawArmature.SettingChanged += delegate
		{
			Armature.DrawArmature.Value = drawArmature.Value;
		};
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>().Expose(drawArmature, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null).Expose(startCustomPoseHotkey, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null).Expose(enableGuestSelfCollision, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null).Expose(VerboseSceneLogging, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null);
		EditorUiPanel = Lookup<OverlayService>().RequestPanel(0, 0);
		UiPanel = Lookup<OverlayService>().RequestPanel(1280, 720);
		Harmony.CreateAndPatchAll(typeof(BetterSceneHarmonyPatches), (string)null);
		menuTracker = base.Scope.AddService(new MainMenuTracker());
		packageManager = base.Scope.AddService(new PackageManager());
		storyteller = base.Scope.AddService(new StoryManager());
		storyteller.StoryServices.Add(() => new AssetLoader());
		storyteller.SceneServices.Add(() => new POIManager());
		storyteller.SceneServices.Add(() => new PoseManager());
		storyteller.StoryInterviewServices.Add(() => new PositionManager());
		storyteller.StoryInterviewServices.Add(() => new InteractionManager());
		storyteller.StoryInterviewServices.Add(() => new ExtendedPoseEditor
		{
			StartCustomPoseHotkey = startCustomPoseHotkey
		});
		if (enableGuestSelfCollision.Value)
		{
			storyteller.StoryInterviewServices.Add(() => new GuestSelfCollisionFeature());
		}
		base.Scope.AddService(new MonkeyBridge());
		storyLauncherController = new StoryLauncherWindow(UiPanel.MainMenu);
		storyLauncherController.Play += StoryLauncherController_Play;
		storyLauncherController.Hidden += InvalidateVisibility;
		storyLauncherController.Hide();
		storyBtn = new Button();
		storyBtn.text = "<< loading packages >>";
		storyBtn.SetEnabled(value: false);
		storyBtn.style.top = new Length(100f, LengthUnit.Percent);
		storyBtn.style.left = new Length(50f, LengthUnit.Percent);
		storyBtn.style.width = 600f;
		storyBtn.style.height = 40f;
		storyBtn.style.marginLeft = -300f;
		storyBtn.style.marginTop = -120f;
		UiPanel.MainMenu.Add(storyBtn);
		UIBuilder.Hide((VisualElement)storyBtn);
		storyBtn.clicked += StoryBtn_clicked;
		menuTracker.OnStateChanged.Add(InvalidateVisibility, base.Scope);
		packageManager.OnPackagesReady.Add(WhenPackagesReady, base.Scope);
	}

	private void WhenPackagesReady()
	{
		storyLauncherController.DisabledExtensions = DeserializeExtensions(storedDisabledExtensions.Value);
		storyLauncherController.SetPackages(packageManager.StoryPackages);
		storyLauncherController.SetLastStoryId(lastStory.Value);
		storyBtn.text = "Custom Story";
		storyBtn.SetEnabled(value: true);
	}

	private List<string> DeserializeExtensions(string value)
	{
		try
		{
			return (from x in value.Split(new char[1] { ';' })
				select x.Trim()).ToList();
		}
		catch (Exception)
		{
			logger.Error("Failed to parse extension set {0}", value);
			return new List<string>();
		}
	}

	private void StoryBtn_clicked()
	{
		UIBuilder.Hide((VisualElement)storyBtn);
		storyLauncherController.Show();
	}

	private void InvalidateVisibility()
	{
		if (!menuTracker.IsMainMenu)
		{
			UIBuilder.Hide((VisualElement)storyBtn);
			storyLauncherController.Hide();
		}
		else if (storyLauncherController.IsVisible)
		{
			UIBuilder.Hide((VisualElement)storyBtn);
		}
		else
		{
			UIBuilder.Show((VisualElement)storyBtn);
		}
	}

	private void StoryLauncherController_Play()
	{
		Package package = storyLauncherController.SelectedPackage;
		if (package == null)
		{
			return;
		}
		IReadOnlyList<Package> enabledExtensions = storyLauncherController.SelectedExtensions;
		List<Package> allExtensions = package.AllDependencies.Where((Package x) => x.Manifest.type == PackageType.extension).ToList();
		List<string> disabled = storyLauncherController.DisabledExtensions.ToList();
		List<Package> disabledExts = new List<Package>();
		foreach (Package ext in allExtensions)
		{
			if (enabledExtensions.Contains(ext))
			{
				disabled.Remove(ext.Id);
				continue;
			}
			if (!disabled.Contains(ext.Id))
			{
				disabled.Add(ext.Id);
			}
			disabledExts.Add(ext);
		}
		storedDisabledExtensions.Value = string.Join(";", disabled);
		storyLauncherController.DisabledExtensions = disabled;
		lastStory.Value = package.Id;
		if (storyLauncherController.SingleMode)
		{
			menuTracker.SingleInterview(delegate
			{
				storyteller.SelectStory(package, disabledExts);
			});
		}
		else
		{
			menuTracker.Continue(delegate
			{
				storyteller.SelectStory(package, disabledExts);
			});
		}
	}
}
