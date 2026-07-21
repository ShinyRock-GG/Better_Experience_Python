using System;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

internal class AutoThrustFeature : PluginFeature
{
	public class AutoThrustService : SessionService
	{
		public class SequenceState
		{
			public float Velocity { get; set; }

			public BoneStretchedChain hole { get; set; }

			public Transform HoleEntrance { get; set; }

			public float MaxPRatio { get; set; }

			public MotionType Motion { get; set; }

			public bool HoleDepthLimit => false; // maximaProfundidadVirtualAlcanzada is [Obsolete] in SMA 23.1

			public bool HoleDiameterLimit => hole.maximaAnchuraVirtualAlcanzada;

			public int Step { get; set; }

			public float NonDeformedExitPRatio { get; set; }

			public float ExitDeformation { get; set; }

			public int Ticks { get; internal set; }

			public bool ExitDueToMotionLimit { get; internal set; }

			public bool RampUpVelocity { get; set; } = true;

			internal float UpdatePRatio(float pRatio)
			{
				if (MaxPRatio > pRatio)
				{
					MaxPRatio = Mathf.Lerp(MaxPRatio, pRatio, 0.1f);
				}
				else
				{
					MaxPRatio = Mathf.Lerp(MaxPRatio, pRatio, 0.3f);
				}
				return MaxPRatio;
			}

			internal void UpdateVelocity(float targetVelocity)
			{
				if (RampUpVelocity)
				{
					Velocity = LerpVelocity(Velocity, targetVelocity);
				}
				else
				{
					Velocity = targetVelocity;
				}
			}

			public float LerpVelocity(float actual, float target)
			{
				float velocity = Mathf.Lerp(actual, target, 0.1f);
				return Mathf.Min(velocity, actual + (float)Math.Sign(target - actual) * Mathf.Min(0.07f, Mathf.Abs(target - actual)));
			}
		}

		private const float MAX_DEFORMATION_FACTOR = 0.6f;

		private OverlayService overlay;

		private IInputHandle hotkeyHandle;

		private PelvisMovementController controller;

		private LocalEffectorOffset controllerOffsets;

		private Traverse<float> controllerSmoothTime;

		private float defaultControllerMaxSpeed;

		private float defaultControllerSmoothTime;

		private PlacerBase pleasure;

		private ConfigEntry<KeyboardShortcut> hotkey;

		private ConfigEntry<bool> useConstantVelocity;

		private ConfigEntry<bool> reduceSmoothTime;

		private ConfigEntry<bool> targetVelocityScale;

		private float lastDepth;

		private float lastTickDepth;

		private bool firstThrust;

		public int DepthLookahead { get; set; } = 1;

		public float MaxDepth { get; set; } = 0.2f;

		public float MaxBalancedVelocity { get; set; } = 0.7f;

		public float MinVelocity { get; set; } = 0.05f;

		public float MaxVelocity { get; set; } = 0.7f;

		public float MaxSafeVelocity { get; set; } = 0.15f;

		public float ThrustBalance { get; set; } = 0.5f;

		public float UserThrustBalance { get; set; } = 0.5f;

		public float UserForwardTarget { get; set; } = 1f;

		public float UserBackwardTarget { get; set; }

		public bool ViolentMode
		{
			get
			{
				return controller.maxSpeed == defaultControllerMaxSpeed;
			}
			set
			{
				if (value)
				{
					controller.maxSpeed = 100f;
				}
				else
				{
					controller.maxSpeed = defaultControllerMaxSpeed;
				}
			}
		}

		private float ImmediateDepth => controllerOffsets.leftThighOffset.z;

		private float MaxWorldPenetration => base.Session.Player.Character.pene.worldLength;

		public SequenceState Sequence { get; private set; }

		public bool VelocityRampUp { get; set; } = true;

		public bool IgnorePRatio { get; set; }

		public AutoThrustService(ConfigEntry<KeyboardShortcut> hotkey, ConfigEntry<bool> useConstantVelocity, ConfigEntry<bool> reduceSmoothTime, ConfigEntry<bool> targetVelocityScale)
		{
			this.hotkey = hotkey;
			this.useConstantVelocity = useConstantVelocity;
			this.reduceSmoothTime = reduceSmoothTime;
			this.targetVelocityScale = targetVelocityScale;
		}

