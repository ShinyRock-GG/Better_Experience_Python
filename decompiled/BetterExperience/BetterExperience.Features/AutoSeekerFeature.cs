using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.AI.Reactores.Effector;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ai.Reactores.Orales;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets.TValle.BeachGirl;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Features;

internal class AutoSeekerFeature : PluginFeature
{
	private class AutoSeekerService : SessionService
	{
		private const float TRANSLATION_SPEED = 0.1f;

		private const int MAX_SOLVABLE_V_ANGLE = 80;

		private const float TRANSLATION_PRECISSION = 0.005f;

		private IInputHandle hotkey;

		private PelvisMovementController ctl;

		private AutoplacerState state;

		private Vector3 pelvisTarget;

		private List<Behaviour> unwantedBehaviors = new List<Behaviour>();

		private OverlayService overlay;

		private AutoThrustFeature.AutoThrustService autoThruster;

		private bool autoscale;

		public float MaxDepth
		{
			get
			{
				if (autoThruster == null)
				{
					return 0.3f;
				}
				return autoThruster.MaxDepth / 2f;
			}
		}

		public ConfigEntry<KeyboardShortcut> HotkeyCfg { get; internal set; }

		public ConfigEntry<bool> Autothrust { get; internal set; }

		public override void OnStart()
		{
			base.OnStart();
			overlay = Lookup<OverlayService>();
			DispatcherService disp = Lookup<DispatcherService>();
			disp.DoUpdate.Add(OnUpdate, base.Scope);
			hotkey = disp.Input.KeyboardEvent(HotkeyCfg, base.Scope);
			ctl = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.vagHole.entrada);
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.anusHole.entrada);
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.bocaHole.entrada);
			base.Scope.EventHandler(delegate(UpdatingPelvisPosition h)
			{
				ctl.updatingPelvisPosition += h;
			}, delegate(UpdatingPelvisPosition h)
			{
				ctl.updatingPelvisPosition -= h;
			}, OnUpdatingPelvisPosition);
			InitUnwantedBehaviors();
			autoThruster = TryLookup<AutoThrustFeature.AutoThrustService>();
			autoscale = TryLookup<PlayerScaler.ScalerService>() != null;
		}

		private void InitUnwantedBehaviors()
		{
			ReactorConEffectorAEstimulosTactiles[] componentsInChildren = base.Session.Guest.Impl.GetComponentsInChildren<ReactorConEffectorAEstimulosTactiles>();
			foreach (ReactorConEffectorAEstimulosTactiles c in componentsInChildren)
			{
				unwantedBehaviors.Add(c);
			}
			ReactorSexAtPorVerPene[] componentsInChildren2 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorVerPene>();
			foreach (ReactorSexAtPorVerPene c2 in componentsInChildren2)
			{
				unwantedBehaviors.Add(c2);
			}
			ReactorSexAtPorTocarPene[] componentsInChildren3 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorTocarPene>();
			foreach (ReactorSexAtPorTocarPene c3 in componentsInChildren3)
			{
				unwantedBehaviors.Add(c3);
			}
			ReactorSexAtPorSerPenetrada[] componentsInChildren4 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorSerPenetrada>();
			foreach (ReactorSexAtPorSerPenetrada c4 in componentsInChildren4)
			{
				unwantedBehaviors.Add(c4);
			}
		}

		private void OnUpdatingPelvisPosition(ref Vector3 currentLocalTarget, Transform effectorTransform, PelvisMovementController sender)
		{
			pelvisTarget = currentLocalTarget;
		}

		private void SetScriptsEnabled(bool enabled)
		{
			foreach (Behaviour ub in unwantedBehaviors)
			{
				ub.enabled = enabled;
			}
		}

		private void OnUpdate()
		{
			MaleChar c = base.Session.Player.Character;
			if (state != null)
			{
				if (state.ExitReason == ExitReason.None)
				{
					if (hotkey.Up && hotkey.Duration < 2f)
					{
						state.ExitReason = ExitReason.Manual;
					}
					else if (c.pene.isPenetrating)
					{
						state.ExitReason = ExitReason.Completed;
					}
					else if (c.pene.IsBlocked())
					{
						state.ExitReason = ExitReason.NoTool;
					}
				}
				if (state.ExitReason != ExitReason.None)
				{
					SetScriptsEnabled(enabled: true);
					if (state.ExitReason == ExitReason.VerticalAngleTooWide)
					{
						overlay.InfoMessage("Auto-seek sequence stopped: too wide angle");
					}
					else if (state.ExitReason == ExitReason.UnreachableTarget)
					{
						overlay.InfoMessage("Auto-seek sequence stopped: unreachable target [ is player too short? ]");
					}
					else
					{
						if (state.ExitReason == ExitReason.Completed && autoThruster != null && Autothrust.Value)
						{
							autoThruster.TryStartSequence();
						}
						overlay.InfoMessage("Auto-seek sequence stopped");
					}
					state = null;
				}
				else
				{
					Tracer.DrawTransform(state.Hole);
					UpdatePlacement();
				}
			}
			else if (hotkey.Up && hotkey.Duration < 2f && !c.pene.isPenetrating && !c.pene.IsBlocked())
			{
				state = new AutoplacerState();
				state.RootTransform = base.Session.Player.RootMotion;
				SetScriptsEnabled(enabled: false);
				overlay.InfoMessage("Auto-seek sequence started {0}s", hotkey.Duration);
				state.Hole = GetClosestHole();
			}
		}

		private void UpdatePlacement()
		{
			Vector3 targetLocal = state.RootTransform.InverseTransformPoint(state.Hole.position);
			Transform transform = ((IPene)base.Session.Player.Character.pene).partePunta;
			state.TranslateInto = targetLocal - state.RootTransform.InverseTransformPoint(transform.position);
			Tracer.DrawTransform(state.RootTransform);
			if (!state.ResetComplete)
			{
				if (Mathf.Abs(pelvisTarget.z) > 0.005f)
				{
					float dv = Mathf.Clamp(0f - pelvisTarget.z, -0.01f, 0.01f);
					ctl.AddProfundidadDelta(dv);
					return;
				}
				state.ResetComplete = true;
				if (autoscale)
				{
					base.Session.Player.ResetScale();
				}
			}
			if (UpdateRotation())
			{
				return;
			}
			Transform rootmotion = base.Session.Player.RootMotion;
			Vector3 dp = state.TranslateInto;
			float vangle = UnityUtils.FromToAxisAngle(transform.forward, -state.Hole.forward, rootmotion.right);
			dp.z += base.Session.Player.Character.pene.worldTipPartLength * 0.1f;
			if (dp.y + pelvisTarget.y > 0.2f)
			{
				state.ExitReason = ExitReason.UnreachableTarget;
				return;
			}
			bool fixY = Mathf.Abs(dp.y) > 0.005f;
			bool fixX = Mathf.Abs(dp.x) > 0.005f;
			if (!state.FinalStage && (fixY || fixX) && dp.z < 0.1f)
			{
				dp.z -= 1f;
			}
			if (dp.z > 0f && !state.FinalStage)
			{
				if (fixY)
				{
					if (dp.y < 0f || pelvisTarget.y < 0f)
					{
						float delta = dp.y * Mathf.Min(1f, Time.deltaTime * 5f);
						ctl.AddVerticalDelta(delta);
					}
					else if (autoscale)
					{
						Vector3 scale = Vector3.up * 0.1f * Time.deltaTime;
						base.Session.Player.AddScale(scale);
					}
					else
					{
						state.ExitReason = ExitReason.UnreachableTarget;
					}
					return;
				}
				if (Mathf.Abs(vangle) > 1f && (vangle < 0f || pelvisTarget.y < 0f) && Mathf.Abs(pelvisTarget.z) < MaxDepth)
				{
					float limit = Mathf.Abs(vangle) / 100f;
					limit = Mathf.Max(limit, 0.01f);
					float v = Mathf.Clamp(vangle, 0f - limit, limit);
					ctl.AddProfundidadDelta((0f - v) * Time.deltaTime);
					return;
				}
				if (fixX)
				{
					float dv2 = TranslateTowards(dp.x);
					base.Session.Player.Move(new Vector3(dv2, 0f, 0f));
					return;
				}
				state.FinalStage = true;
			}
			if (Mathf.Abs(dp.z) > 0f)
			{
				float dv3 = TranslateTowards(dp.z, 0.05f);
				base.Session.Player.Move(new Vector3(0f, 0f, dv3));
			}
		}

		private float TranslateTowards(float target, float dampAt = 0f)
		{
			float maxDelta = 0.1f * Time.deltaTime;
			if (Mathf.Abs(target) < dampAt)
			{
				maxDelta /= 10f;
			}
			return Mathf.MoveTowards(0f, target, maxDelta);
		}

		private bool UpdateRotation()
		{
			Transform root = base.Session.Player.RootMotion;
			float orientation = UnityUtils.FromToAxisAngle(root.forward, -state.Hole.forward, root.right);
			if (Mathf.Abs(orientation) > 80f)
			{
				state.ExitReason = ExitReason.VerticalAngleTooWide;
				return true;
			}
			float angle = UnityUtils.FromToAxisAngle(root.forward, -state.Hole.forward, root.up);
			if (Mathf.Abs(angle) < 1f)
			{
				return false;
			}
			base.Session.Player.Rotate(Mathf.MoveTowards(0f, angle, 50f * Time.deltaTime));
			return true;
		}

		private Transform GetClosestHole()
		{
			FemaleChar ch = base.Session.Guest.Impl;
			IPene pene = base.Session.Player.Character.peneDeCharacter;
			Transform hole = base.Session.Guest.Puppet.GetIKBoneTransform(ch.vagHole.entrada);
			float distance = Vector3.Distance(pene.partePunta.position, hole.position);
			TestTransform(ch.anusHole.entrada, ref distance, ref hole);
			TestTransform(ch.bocaHole.entrada, ref distance, ref hole);
			return hole;
		}

		private void TestTransform(Transform t, ref float distance, ref Transform hole)
		{
			IPene pene = base.Session.Player.Character.peneDeCharacter;
			t = base.Session.Guest.Puppet.GetIKBoneTransform(t);
			float tmpdistance = Vector3.Distance(pene.partePunta.position, t.position);
			if (tmpdistance < distance)
			{
				distance = tmpdistance;
				hole = t;
			}
		}
	}

	private class AutoplacerState
	{
		public Transform Hole { get; set; }

		public Transform RootTransform { get; set; }

		public Vector3 TranslateInto { get; internal set; }

		public ExitReason ExitReason { get; set; }

		public bool FinalStage { get; internal set; }

		public bool ResetComplete { get; set; }

		public float GetRotation2()
		{
			float angle = UnityUtils.ToEuler(Quaternion.LookRotation(-Hole.forward, RootTransform.up)).y;
			float angle2 = UnityUtils.ToEuler(Quaternion.LookRotation(RootTransform.forward, RootTransform.up)).y;
			float targetAngle = angle - angle2;
			float absAngle = Mathf.Abs(targetAngle);
			if (absAngle < 90f && absAngle > 1f)
			{
				return targetAngle;
			}
			return 0f;
		}

		public float GetRotation()
		{
			float angle2 = UnityUtils.FromToAxisAngle(RootTransform.forward, -Hole.forward, RootTransform.forward);
			return UnityUtils.FromToAxisAngle(RootTransform.forward, -Hole.forward, RootTransform.up);
		}
	}

	private enum ExitReason
	{
		None,
		UnreachableTarget,
		VerticalAngleTooWide,
		Manual,
		Completed,
		NoTool
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<KeyboardShortcut> hotkey;

	private ConfigEntry<bool> autothrust;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		enableFeature = config.Bind<bool>("Features", "AutoSeekerEnabled", true, "Enable auto seeker: automated hole seeker");
		hotkey = config.Bind<KeyboardShortcut>("AutoSeeker", "Hotkey", new KeyboardShortcut(KeyCode.Space, Array.Empty<KeyCode>()), "Autoplacer: start/stop hotkey");
		autothrust = config.Bind<bool>("AutoSeeker", "EnableAutothrust", true, "Auto seeker: Start auto-thrust sequence when ready");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(autothrust, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(hotkey, base.Scope, PluginOptionsService.SettingsType.player);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new AutoSeekerService
		{
			HotkeyCfg = hotkey,
			Autothrust = autothrust
		});
	}
}
