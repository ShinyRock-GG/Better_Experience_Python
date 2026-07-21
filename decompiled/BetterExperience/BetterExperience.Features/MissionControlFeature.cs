using System;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features;

internal class MissionControlFeature : PluginFeature
{
	private class MissionControlService : SessionService
	{
		private IInputHandle toggleKey;

		public MissionControlWindow Window { get; private set; }

		public ConfigEntry<KeyboardShortcut> Hotkey { get; internal set; }

		public override void OnStart()
		{
			base.OnStart();
			DispatcherService dispatcher = Lookup<DispatcherService>();
			toggleKey = dispatcher.Input.KeyboardEvent(Hotkey, base.Scope);
			dispatcher.DoUpdate.Add(OnUpdate, base.Scope);
			Window = new MissionControlWindow();
			base.Scope.EventHandler(delegate(EventHandler h)
			{
				Hotkey.SettingChanged += h;
			}, delegate(EventHandler h)
			{
				Hotkey.SettingChanged -= h;
			}, delegate
			{
				//IL_0011: Unknown result type (might be due to invalid IL or missing references)
				//IL_0016: Unknown result type (might be due to invalid IL or missing references)
				Window.Text = "Mission Control [" + ((object)Hotkey.Value/*cast due to constrained. prefix*/).ToString() + "]";
			})(null, null);
			if (Plugin.MonkeyMode)
			{
				Lookup<OverlayService>().AddDrawable(new DockingContainer(Vector2Int.down + Vector2Int.right, Window), base.Scope);
			}
			else
			{
				Lookup<OverlayService>().AddDrawable(new DockingContainer(Vector2Int.up + Vector2Int.left, Window)
				{
					Position = new Vector2(10f, 150f)
				}, base.Scope);
			}
			try
			{
				Window.InitAutoThrust(Lookup<AutoThrustFeature.AutoThrustService>());
			}
			catch (Exception)
			{
			}
			try
			{
				Window.InitVelocityControl(Lookup<VelocityControlFeature.VelocityControlService>());
			}
			catch (Exception)
			{
			}
		}

		private void OnUpdate()
		{
			if (toggleKey.Up)
			{
				Window.Visible = !Window.Visible;
			}
			if (Window.Visible)
			{
				Window.Refresh();
			}
		}
	}

	private class MissionControlWindow : DrawableWindow
	{
		private HLayout<Drawable> layout;

		private DrawableToggle maxVeloctyLabel;

		private DrawableSlider maxVelocitySlider;

		private DrawableLabel userThrustLabel;

		private DrawableLabel forwardTargetLabel;

		private DrawableLabel backwardTargetLabel;

		private DrawableSlider backwardTargetSlider;

		private DrawableSlider forwardTargetSlider;

		private DrawableSlider userThrustSlider;

		private DrawableToggle activeVelocityLabel;

		private DrawableSlider activeVelocitySlider;

		private DrawableLabel speedLabel;

		private DrawableSlider speedSlider;

		private DrawableSlider depthSlider;

		private DrawableLabel activeThrustLabel;

		private DrawableSlider activeThrustSlider;

		private VLayout<Drawable> atLayout;

		private VLayout<Drawable> asLayout;

		private VelocityControlFeature.VelocityControlService asService;

		private AutoThrustFeature.AutoThrustService atService;

		private DrawableLabel depthLabel;

		private DrawableSlider activePRatioSlider;

		private DrawableToggle activePRatioLabel;