		public override void OnStart()
		{
			base.OnStart();
			overlay = Lookup<OverlayService>();
			hotkeyHandle = Lookup<DispatcherService>().Input.KeyboardEvent(hotkey, base.Scope);
			controller = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			controllerOffsets = Traverse.Create((object)controller).Field<LocalEffectorOffset>("m_effector").Value;
			controllerSmoothTime = Traverse.Create((object)controller).Field<float>("smoothTime");
			defaultControllerMaxSpeed = controller.maxSpeed;
			defaultControllerSmoothTime = controllerSmoothTime.Value;
			Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
			controller.updatingPelvisPosition += Controller_updatingPelvisPosition;
			EmocionesFemeninas e = base.Session.Guest.Impl.GetComponentInChildren<EmocionesFemeninas>();
			pleasure = e.placer;
		}

		public override void OnStop()
		{
			base.OnStop();
			if (controller != null)
			{
				controller.updatingPelvisPosition -= Controller_updatingPelvisPosition;
			}
		}

		private void Controller_updatingPelvisPosition(ref Vector3 currentLocalTarget, Transform effectorTransform, PelvisMovementController sender)
		{
			lastDepth = currentLocalTarget.z;
		}

		private void ResetHipTarget()
		{
			float currentDepth = controllerOffsets.leftThighOffset.z;
			controller.AddProfundidadDelta(currentDepth - lastDepth);
		}

		private void OnUpdate()
		{
			ReactInput();
			if (Sequence != null)
			{
				Process();
			}
		}

		private float GetPenetrationDepth()
		{
			return 0f; // penetracionLocalActual removed in SMA 23.1
		}

		private float GetPenetrationFactor()
		{
			return GetPenetrationDepth() / MaxWorldPenetration;
		}

		private float GetMinPenetrationExpectation()
		{
			Penis pene = base.Session.Player.Character.pene;
			float minWorldPenetration = pene.worldTipPartLength * GetDepenetractionScaleFactor();
			return Mathf.Lerp(minWorldPenetration, MaxWorldPenetration, UserBackwardTarget);
		}

		private float GetMaxPenetrationExpectation()
		{
			return Mathf.Lerp(GetMinPenetrationExpectation(), MaxWorldPenetration * 0.75f, UserForwardTarget);
		}

		private float GetDeformationFactor(float penetrationFactor)
		{
			Penis pene = base.Session.Player.Character.pene;
			float actualWorldLength = (pene.punta.physicBone.position - pene.@base.physicBone.position).magnitude;
			float deformationFactor = actualWorldLength / pene.worldLength;
			deformationFactor = Mathf.InverseLerp(0f, 0.9f - Mathf.InverseLerp(0.7f, 1f, penetrationFactor) * 0.3f, deformationFactor);
			if (Sequence.NonDeformedExitPRatio == 1f)
			{
				deformationFactor = 1f;
			}
			return deformationFactor;
		}

		private float GetPenetrationRatio()
		{
			return Mathf.InverseLerp(GetMinPenetrationExpectation(), GetMaxPenetrationExpectation(), GetPenetrationDepth());
		}

		private float GetDepenetrationThreshold()
		{
			return GetMinPenetrationExpectation() / MaxWorldPenetration;
		}

		private float GetVelocityForPenetrationFactor(float currentFactor, float targetFactor)
		{
			return Mathf.Abs((currentFactor - targetFactor) * (GetMaxPenetrationExpectation() - GetMinPenetrationExpectation()) / Time.deltaTime) / GetThrustScaleFactor();
		}

		private float GetDepenetractionScaleFactor()
		{
			float activeVelocity = GetVelocity(MotionType.OUT);
			return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(MinVelocity, 2f, activeVelocity * GetThrustScaleFactor()));
		}

