using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features;

internal class OverlayFeature : PluginFeature
{
	private ConfigEntry<KeyboardShortcut> hideOverlayHotkey;

	private OverlayService overlayService;

	public override bool Enabled => true;

	public List<Drawable> Renderables { get; private set; } = new List<Drawable>();

	public List<TemporaryNotification> Notifications { get; set; } = new List<TemporaryNotification>();

	public List<Drawable> MainMenuRenderables { get; private set; } = new List<Drawable>();

	public List<Drawable> AlwaysOnScreenRenderables { get; private set; } = new List<Drawable>();

	public override void Configure(ConfigFile config)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		base.Configure(config);
		hideOverlayHotkey = config.Bind<KeyboardShortcut>("Overlay", "HideOverlayHotkey", new KeyboardShortcut(KeyCode.Backspace, Array.Empty<KeyCode>()), "Overlay: Hide overlay hotkey");
	}

	public override void OnInit()
	{
		overlayService = base.Scope.Parent.AddService(new OverlayService(Renderables, Notifications, AlwaysOnScreenRenderables));
	}

	public override void OnStart()
	{
		Lookup<PluginOptionsService>().Expose(hideOverlayHotkey, base.Scope);
		Lookup<SessionTracker>().SessionServices.Add(() => new SessionOverlayRenderService(Renderables, Notifications, AlwaysOnScreenRenderables)
		{
			HideOverlayHotkey = hideOverlayHotkey,
			CursorHolders = overlayService.CursorHolders
		});
	}
}
