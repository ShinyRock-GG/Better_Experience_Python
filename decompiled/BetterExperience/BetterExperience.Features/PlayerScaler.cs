using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using UnityEngine;

namespace BetterExperience.Features;

internal class PlayerScaler : PluginFeature
{
	public class ScalerService : SessionService
	{
		private KeyboardShortcut grow;

		private KeyboardShortcut shrink;

		private KeyboardShortcut reset;

		private IInputHandle growKey;

		private IInputHandle shrinkKey;

		private IInputHandle resetKey;

		public ScalerService(KeyboardShortcut grow, KeyboardShortcut shrink, KeyboardShortcut reset)
		{
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			//IL_0008: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0015: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			this.grow = grow;
			this.shrink = shrink;
			this.reset = reset;
		}

		public override void OnStart()
		{
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0049: Unknown result type (might be due to invalid IL or missing references)
			DispatcherService dispatcher = Lookup<DispatcherService>();
			growKey = dispatcher.Input.KeyboardEvent(grow, base.Scope);
			shrinkKey = dispatcher.Input.KeyboardEvent(shrink, base.Scope);
			resetKey = dispatcher.Input.KeyboardEvent(reset, base.Scope);
			dispatcher.DoUpdate.Add(HandleInput, base.Scope);
			base.Session.OnGuestReady += delegate
			{
				base.Session.Player.ResetScale();
			};
		}

		private void HandleInput()
		{
			PlayerCharacter player = base.Session.Player;
			if (player.ActionsEnabled)
			{
				Vector3 direction = Vector3.zero;
				if (resetKey.IsHold)
				{
					player.ResetScale();
				}
				else if (growKey.IsHold)
				{
					direction += Vector3.up;
				}
				else if (shrinkKey.IsHold)
				{
					direction += Vector3.down;
				}
				if (direction != Vector3.zero)
				{
					player.AddScale(direction * 0.1f * Time.deltaTime);
				}
			}
		}
	}

	private ConfigEntry<bool> configEnablePlayerScaler;

	private ConfigEntry<KeyboardShortcut> growShortcut;

	private ConfigEntry<KeyboardShortcut> shrinkShortcut;

	private ConfigEntry<KeyboardShortcut> resetShortcut;

	public override bool Enabled => configEnablePlayerScaler.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		configEnablePlayerScaler = config.Bind<bool>("Features", "PlayerScaler", true, "Player Scaler: Enable feature");
		growShortcut = config.Bind<KeyboardShortcut>("PlayerScaler", "GrowKey", new KeyboardShortcut(KeyCode.Q, new KeyCode[1] { KeyCode.LeftControl }), "Player Scaler: Grow Key");
		shrinkShortcut = config.Bind<KeyboardShortcut>("PlayerScaler", "ShrinkKey", new KeyboardShortcut(KeyCode.E, new KeyCode[1] { KeyCode.LeftControl }), "Player Scaler: Shrink Key");
		resetShortcut = config.Bind<KeyboardShortcut>("PlayerScaler", "ResetKey", new KeyboardShortcut(KeyCode.R, new KeyCode[1] { KeyCode.LeftControl }), "Player Scaler: Reset Key");
	}

	public override void OnInit()
	{
		PluginOptionsService options = Lookup<PluginOptionsService>();
		options.Expose(configEnablePlayerScaler, base.Scope, PluginOptionsService.SettingsType.player);
		options.Expose(growShortcut, base.Scope, PluginOptionsService.SettingsType.player);
		options.Expose(shrinkShortcut, base.Scope, PluginOptionsService.SettingsType.player);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().SessionServices.Add(() => new ScalerService(growShortcut.Value, shrinkShortcut.Value, resetShortcut.Value));
	}
}