		private void Process()
		{
			float deltaDepth = ImmediateDepth - lastTickDepth;
			lastTickDepth = ImmediateDepth;
			if (Sequence.Motion == MotionType.NONE)
			{
				Thrust(0.01f);
			}
			Sequence.Ticks++;
			float penetrationFactor = GetPenetrationFactor();
			float deformationFactor = GetDeformationFactor(penetrationFactor);
			if (Sequence.Motion == MotionType.OUT)
			{
				float activeVelocity = GetVelocity(MotionType.OUT);
				float depenetrationThreshold = GetDepenetrationThreshold();
				float maxVelocity = GetVelocityForPenetrationFactor(penetrationFactor, depenetrationThreshold);
				float inVelocity = GetVelocity(MotionType.IN);
				if (activeVelocity > MaxBalancedVelocity)
				{
					activeVelocity = Mathf.Lerp(MaxBalancedVelocity, activeVelocity, penetrationFactor * penetrationFactor * penetrationFactor);
				}
				else if (inVelocity < activeVelocity)
				{
					activeVelocity = Mathf.Lerp(inVelocity, activeVelocity, penetrationFactor);
				}
				activeVelocity = Mathf.Min(activeVelocity, maxVelocity);
				if (penetrationFactor > depenetrationThreshold || deformationFactor < 1f)
				{
					Thrust(0f - activeVelocity);
				}
				else
				{
					Thrust(0.01f);
				}
				return;
			}
			float relPosition = GetRelativeHipsToHoleDistance();
			float penetrationThreshold = GetDepenetrationThreshold() * 1.25f;
			float activeVelocity2 = GetVelocity(MotionType.IN);
			bool atLimit = Sequence.HoleDepthLimit && penetrationFactor > penetrationThreshold;
			float pRatio = GetPenetrationRatio();
			if (Sequence.HoleDiameterLimit && (double)deformationFactor < 0.8)
			{
				if (Sequence.NonDeformedExitPRatio == 0f)
				{
					Sequence.NonDeformedExitPRatio = pRatio;
				}
				if (deformationFactor < 1f)
				{
					activeVelocity2 *= Mathf.Pow(0.5f, deformationFactor);
				}
			}
			if (UserForwardTarget < 1f)
			{
				float maxPF = GetVelocityForPenetrationFactor(penetrationFactor, GetMaxPenetrationExpectation() / MaxWorldPenetration);
				activeVelocity2 = Mathf.Min(activeVelocity2, maxPF);
			}
			_ = MaxSafeVelocity;
			float motionThreshold = 0.00015f;
			float depthLimit = GetRequestedDepth();
			if (ImmediateDepth < depthLimit && (firstThrust || deltaDepth > motionThreshold) && deformationFactor > 0.6f && !atLimit && relPosition > 0f && pRatio < 1f)
			{
				Thrust(activeVelocity2);
				return;
			}
			Sequence.ExitDueToMotionLimit = !(lastDepth < depthLimit) || (!firstThrust && !(deltaDepth > motionThreshold));
			Thrust(-0.01f);
			Sequence.ExitDeformation = deformationFactor;
		}

		private float GetRequestedDepth()
		{
			return MaxDepth;
		}

		private float GetRelativeHipsToHoleDistance()
		{
			Vector3 holePosition = Sequence.HoleEntrance.position;
			Vector3 hipsPosition = base.Session.Player.Character.pene.@base.physicBone.position;
			Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
			holePosition = rm.InverseTransformPoint(holePosition);
			hipsPosition = rm.InverseTransformPoint(hipsPosition);
			Vector3 v1 = Vector3.Project(holePosition, Vector3.forward);
			Vector3 v2 = Vector3.Project(hipsPosition, Vector3.forward);
			return v1.z - v2.z;
		}

		private void Thrust(float dv)
		{
			MotionType req = ((dv > 0f) ? MotionType.IN : MotionType.OUT);
			if (req == MotionType.IN)
			{
				firstThrust = Sequence.Motion != MotionType.IN;
			}
			if (Sequence.Motion != req)
			{
				Sequence.Motion = req;
				Sequence.Ticks = 0;
				if (req == MotionType.OUT)
				{
					UpdateVelocitySettings();
				}
				ResetHipTarget();
			}
			float value = dv * GetThrustScaleFactor();
			if (req == MotionType.OUT)
			{
				value = 0f - Mathf.Max(Mathf.Abs(value), MaxSafeVelocity);
			}
			controller.AddProfundidadDelta(value * Time.deltaTime);
		}

		private float GetThrustScaleFactor()
		{
			return GetPerLengthThrustScaleFactor() * base.Session.Player.Character.pene.worldLength / 0.2f;
		}

		private float GetPerLengthThrustScaleFactor()
		{
			if (targetVelocityScale.Value)
			{
				return UserForwardTarget - UserBackwardTarget;
			}
			return 1f;
		}

		private float GetVelocity(MotionType motion)
		{
			float v = Sequence.Velocity * GetVelocityMultiplier(motion);
			if (Math.Abs(v) < MinVelocity)
			{
				return MinVelocity;
			}
			return v;
		}

		private float GetVelocityMultiplier(MotionType motion)
		{
			float s = ((motion != MotionType.IN) ? (1f - Mathf.Clamp(ThrustBalance, 0.5f, 1f)) : Mathf.Clamp(ThrustBalance, 0f, 0.5f));
			s = s * s / 0.25f;
			return Mathf.Max(s, 0.001f);
		}

