using System;
using System.Runtime.CompilerServices;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using HarmonyLib;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.Features;

internal class PlayerPostureFeature : PluginFeature
{
	public class PlayerPostureService : SessionService
	{
		private LocalEffectorOffset offset;

		private IInputHandle up;

		private IInputHandle down;

		private IInputHandle alt;

		private IInputHandle shift;

		private float maxBending = 1f;

		private float bending;

		private float maxBendingLocal;

		private bool bendingNow;

		private bool pelvisGrounded;

		private bool isSitting;

		private Vector3 leftFootOffset;

		private Vector3 rightFootOffset;

		private Vector3 leftFootOffsetTarget;

		private Vector3 rightFootOffsetTarget;

		private float velocity = 1f;

		private CapsuleCollider hipsCapsule;

		private PelvisRaycast legsRaycast = new PelvisRaycast();

		private float legLength;

		private float PelvisGroundingDistance { get; } = 0.135f;

		private float PevlisGroundingThreshold { get; } = 0.05f;

		private float PevlisDeGroundingThreshold { get; } = 0.3f;

		private int GroundingLayerMask { get; set; } = 1;

		private float GroundingRaycastDistance { get; set; } = 1f;

		public ConfigEntry<bool> EnableBending { get; internal set; }

		public ConfigEntry<KeyboardShortcut> UpKey { get; internal set; }

		public ConfigEntry<KeyboardShortcut> DownKey { get; internal set; }

		public ConfigEntry<bool> EnableFixHandsOffset { get; internal set; }

		public ConfigEntry<bool> EnableSitIK { get; internal set; }

		public ConfigEntry<bool> EnableKneelIK { get; internal set; }