		public MissionControlWindow()
			: base(400, 275)
		{
			base.Text = "Mission Control";
			base.Visible = false;
			layout = this.HLayout();
			layout.Spacing = 10f;
			atLayout = layout.VLayout();
			atLayout.Label("Player");
			maxVeloctyLabel = atLayout.Toggle("Max velocity:");
			maxVeloctyLabel.OnValueChanged += MaxVeloctyLabel_OnValueChanged;
			maxVelocitySlider = atLayout.HSlider(0f, 0f, 2f);
			ConfigureSlider(maxVelocitySlider);
			userThrustLabel = atLayout.Label("Thrust balance:");
			userThrustSlider = atLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(userThrustSlider);
			forwardTargetLabel = atLayout.Label("Forward Target: 100%");
			forwardTargetSlider = atLayout.HSlider(1f, 0f, 1f);
			ConfigureSlider(forwardTargetSlider);
			backwardTargetLabel = atLayout.Label("Backward Target: 0%");
			backwardTargetSlider = atLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(backwardTargetSlider);
			atLayout.Label("Read-only values:");
			activeVelocityLabel = atLayout.Toggle("Active velocity:");
			activeVelocityLabel.OnValueChanged += ActiveVelocityLabel_OnValueChanged;
			activeVelocitySlider = atLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activeVelocitySlider);
			activeThrustLabel = atLayout.Label("Active thrust:");
			activeThrustSlider = atLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activeThrustSlider);
			activePRatioLabel = atLayout.Toggle("Active P-ratio:");
			activePRatioLabel.OnValueChanged += ActivePRatioLabel_OnValueChanged;
			activePRatioSlider = atLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activePRatioSlider);
			atLayout.Visible = false;
			asLayout = layout.VLayout();
			asLayout.Label("Guest");
			speedLabel = asLayout.Label("Speed: default");
			speedSlider = asLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(speedSlider);
			depthLabel = asLayout.Label("Depth: default");
			depthSlider = asLayout.HSlider(0f, 0f, 0.1f);
			ConfigureSlider(depthSlider);
			asLayout.Visible = false;
			maxVelocitySlider.OnValueChange += VelocitySlider_OnValueChange;
			userThrustSlider.OnValueChange += ThrustSlider_OnValueChange;
			forwardTargetSlider.OnValueChange += ForwardTargetSlider_OnValueChange;
			backwardTargetSlider.OnValueChange += BackwardTargetSlider_OnValueChange;
			speedSlider.OnValueChange += SpeedSlider_OnValueChange;
			depthSlider.OnValueChange += DepthSlider_OnValueChange;
		}

		private void ActivePRatioLabel_OnValueChanged()
		{
			if (atService != null)
			{
				atService.IgnorePRatio = activePRatioLabel.Value;
			}
		}

		private void ActiveVelocityLabel_OnValueChanged()
		{
			if (atService != null)
			{
				atService.VelocityRampUp = !activeVelocityLabel.Value;
			}
		}

		private void MaxVeloctyLabel_OnValueChanged()
		{
			if (maxVeloctyLabel.Value)
			{
				maxVelocitySlider.MaxValue = 4f;
			}
			else
			{
				maxVelocitySlider.MaxValue = 2f;
			}
			if (atService != null)
			{
				atService.ViolentMode = maxVeloctyLabel.Value;
			}
		}

		private void DepthSlider_OnValueChange()
		{
			if (asService != null)
			{
				asService.Depth = depthSlider.Value;
				if (asService.Depth == 0f)
				{
					depthLabel.Text = "Depth: default";
				}
				else
				{
					depthLabel.Text = $"Depth: {asService.Depth:G3}";
				}
			}
		}

		private void SpeedSlider_OnValueChange()
		{
			if (asService != null)
			{
				asService.Velocity = speedSlider.Value;
				if (asService.Velocity == 0f)
				{
					speedLabel.Text = "Speed: default";
				}
				else
				{
					speedLabel.Text = $"Speed: {asService.Velocity:G3}";
				}
			}
		}

		private void ThrustSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.UserThrustBalance = userThrustSlider.Value;
				userThrustLabel.Text = $"Thrust balance: {atService.UserThrustBalance:G3}";
			}
		}

		private void VelocitySlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.MaxVelocity = maxVelocitySlider.Value;
				maxVeloctyLabel.Text = $"Max velocity: {atService.MaxVelocity:G3}";
			}
		}

		private void ForwardTargetSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.UserForwardTarget = forwardTargetSlider.Value;
				forwardTargetLabel.Text = $"Forward Target: {atService.UserForwardTarget * 100f:G3}%";
			}
		}

		private void BackwardTargetSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.UserBackwardTarget = backwardTargetSlider.Value;
				backwardTargetLabel.Text = $"Backward Target: {atService.UserBackwardTarget * 100f:G3}%";
			}
		}

		private void ConfigureSlider(DrawableSlider slider)
		{
			slider.PreferredSize = new Vector2(150f, 15f);
		}

		public void InitAutoThrust(AutoThrustFeature.AutoThrustService atService)
		{
			this.atService = atService;
			if (atService != null)
			{
				atLayout.Visible = true;
			}
		}

		public void InitVelocityControl(VelocityControlFeature.VelocityControlService asService)
		{
			this.asService = asService;
			if (asService != null)
			{
				asLayout.Visible = true;
				maxVelocitySlider.MaxValue = 2f;
			}
		}

		public void Refresh()
		{
			if (atService != null)
			{
				if (maxVelocitySlider.Value != atService.MaxVelocity)
				{
					maxVelocitySlider.Value = atService.MaxVelocity;
				}
				if (userThrustSlider.Value != atService.ThrustBalance)
				{
					userThrustSlider.Value = atService.UserThrustBalance;
				}
				activeVelocitySlider.MaxValue = maxVelocitySlider.Value;
				if (atService.Sequence != null)
				{
					if (activeVelocitySlider.Value != atService.Sequence.Velocity)
					{
						activeVelocityLabel.Text = $"Active velocity: {atService.Sequence.Velocity:G3}";
						activeVelocitySlider.Value = atService.Sequence.Velocity;
					}
					if (activeThrustSlider.Value != atService.ThrustBalance)
					{
						activeThrustLabel.Text = $"Active balance: {atService.ThrustBalance:G3}";
						activeThrustSlider.Value = atService.ThrustBalance;
					}
					activePRatioSlider.Value = atService.Sequence.MaxPRatio;
				}
				else
				{
					activeVelocityLabel.Text = "Active velocity: N/A";
					activeVelocitySlider.Value = 0f;
					activeThrustLabel.Text = "Active balance: N/A";
					activeThrustSlider.Value = 0f;
				}
			}
			if (asService != null)
			{
				if (asService.MaxVelocity > 0f)
				{
					speedSlider.MaxValue = asService.MaxVelocity;
				}
				else
				{
					speedSlider.MaxValue = 1f;
				}
				if (speedSlider.Value != asService.Velocity)
				{
					speedSlider.Value = asService.Velocity;
				}
				if (depthSlider.Value != asService.Depth)
				{
					depthSlider.Value = asService.Depth;
				}
			}
		}
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<KeyboardShortcut> missionControlHotkey;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		enableFeature = config.Bind<bool>("Features", "EnableMissionControl", true, "Enable MissionControl: all-in-one motion control window");
		missionControlHotkey = config.Bind<KeyboardShortcut>("MissionControl", "Hotkey", new KeyboardShortcut(KeyCode.F6, Array.Empty<KeyCode>()), "Mission control: hotkey");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
		Lookup<PluginOptionsService>().Expose(missionControlHotkey, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new MissionControlService
		{
			Hotkey = missionControlHotkey
		});
	}
}