		private void UpdateVelocitySettings()
		{
			Sequence.Step++;
			Sequence.RampUpVelocity = VelocityRampUp;
			float initialInVelocity = GetVelocity(MotionType.IN);
			float perPleasureVelocity = Mathf.Lerp(MinVelocity, MaxVelocity, pleasure.value.value / 100f);
			float pRatio = GetPenetrationRatio();
			pRatio = Sequence.UpdatePRatio(pRatio);
			float perDepthVelocity = Mathf.Lerp(MinVelocity, MaxVelocity, pRatio);
			if (MaxVelocity > MaxSafeVelocity && perDepthVelocity < MaxSafeVelocity)
			{
				perDepthVelocity = MaxSafeVelocity;
			}
			if (Sequence.ExitDueToMotionLimit && Sequence.ExitDeformation > 0.8f && (pRatio > 0.2f || initialInVelocity > MaxBalancedVelocity))
			{
				perDepthVelocity = MaxVelocity;
			}
			if (IgnorePRatio)
			{
				perDepthVelocity = MaxVelocity;
			}
			float targetVelocity = Mathf.Min(perPleasureVelocity, perDepthVelocity);
			if (useConstantVelocity.Value)
			{
				targetVelocity = perDepthVelocity;
			}
			Sequence.UpdateVelocity(targetVelocity);
			ThrustBalance = 0.5f;
			if (Sequence.NonDeformedExitPRatio > 0f)
			{
				float requiredVelocity = Mathf.Lerp(MinVelocity, Mathf.Min(MaxBalancedVelocity, MaxVelocity), Sequence.NonDeformedExitPRatio);
				requiredVelocity = Mathf.Min(requiredVelocity, Sequence.Velocity);
				float x = Mathf.Lerp(initialInVelocity, requiredVelocity, 0.3f);
				requiredVelocity = x;
				float s = requiredVelocity / Sequence.Velocity * 0.25f;
				ThrustBalance = Mathf.Sqrt(s);
				Sequence.NonDeformedExitPRatio = 0f;
			}
			if (ThrustBalance > 0.49f && ThrustBalance < 0.51f)
			{
				ThrustBalance = UserThrustBalance;
			}
			else if (ThrustBalance < 0.5f)
			{
				ThrustBalance = Math.Min(ThrustBalance, UserThrustBalance);
			}
			else if (ThrustBalance > 0.5f)
			{
				ThrustBalance = Math.Max(ThrustBalance, UserThrustBalance);
			}
		}

		private void ReactInput()
		{
			if (base.Session.Player.Character.pene.isPenetrating)
			{
				if (hotkeyHandle.Up && hotkeyHandle.Duration < 2f)
				{
					if (Sequence != null)
					{
						StopSequence();
					}
					else
					{
						StartSequence();
					}
				}
			}
			else
			{
				StopSequence();
			}
		}

		public void TryStartSequence()
		{
			if (base.Session.Player.Character.pene.isPenetrating && Sequence == null)
			{
				StartSequence();
			}
		}

		private void StartSequence()
		{
			if (Sequence == null)
			{
				Sequence = new SequenceState
				{
					Velocity = MinVelocity,
					hole = base.Session.Player.Character.pene.TryGetPenetratingHole()
				};
				if (Sequence.hole != null)
				{
					Sequence.HoleEntrance = base.Session.Guest.Puppet.GetIKBoneTransform(Sequence.hole.entrada);
				}
				UpdateVelocitySettings();
				if (reduceSmoothTime.Value)
				{
					controllerSmoothTime.Value = 0.005f;
				}
				overlay.InfoMessage("Auto-thrust sequence started");
			}
		}

		private void StopSequence()
		{
			if (Sequence != null)
			{
				Sequence = null;
				controllerSmoothTime.Value = defaultControllerSmoothTime;
				overlay.InfoMessage("Auto-thrust sequence stopped");
			}
		}
	}

	public enum MotionType
	{
		NONE,
		IN,
		OUT
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<KeyboardShortcut> hotkey;

	private ConfigEntry<bool> constantVelocity;

	private ConfigEntry<bool> reduceSmoothTime;

	private ConfigEntry<bool> targetVelocityScale;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		enableFeature = config.Bind<bool>("Features", "EnableAutoThrust", true, "Enable Auto Thrust: Automatic pelvis motion");
		hotkey = config.Bind<KeyboardShortcut>("AutoThrust", "MainHotkey", new KeyboardShortcut(KeyCode.Space, Array.Empty<KeyCode>()), "Auto Thrust: Start/stop hotkey");
		constantVelocity = config.Bind<bool>("AutoThrust", "ConstantVelocity", false, "Auto Thrust: Constant velocity");
		reduceSmoothTime = config.Bind<bool>("AutoThrust", "ReduceSmoothTime", true, "Auto Thrust: Speed patch");
		targetVelocityScale = config.Bind<bool>("AutoThrust", "TargetVelocityScale", false, "Auto Thrust: Scale velocity with user target");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(hotkey, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(constantVelocity, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(reduceSmoothTime, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(targetVelocityScale, base.Scope, PluginOptionsService.SettingsType.player);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new AutoThrustService(hotkey, constantVelocity, reduceSmoothTime, targetVelocityScale));
	}
}
