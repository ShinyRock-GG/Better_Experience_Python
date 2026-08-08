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
		/// <summary>
		/// Show the developer instruments and experiments. FALSE for release.
		///
		/// A compile-time constant rather than a config entry on purpose: these drive self-running
		/// experiments that take over the alignment DOF, and a player who finds them in a .cfg and
		/// switches one on gets a character doing a calibration sweep with no idea why. Anyone who
		/// needs them is already rebuilding.
		///
		/// The widgets are the ONLY thing this hides — every field, handler and underlying feature
		/// is untouched, so flipping it restores the full panel exactly as it was.
		/// </summary>
		private const bool ShowDevControls = false;

		private HLayout<Drawable> layout;

		private DrawableToggle maxVeloctyLabel;

		private DrawableSlider maxVelocitySlider;

		private DrawableLabel userThrustLabel;

		private DrawableLabel forwardTargetLabel;

		private DrawableLabel backwardTargetLabel;

		private DrawableSlider backwardTargetSlider;

		private DrawableLabel maxBendLabel;

		private DrawableSlider maxBendSlider;

		private DrawableLabel punchLabel;

		private DrawableSlider punchSlider;

		private DrawableToggle alignToggle;

		private DrawableToggle alignLateralToggle;

		private DrawableToggle alignTestToggle;

		private DrawableToggle freeCalToggle;

		private DrawableToggle alignCoarseToggle;

		private DrawableToggle strokeStraightToggle;

		private DrawableToggle angleDebugToggle;

		private DrawableToggle strokeAuditToggle;

		private DrawableLabel auditStatusLabel;

		private DrawableLabel seekSpeedLabel;

		private DrawableSlider seekSpeedSlider;

		// SEEKLAB: dock-cycle dials, previously probe-only.
		private DrawableToggle outAxisToggle;

		private DrawableLabel bocaTiltLabel;

		private DrawableSlider bocaTiltSlider;

		private DrawableLabel bocaOutLabel;

		private DrawableSlider bocaOutSlider;

		private DrawableLabel alignLabel;

		private DrawableSlider alignSlider;

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

		private VLayout<Drawable> roLayout;

		private VelocityControlFeature.VelocityControlService asService;

		private AutoThrustFeature.AutoThrustService atService;

		private DrawableLabel depthLabel;

		private DrawableSlider activePRatioSlider;

		private DrawableToggle activePRatioLabel;

		public MissionControlWindow()
			// Narrower, and TALLER than the content needs. The old 560x390 clipped: everything
			// below "Seek speed" — the align-gain slider included — was simply never on screen,
			// which is why controls could be added and nobody could tell. Two columns retained.
			: base(470, 430)
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
			// How far the shaft may bow before the stroke throttles itself. Measured from the
			// game's own straight-vs-current length pair, so it self-adjusts to the character's
			// pene stiffness (rigidez) — no per-character tuning needed.
			maxBendLabel = atLayout.Label("Max bend: 20%");
			maxBendSlider = atLayout.HSlider(0.2f, 0.01f, 0.5f);
			ConfigureSlider(maxBendSlider);
			// Punch: deliberate over-reach PAST Forward 100 %, up to +30 %. Forward 100 % is fully
			// seated, so anything beyond it is opted into here rather than happening by accident.
			punchLabel = atLayout.Label("Punch: +20%");
			punchSlider = atLayout.HSlider(0.2f, 0f, 0.3f);
			ConfigureSlider(punchSlider);
			// Hip alignment assist: corrects the angle between the approach axis and the hole
			// instead of merely throttling the bend it causes. Second driver on the pelvis, so it
			// defaults OFF and yields to manual C/V.
			alignToggle = atLayout.Toggle("Align hips");
			alignToggle.OnValueChanged += AlignToggle_OnValueChanged;
			// Lateral moves the AVATAR rather than the hips, so it is a separate opt-in.
			alignLateralToggle = atLayout.Toggle("Align lateral");
			alignLateralToggle.OnValueChanged += AlignLateralToggle_OnValueChanged;
			// ── DEVELOPER CONTROLS ───────────────────────────────────────────────────────────
			//
			// Instruments and experiments, hidden for release. STUBBED, NOT DELETED: the widgets
			// are simply not created, while every field, handler and the features behind them stay
			// exactly as they are. Flip ShowDevControls to get the full panel back — that is the
			// whole difference, so a debugging session costs a rebuild rather than a rewrite.
			//
			// They are hidden rather than shipped because each one either drives a self-running
			// experiment that seizes the alignment DOF (Align TEST, Stroke AUDIT, Free CALIBRATE)
			// or exposes an unresolved question (Straight-line stroke, Align COARSE). None of them
			// are things a player should be discovering by accident.
			if (ShowDevControls)
			{
				// EXPERIMENT (ALIGNMENT_THEORY.md): self-driving calibrate-then-solve probe. Takes
				// exclusive control of the alignment DOF while it runs, and logs [ALIGNTEST] lines.
				alignTestToggle = atLayout.Toggle("Align TEST (probe)");
				alignTestToggle.OnValueChanged += AlignTestToggle_OnValueChanged;
				// Free-space characterisation: needs NO penetration and no partner. Sweeps each
				// pelvis axis through its full range and logs the character's kinematic map.
				freeCalToggle = atLayout.Toggle("Free CALIBRATE (no sex)");
				freeCalToggle.OnValueChanged += FreeCalToggle_OnValueChanged;
				// COARSE placement: avatar moves to put the base on the hole's axis at half a pene
				// length out, so the hips can fine-tune from a near-neutral pose.
				alignCoarseToggle = atLayout.Toggle("Align COARSE (avatar)");
				alignCoarseToggle.OnValueChanged += AlignCoarseToggle_OnValueChanged;
				// STRAIGHT-LINE STROKE: split the stroke command across all three hip axes so the
				// motion travels along the hole's axis instead of the pelvis's own z.
				strokeStraightToggle = atLayout.Toggle("Straight-line stroke");
				strokeStraightToggle.OnValueChanged += StrokeStraightToggle_OnValueChanged;
				// ANGLE readout: prints the hole axis and the shaft axis side by side.
				angleDebugToggle = atLayout.Toggle("ANGLE readout");
				angleDebugToggle.OnValueChanged += AngleDebugToggle_OnValueChanged;
				// AUDIT: drives the align toggles itself for the duration, then restores them.
				strokeAuditToggle = atLayout.Toggle("Stroke AUDIT (A/B)");
				strokeAuditToggle.OnValueChanged += StrokeAuditToggle_OnValueChanged;
			}
			// SEEK SPEED, up to 8x deliberately. A placement loop that still converges at 8x has
			// real stability margin; one that only works at 1x is sitting on the edge and will
			// break the moment anything else changes. Find where it breaks, then back off — that
			// gap IS the margin. Live: the slider writes the static the seeker reads each tick.
			// Initialised FROM the live value, not from a literal: a slider that reads 1.00x while
			// the seeker runs at 4x is a lying instrument, and worse, the first drag would snap the
			// speed down to whatever the slider happened to be showing.
			seekSpeedLabel = atLayout.Label("Seek speed: "
				+ AutoSeekTuning.SpeedScale.ToString("F2") + "x");
			seekSpeedSlider = atLayout.HSlider(AutoSeekTuning.SpeedScale, 0.25f, 8f);
			ConfigureSlider(seekSpeedSlider);
			alignLabel = atLayout.Label("Align gain: 50%");
			alignSlider = atLayout.HSlider(0.5f, 0.05f, 1f);
			ConfigureSlider(alignSlider);

			// SEEKLAB: the dials added by the dock-cycle work. They were probe-only (T: paths),
			// which is fine for me and useless at the keyboard — a tunable nobody can reach while
			// playing does not get tuned. All three are live statics, so they take effect mid-seek.
			//
			// Every one of these is an OPEN QUESTION rather than a settled constant, which is
			// exactly why they belong on the panel: the axis experiment has never been resolved,
			// and the boca values were dialled against geometry that has since changed.
			if (ShowDevControls)
			{
				outAxisToggle = atLayout.Toggle("Axis: worldOutHole (else bone)");
				outAxisToggle.Value = AutoSeekTuning.UseOutHoleAxis;
				outAxisToggle.OnValueChanged += OutAxisToggle_OnValueChanged;

				bocaTiltLabel = atLayout.Label("Boca tilt: "
					+ AutoSeekTuning.BocaUpTilt.ToString("F1") + "deg");
				bocaTiltSlider = atLayout.HSlider(AutoSeekTuning.BocaUpTilt, -15f, 15f);
				ConfigureSlider(bocaTiltSlider);
				bocaTiltSlider.OnValueChange += BocaTiltSlider_OnValueChange;

				bocaOutLabel = atLayout.Label("Boca lip offset: "
					+ (AutoSeekTuning.BocaTargetOut * 100f).ToString("F1") + "cm");
				bocaOutSlider = atLayout.HSlider(AutoSeekTuning.BocaTargetOut, 0f, 0.05f);
				ConfigureSlider(bocaOutSlider);
				bocaOutSlider.OnValueChange += BocaOutSlider_OnValueChange;
			}
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
			// READ-ONLY TELEMETRY gets its OWN column. It reports AutoThrust state, so it must
			// appear whenever AutoThrust does — putting it inside the Guest column made Guest
			// visible without a VelocityControlService behind it, and those sliders then moved
			// but never took effect, reading "default" forever.
			roLayout = layout.VLayout();
			roLayout.Label("Read-only values:");
			// Audit progress. The run takes minutes and previously signalled completion only via
			// a log line, which cannot be seen while playing. Follows its toggle: with the audit
			// hidden there is nothing for this to report, and a permanent "AUDIT: off" in a
			// release panel is just a question the player cannot answer.
			if (ShowDevControls)
			{
				auditStatusLabel = roLayout.Label("AUDIT: off");
			}
			activeVelocityLabel = roLayout.Toggle("Active velocity:");
			activeVelocityLabel.OnValueChanged += ActiveVelocityLabel_OnValueChanged;
			activeVelocitySlider = roLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activeVelocitySlider);
			activeThrustLabel = roLayout.Label("Active thrust:");
			activeThrustSlider = roLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activeThrustSlider);
			activePRatioLabel = roLayout.Toggle("Active P-ratio:");
			activePRatioLabel.OnValueChanged += ActivePRatioLabel_OnValueChanged;
			activePRatioSlider = roLayout.HSlider(0f, 0f, 1f);
			ConfigureSlider(activePRatioSlider);
			roLayout.Visible = false;
			maxVelocitySlider.OnValueChange += VelocitySlider_OnValueChange;
			userThrustSlider.OnValueChange += ThrustSlider_OnValueChange;
			forwardTargetSlider.OnValueChange += ForwardTargetSlider_OnValueChange;
			backwardTargetSlider.OnValueChange += BackwardTargetSlider_OnValueChange;
			maxBendSlider.OnValueChange += MaxBendSlider_OnValueChange;
			punchSlider.OnValueChange += PunchSlider_OnValueChange;
			seekSpeedSlider.OnValueChange += SeekSpeedSlider_OnValueChange;
			alignSlider.OnValueChange += AlignSlider_OnValueChange;
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

		private void AlignToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.AlignHips = alignToggle.Value;
			}
		}

		private void AngleDebugToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.AngleDebug = angleDebugToggle.Value;
			}
		}


		private void StrokeAuditToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.StrokeAudit = strokeAuditToggle.Value;
			}
		}

		private void StrokeStraightToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.StrokeStraight = strokeStraightToggle.Value;
			}
		}

		private void AlignCoarseToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.AlignCoarse = alignCoarseToggle.Value;
			}
		}

		private void FreeCalToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.FreeCal = freeCalToggle.Value;
			}
		}

		private void AlignTestToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.AlignTest = alignTestToggle.Value;
			}
		}

		private void AlignLateralToggle_OnValueChanged()
		{
			if (atService != null)
			{
				atService.AlignLateral = alignLateralToggle.Value;
			}
		}

		private void AlignSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.AlignGain = alignSlider.Value;
				alignLabel.Text = $"Align gain: {atService.AlignGain * 100f:G3}%";
			}
		}

		private void OutAxisToggle_OnValueChanged()
		{
			AutoSeekTuning.UseOutHoleAxis = outAxisToggle.Value;
		}

		private void BocaTiltSlider_OnValueChange()
		{
			// Signed: negative is the other direction, which is the whole point of exposing it —
			// the correct sign has been derived wrong twice and observed right in seconds.
			AutoSeekTuning.BocaUpTilt = bocaTiltSlider.Value;
			bocaTiltLabel.Text = "Boca tilt: " + bocaTiltSlider.Value.ToString("F1") + "deg";
		}

		private void BocaOutSlider_OnValueChange()
		{
			AutoSeekTuning.BocaTargetOut = bocaOutSlider.Value;
			bocaOutLabel.Text = "Boca lip offset: "
				+ (bocaOutSlider.Value * 100f).ToString("F1") + "cm";
		}

		private void SeekSpeedSlider_OnValueChange()
		{
			// Writes the static the seeker reads every tick, so it takes effect mid-seek.
			AutoSeekTuning.SpeedScale = seekSpeedSlider.Value;
			seekSpeedLabel.Text = "Seek speed: " + seekSpeedSlider.Value.ToString("F2") + "x";
		}

		private void PunchSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.PunchFraction = punchSlider.Value;
				punchLabel.Text = $"Punch: +{atService.PunchFraction * 100f:G3}%";
			}
		}

		private void MaxBendSlider_OnValueChange()
		{
			if (atService != null)
			{
				atService.MaxBendFraction = maxBendSlider.Value;
				maxBendLabel.Text = $"Max bend: {atService.MaxBendFraction * 100f:G3}%";
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
				// Read-only telemetry is AutoThrust's, so it shows with AutoThrust — and it is its
				// OWN column, so the Guest column stays hidden unless VelocityControl exists.
				roLayout.Visible = true;
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
				// Null when the dev controls are hidden — the widgets are never created. Refresh
				// runs every frame the window is open, so this is the one place the stubbing has
				// to be handled rather than simply not reached; the handlers are safe by
				// construction because a widget that does not exist cannot raise an event.
				if (auditStatusLabel != null)
				{
					auditStatusLabel.Text = atService.AuditStatus;
				}
				// The audit clears its own flag when it finishes; keep the toggle honest so a
				// completed run does not look like one still going.
				if (strokeAuditToggle != null && strokeAuditToggle.Value != atService.StrokeAudit)
				{
					strokeAuditToggle.Value = atService.StrokeAudit;
				}
				if (maxVelocitySlider.Value != atService.MaxVelocity)
				{
					maxVelocitySlider.Value = atService.MaxVelocity;
				}
				if (userThrustSlider.Value != atService.ThrustBalance)
				{
					userThrustSlider.Value = atService.UserThrustBalance;
				}
				if (maxBendSlider.Value != atService.MaxBendFraction)
				{
					maxBendSlider.Value = atService.MaxBendFraction;
				}
				if (punchSlider.Value != atService.PunchFraction)
				{
					punchSlider.Value = atService.PunchFraction;
				}
				if (alignSlider.Value != atService.AlignGain)
				{
					alignSlider.Value = atService.AlignGain;
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
