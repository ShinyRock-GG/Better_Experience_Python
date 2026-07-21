using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Controllers;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using Assets._ReusableScripts.PhysicsScripts;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

internal class LipsDeformerFeature : PluginFeature
{
	private class LabiaController : SessionService
	{
		private class AdminState
		{
			public int Index { get; set; }

			public Action PatchCallback { get; set; }

			public Vector3 PosTargetInitial { get; set; }

			public Traverse<Vector3> PosTargetAccessor { get; set; }
		}

		private VagController controller;

		private float[] targetPointsL;

		private float[] targetPointsR;

		private float[] realTargetsL;

		private float[] realTargetsR;

		private float _initialWidth;

		private Transform labiaRootTransform;

		private IDictionary<JointDistancesAdmin, AdminState> overriders = new Dictionary<JointDistancesAdmin, AdminState>();

		public float[] DefaultTargetsL => realTargetsL;

		public float[] DefaultTargetsR => realTargetsR;

		public float[] TargetsL => targetPointsL;

		public float[] TargetsR => targetPointsR;

		public float[] YTargets { get; set; }

		internal float Width => controller.vagHoleController.hole.maxAnchuraVirtualLocal;

		public float InitialWidth
		{
			get
			{
				if (_initialWidth == 0f)
				{
					if (!(PenetrationDepth > 0f))
					{
						return Width;
					}
					_initialWidth = Width;
				}
				return _initialWidth;
			}
		}

		internal float PenetrationDepth
		{
			get
			{
				BoneStretchedChain hole = controller.vagHoleController.hole;
				if (hole.isPenetrated)
				{
					return 0f; // penetracionLocalActual removed in SMA 23.1
				}
				return 0f;
			}
		}

		internal bool WidthLimit
		{
			get
			{
				BoneStretchedChain hole = controller.vagHoleController.hole;
				if (hole.isPenetrated)
				{
					return hole.maximaAnchuraVirtualAlcanzada;
				}
				return false;
			}
		}

		internal float ScalerX => (labiaRootTransform.localScale.x - 0.7f) / 0.45f;

		internal float ScalerZ => (labiaRootTransform.localScale.z - 0.8752499f) / 0.19450003f;

		public override void OnStart()
		{
			base.OnStart();
			controller = base.Session.Guest.Impl.GetComponentInChildren<VagController>();
			SMAGlobalPatches.BeforeUpdateJointDistances.Add(OnOverrideJointDistance, base.Scope);
			targetPointsL = new float[controller.labiaController.l.chain.puntosBase.Count];
			targetPointsR = new float[targetPointsL.Length];
			realTargetsL = new float[targetPointsL.Length];
			realTargetsR = new float[targetPointsL.Length];
			YTargets = new float[targetPointsL.Length];
			ConfigureOverriders(controller.labiaController.l.chain, targetPointsL, realTargetsL);
			ConfigureOverriders(controller.labiaController.r.chain, targetPointsR, realTargetsR);
			labiaRootTransform = base.Session.Guest.Puppet.GetBoneTransform(Singleton<MapasDeHuesos>.instance.mapas.vagLabiaBonesMap.vagLabiaRoot);
		}

		private void ConfigureOverriders(Linear7BoneChainBase chain, float[] targetPoints, float[] realPoints)
		{
			for (int i = 0; i < chain.puntosBase.Count; i++)
			{
				JointDistancesAdmin admin = chain.puntosBase[i].jointDistancesAdmin;
				int idx = i;
				AdminState adminState = (overriders[admin] = new AdminState());
				AdminState state = adminState;
				state.PatchCallback = delegate
				{
					realPoints[idx] = admin.configuracion.finalTagetPoistionMod;
					if (YTargets[idx] == 0f)
					{
						admin.configuracion.finalTagetPoistionMod = Mathf.Min(admin.configuracion.finalTagetPoistionMod, targetPoints[idx]);
					}
					UpdateGrip(state);
				};
				targetPoints[i] = (realPoints[i] = admin.configuracion.finalTagetPoistionMod);
				state.PosTargetAccessor = Traverse.Create((object)admin).Field<Vector3>("targetPos");
				state.PosTargetInitial = state.PosTargetAccessor.Value;
				state.Index = i;
			}
		}

