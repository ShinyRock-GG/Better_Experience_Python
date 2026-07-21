using System;
using System.Collections.Generic;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.PyStory;

public class ScriptPluginFeature : PluginFeature
{
	public class Plugin
	{
		public readonly Package package;

		public readonly VirtIO virtIO;

		public readonly Observable restart = new Observable();

		public Plugin(Package package, VirtIO virtIO)
		{
			this.package = package;
			this.virtIO = virtIO;
		}
	}

	public CustomSceneFeature customScene { get; private set; }

	public StoryManager storyManager { get; private set; }

	public PackageManager packageManager { get; private set; }

	public List<Plugin> plugins { get; private set; } = new List<Plugin>();

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		customScene = base.Scope.Lookup<CustomSceneFeature>();
		storyManager = customScene.Scope.Lookup<StoryManager>();
		packageManager = customScene.Scope.Lookup<PackageManager>();
		packageManager.OnPackagesReady.Add(delegate
		{
			plugins.Clear();
			foreach (Package current in packageManager.PluginPackages)
			{
				plugins.Add(new Plugin(current, packageManager.CreateMergedFS(current, new List<Package>())));
			}
			GenerateSettings();
		});
		base.Scope.Lookup<SessionTracker>().SessionServices.Add(() => new SessionScriptFeature(this));
	}

	private void GenerateSettings()
	{
		BetterExperience.Features.PluginOptions.PluginOptionsService options = Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>();
		foreach (Plugin plugin in plugins)
		{
			options.ExposeAction((Action)plugin.restart.Invoke, ":" + plugin.package.Name + " Restart", "Plugins", base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)4, "PyPlugins");
		}
	}
}