		public unsafe override void OnStart()
		{
			//IL_0073: Unknown result type (might be due to invalid IL or missing references)
			//IL_0099: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Expected O, but got Ref
			base.OnStart();
			offset = base.Session.Player.GameObject.GetComponentInChildren<LocalEffectorOffset>();
			DispatcherService dispatcher = Lookup<DispatcherService>();
			up = dispatcher.Input.KeyboardEvent(UpKey, base.Scope);
			down = dispatcher.Input.KeyboardEvent(DownKey, base.Scope);
			alt = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.LeftAlt, Array.Empty<KeyCode>()), base.Scope);
			shift = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.LeftShift, Array.Empty<KeyCode>()), base.Scope);
			Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
			PelvisMovementController ctl = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			ctl.updatingPelvisPosition += Ctl_updatingPelvisPosition;
			PelvisMovementInterceptor.AfterPelvisIKUpdated.Add(AfterPelvisControllerUpdated);
			// yRange.min mutation via Unsafe.AsPointer not supported in net472 SMA 23.1
			PuppetMaster pm = base.Session.Player.Character.GetComponentInChildren<PuppetMaster>();
			Muscle hips = pm.GetMuscle(HumanBodyBones.Hips);
			Collider hipscollider = hips.colliders[0];
			hipsCapsule = hipscollider as CapsuleCollider;
			Animator animator = base.Session.Player.Character.animator;
			Transform animHips = animator.GetBoneTransform(HumanBodyBones.Hips);
			Transform animLFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
			legLength = animHips.position.y - animLFoot.position.y;
		}

		private void LegsRaycast(Transform effectorTransform)
		{
			legsRaycast.LeftHit = null;
			legsRaycast.RightHit = null;
			Vector3 down = -base.Session.Player.Character.animatorRootMotionTransform.up;
			float halfH = hipsCapsule.height / 1.5f;
			Vector3 offset = effectorTransform.TransformVector(halfH, 0f, 0f);
			if (Physics.Raycast(new Ray(effectorTransform.position - offset, down), out var hit, legLength, GroundingLayerMask))
			{
				legsRaycast.LeftHit = hit;
			}
			if (Physics.Raycast(new Ray(effectorTransform.position + offset, down), out hit, legLength, GroundingLayerMask))
			{
				legsRaycast.RightHit = hit;
			}
		}

		private void Ctl_updatingPelvisPosition(ref Vector3 currentLocalTarget, Transform effectorTransform, PelvisMovementController sender)
		{
			if (base.Session.Player.Character.animator.transform.localRotation != Quaternion.identity)
			{
				pelvisGrounded = false;
				isSitting = false;
				legsRaycast.LeftHit = null;
				legsRaycast.RightHit = null;
				currentLocalTarget.y = 0f;
				return;
			}
			Transform rootmotion = base.Session.Player.Character.animatorRootMotionTransform;
			Vector3 down = -base.Session.Player.Character.animatorRootMotionTransform.up;
			LegsRaycast(effectorTransform);
			bool groundingState;
			if (Physics.Raycast(new Ray(effectorTransform.position, down), out var hit, GroundingRaycastDistance, GroundingLayerMask))
			{
				if (hit.distance <= PelvisGroundingDistance)
				{
					float overflow = PelvisGroundingDistance - hit.distance;
					float pendingOffset = currentLocalTarget.y - offset.leftThighOffset.y;
					overflow -= pendingOffset;
					if (overflow > 0f)
					{
						currentLocalTarget.y += overflow;
					}
				}
				if (hit.distance < PelvisGroundingDistance + PevlisGroundingThreshold)
				{
					groundingState = true;
				}
				else if (hit.distance > PelvisGroundingDistance + PevlisDeGroundingThreshold)
				{
					if (pelvisGrounded && isSitting)
					{
						currentLocalTarget.y = 0f;
					}
					groundingState = false;
				}
				else
				{
					groundingState = false;
				}
			}
			else
			{
				groundingState = false;
			}
			if (!groundingState && pelvisGrounded && isSitting)
			{
				currentLocalTarget.y = 0f;
			}
			pelvisGrounded = groundingState;
			isSitting = false;
			if (!pelvisGrounded)
			{
				return;
			}
			isSitting = true;
			if (Physics.Raycast(effectorTransform.position + rootmotion.forward * 0.5f, down, out var forwardHit, GroundingRaycastDistance, GroundingLayerMask))
			{
				float dh = forwardHit.distance - hit.distance;
				if (dh < 0f || forwardHit.distance < PelvisGroundingDistance)
				{
					isSitting = false;
				}
				else if (forwardHit.distance < PelvisGroundingDistance + PevlisGroundingThreshold * 1.5f)
				{
					isSitting = false;
				}
			}
		}

		private void AfterPelvisControllerUpdated()
		{
			Vector3 vector = (offset.leftThighOffset + offset.rightThighOffset) / 2f;
			float feetOffset = vector.y / 4f;
			float yoffset = legLength + vector.y;
			if (pelvisGrounded && isSitting && EnableSitIK.Value)
			{
				float xfactor = 2f;
				float zfactor = 3f;
				float zoffset = Mathf.Min(0.5f, (0f - feetOffset) * zfactor);
				leftFootOffsetTarget = new Vector3(feetOffset * xfactor, 0f, zoffset);
				rightFootOffsetTarget = new Vector3((0f - feetOffset) * xfactor, 0f, zoffset);
				velocity = 1f;
			}
			else if (legsRaycast.AnyHit && EnableKneelIK.Value)
			{
				float zoffset2 = -0.25f;
				float xoffset = -0f;
				float lFeetOffsetY = (legsRaycast.LeftHit.HasValue ? (yoffset - legsRaycast.LeftHit.Value.distance) : 0f);
				float rFeetOffsetY = (legsRaycast.RightHit.HasValue ? (yoffset - legsRaycast.RightHit.Value.distance) : 0f);
				float lZOffsetFactor = ((lFeetOffsetY < 0.5f * legLength) ? 1 : 0);
				float rZOffsetFactor = ((rFeetOffsetY < 0.5f * legLength) ? 1 : 0);
				if (lFeetOffsetY != 0f)
				{
					leftFootOffsetTarget = new Vector3(xoffset, lFeetOffsetY, zoffset2 * lZOffsetFactor);
				}
				else
				{
					leftFootOffsetTarget = new Vector3(feetOffset / 2f, 0f, feetOffset);
				}
				if (rFeetOffsetY != 0f)
				{
					rightFootOffsetTarget = new Vector3(0f - xoffset, rFeetOffsetY, zoffset2 * rZOffsetFactor);
				}
				else
				{
					rightFootOffsetTarget = new Vector3((0f - feetOffset) / 2f, 0f, feetOffset);
				}
				velocity = 1f;
			}
			else
			{
				leftFootOffsetTarget = new Vector3(feetOffset / 2f, 0f, feetOffset);
				rightFootOffsetTarget = new Vector3((0f - feetOffset) / 2f, 0f, feetOffset);
				velocity = 2f;
			}
			leftFootOffset = Vector3.MoveTowards(leftFootOffset, leftFootOffsetTarget, Time.deltaTime * velocity);
			rightFootOffset = Vector3.MoveTowards(rightFootOffset, rightFootOffsetTarget, Time.deltaTime * velocity);
			offset.leftFootOffset = leftFootOffset;
			offset.rightFootOffset = rightFootOffset;
			if (EnableFixHandsOffset.Value)
			{
				offset.leftHandOffset = new Vector3(0f, feetOffset, 0f);
				offset.rightHandOffset = new Vector3(0f, feetOffset, 0f);
			}
			offset.leftShoulderOffset = new Vector3(0f, (0f - Mathf.Abs(bending)) / 2f, bending);
			offset.rightShoulderOffset = new Vector3(0f, (0f - Mathf.Abs(bending)) / 2f, bending);
		}

		private void OnUpdate()
		{
			if (!EnableBending.Value)
			{
				bending = 0f;
				bendingNow = false;
				return;
			}
			int direction = 0;
			if (up.IsHold)
			{
				direction = 1;
			}
			else if (down.IsHold)
			{
				direction = -1;
			}
			float scale = 1f;
			if (alt.IsHold)
			{
				scale *= 0.5f;
			}
			if (shift.IsHold)
			{
				scale *= 2f;
			}
			if (direction != 0)
			{
				if (!bendingNow)
				{
					bendingNow = true;
					if (bending != 0f && Math.Sign(bending) != Math.Sign(direction))
					{
						maxBendingLocal = 0f;
					}
					else
					{
						maxBendingLocal = maxBending;
					}
				}
				bending = Mathf.MoveTowards(bending, (float)direction * maxBendingLocal, Time.deltaTime * scale);
			}
			else
			{
				bendingNow = false;
			}
		}
	}

	private class PelvisRaycast
	{
		public RaycastHit? LeftHit { get; internal set; }

		public RaycastHit? RightHit { get; internal set; }

		public bool AnyHit
		{
			get
			{
				if (!LeftHit.HasValue)
				{
					return RightHit.HasValue;
				}
				return true;
			}
		}
	}

	public static class PelvisMovementInterceptor
	{
		public static Observable AfterPelvisIKUpdated { get; } = new Observable();

		[HarmonyPatch(typeof(PelvisMovementController), "OnUpdateEvent1")]
		[HarmonyPostfix]
		public static void After_PelvisMovementController_OnUpdateEvent1()
		{
			AfterPelvisIKUpdated.Invoke();
		}
	}

	private ConfigEntry<bool> configEnableFeature;

	private ConfigEntry<bool> enableBending;

	private ConfigEntry<KeyboardShortcut> upShortcut;

	private ConfigEntry<KeyboardShortcut> downShortcut;

	private ConfigEntry<bool> enableFixHandsOffset;

	private ConfigEntry<bool> enableSitIK;

	private ConfigEntry<bool> enableKneelIK;

	public override bool Enabled => configEnableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		configEnableFeature = config.Bind<bool>("Features", "PlayerPosture", true, "Extended player posing: Enable feature");
		enableBending = config.Bind<bool>("PlayerPosture", "EnableBending", true, "PlayerPosture: Enable bending");
		upShortcut = config.Bind<KeyboardShortcut>("PlayerPosture", "BendFwdKey", new KeyboardShortcut(KeyCode.PageUp, Array.Empty<KeyCode>()), "PlayerPosture: Bend forward");
		downShortcut = config.Bind<KeyboardShortcut>("PlayerPosture", "BendBwdKey", new KeyboardShortcut(KeyCode.PageDown, Array.Empty<KeyCode>()), "PlayerPosture: Bend backward");
		enableFixHandsOffset = config.Bind<bool>("PlayerPosture", "EnableHandsOffset", false, "PlayerPosture: Enable 'fix hands offset' - hands will move down with hips");
		enableSitIK = config.Bind<bool>("PlayerPosture", "EnableSitIK", true, "PlayerPosture: Enable IK-based sit pose");
		enableKneelIK = config.Bind<bool>("PlayerPosture", "EnableKneelIK", false, "PlayerPosture: Enable IK-based kneel pose");
	}

	public override void OnStart()
	{
		base.OnStart();
		Harmony.CreateAndPatchAll(typeof(PelvisMovementInterceptor), (string)null);
		Lookup<SessionTracker>().SessionServices.Add(() => new PlayerPostureService
		{
			EnableBending = enableBending,
			UpKey = upShortcut,
			DownKey = downShortcut,
			EnableFixHandsOffset = enableFixHandsOffset,
			EnableSitIK = enableSitIK,
			EnableKneelIK = enableKneelIK
		});
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(configEnableFeature, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(enableBending, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(upShortcut, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(downShortcut, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(enableFixHandsOffset, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(enableSitIK, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(enableKneelIK, base.Scope, PluginOptionsService.SettingsType.player);
	}
}