		private void UpdateGrip(AdminState state)
		{
			Vector3 target = state.PosTargetInitial;
			target.y -= YTargets[state.Index];
			state.PosTargetAccessor.Value = target;
		}

		private void OnOverrideJointDistance(JointDistancesAdmin obj)
		{
			if (overriders.TryGetValue(obj, out var patcher))
			{
				patcher.PatchCallback();
			}
		}

		internal void Invalidate()
		{
			controller.labiaController.UpdateDesgaste(force: true);
		}
	}

	private class LipsDeformerService : SessionService
	{
		private class State
		{
			public float WidthFactor { get; set; }

			public float YDeformationLowerBound { get; set; }

			public float YDeformationUpperBound { get; set; }
		}

		private LipsDeformerFeature feature;

		private static float[] maxTargetPoints = new float[7] { 1.25f, 0.75f, 0.5f, 0f, -1f, -1.5f, -2f };

		private float leftWeight;

		private float rightWeight;

		private LabiaController backend;

		private Transform lowerLegL;

		private Transform lowerLegR;

		private Transform hips;

		private float lastPenetration;

		private bool hasResistance;

		private bool anyResistance;

		private State state = new State();

		public LipsDeformerService(LipsDeformerFeature feature)
		{
			this.feature = feature;
		}

		public override void OnStart()
		{
			base.OnStart();
			backend = Lookup<LabiaController>();
			lowerLegL = base.Session.Guest.Puppet.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
			lowerLegR = base.Session.Guest.Puppet.GetBoneTransform(HumanBodyBones.RightLowerLeg);
			hips = base.Session.Guest.Puppet.GetBoneTransform(HumanBodyBones.Hips);
			IIKUpdater ik = base.Session.Guest.Impl.GetComponentInChildren<IIKUpdater>();
			base.Scope.EventHandler(delegate(Action<IIKUpdater> handler)
			{
				ik.onPhysicsIKUpdated += handler;
			}, delegate(Action<IIKUpdater> handler)
			{
				ik.onPhysicsIKUpdated -= handler;
			}, delegate
			{
				OnAfterPhysicsUpdate();
			});
		}

		private void OnAfterPhysicsUpdate()
		{
			bool invalidate = false;
			float widthFactor = Mathf.Clamp((backend.Width - backend.InitialWidth) / backend.InitialWidth, 0f, 2f);
			if (!ExtendedMonoBehaviour.AlmostEqual(state.WidthFactor, widthFactor))
			{
				state.WidthFactor = widthFactor;
				float shapeScale = 1f + 0.5f * backend.ScalerX;
				float shapeScaleZ = backend.ScalerZ;
				float factor = Mathf.Clamp01(widthFactor);
				if (!feature.enableShapeAwareGrip.Value)
				{
					shapeScale = 1.5f;
					shapeScaleZ = 1f;
				}
				if (!feature.enableResistanceAwareGrip.Value)
				{
					factor = 0f;
				}
				state.YDeformationLowerBound = Mathf.Lerp(-0.05f, -0.03f, factor) * shapeScale * 0.3f;
				float upperBoundMinDeformation = Mathf.Lerp(-0.001f, -0.005f, shapeScaleZ) + 0.02f;
				float upperBoundMaxDeformation = Mathf.Lerp(-0.001f, -0.01f, shapeScaleZ) + 0.02f;
				state.YDeformationUpperBound = Mathf.Lerp(upperBoundMaxDeformation, upperBoundMinDeformation, factor);
			}
			if (Time.frameCount % 10 == 0)
			{
				Vector3 leglpos = hips.transform.InverseTransformPoint(lowerLegL.position);
				Vector3 legrpos = hips.transform.InverseTransformPoint(lowerLegR.position);
				float left = leglpos.x;
				float right = legrpos.x;
				left = Mathf.InverseLerp(0.1f, 0.4f, left) * widthFactor;
				right = Mathf.InverseLerp(0.1f, 0.4f, 0f - right) * widthFactor;
				if (!feature.EnableLipGapDeformer.Value)
				{
					left = 0f;
					right = 0f;
				}
				else if (feature.EnablePermanentLipGap.Value)
				{
					left = 1f;
					right = 1f;
				}
				if (leftWeight != left || rightWeight != right)
				{
					leftWeight = left;
					rightWeight = right;
					float[] targetPointsL = backend.TargetsL;
					for (int i = 0; i < targetPointsL.Length; i++)
					{
						backend.TargetsL[i] = Mathf.Lerp(backend.DefaultTargetsL[i], maxTargetPoints[i], left);
						backend.TargetsR[i] = Mathf.Lerp(backend.DefaultTargetsL[i], maxTargetPoints[i], right);
					}
					invalidate = true;
				}
			}
			float delta = backend.PenetrationDepth - lastPenetration;
			if (delta > 0f)
			{
				hasResistance = backend.WidthLimit;
			}
			delta *= 2f;
			anyResistance |= hasResistance;
			float ytarget = backend.YTargets[6];
			if (!feature.enableGripSimulation.Value || backend.PenetrationDepth == 0f || (!hasResistance && !anyResistance && feature.enableResistanceAwareGrip.Value))
			{
				lastPenetration = 0f;
				if (ytarget != 0f)
				{
					invalidate = true;
					SetYTargets(0f);
				}
			}
			else
			{
				delta = Mathf.MoveTowards(0f, delta, 0.3f * Time.deltaTime);
				float newy = Mathf.Clamp(ytarget + delta, state.YDeformationLowerBound, state.YDeformationUpperBound);
				if (newy != ytarget)
				{
					SetYTargets(newy);
					invalidate = true;
				}
			}
			lastPenetration = backend.PenetrationDepth;
			if (invalidate)
			{
				backend.Invalidate();
			}
		}

		private void SetYTargets(float y)
		{
			for (int i = 4; i < 7; i++)
			{
				backend.YTargets[i] = y;
			}
		}
	}

	private ConfigEntry<bool> enableFeature;

	public ConfigEntry<bool> EnableLipGapDeformer { get; private set; }

	public ConfigEntry<bool> EnablePermanentLipGap { get; private set; }

	public ConfigEntry<bool> enableGripSimulation { get; private set; }

	public ConfigEntry<bool> enableShapeAwareGrip { get; private set; }

	public ConfigEntry<bool> enableResistanceAwareGrip { get; private set; }

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableLipsDeformer", true, "Enable lips deformer: various labia deformations");
		EnableLipGapDeformer = config.Bind<bool>("LipsDefomer", "EnableLipGapDeformation", true, "Lips Deformer: Enable lip gap deformation: larger hole -> larger potential gap");
		EnablePermanentLipGap = config.Bind<bool>("LipsDefomer", "EnablePermanentGapDeformation", false, "Lips Deformer: Enable permanent max gap deformation");
		enableGripSimulation = config.Bind<bool>("LipsDefomer", "EnableGripSimulation", true, "Lips Deformer: Enable grip simulation");
		enableShapeAwareGrip = config.Bind<bool>("LipsDefomer", "ShapeAwareGripSimulation", true, "Lips Deformer: Shape Aware Grip: Link grip shape to lips shape");
		enableResistanceAwareGrip = config.Bind<bool>("LipsDefomer", "ResistanceAwareGripSimulation", true, "Lips Deformer: Resistance Aware Grip: Link grip shape to actual hole resistance");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(EnableLipGapDeformer, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(EnablePermanentLipGap, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(enableGripSimulation, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(enableShapeAwareGrip, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(enableResistanceAwareGrip, base.Scope, PluginOptionsService.SettingsType.guest);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new LabiaController());
		Lookup<SessionTracker>().InterviewServices.Add(() => new LipsDeformerService(this));
	}
}
