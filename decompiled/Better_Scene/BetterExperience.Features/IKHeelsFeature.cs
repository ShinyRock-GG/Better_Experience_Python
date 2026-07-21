using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk.CustomEffectors;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk.Interacciones;
using Assets.Base.RootMotion.BeachGirl.Runtime.FinalIk.HighHeelScripts;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

public class IKHeelsFeature : PluginFeature
{
	public class HeelSupportService : StoryService
	{
		private FemaleHighHeelSystem system;

		private IToeIKEffector eToeR;

		private IToeIKEffector eToeL;

		private PoseManager poseManager;

		private InteractionManager interactionManager;

		private Transform footL;

		private Transform footR;

		private ArmatureBone bFootL;

		private ArmatureBone bFootR;

		private Quaternion toeIdentityRotationR;

		private Quaternion toeIdentityRotationL;

		private Traverse<float> pHeelAngle;

		private Posture _currentPosture;

		private bool _isCurrentPostureStanding;

		private static BitMask<Direction> NON_DOWN_BITMASK = new BitMask<Direction>(Direction.forward, Direction.backward, Direction.up, Direction.right, Direction.left);

		private bool updateToeState;

		private Quaternion activeAngle = Quaternion.identity;

		private Vector3 activeOffset = Vector3.zero;

		private bool requireFootSupport;

		private Traverse pInteractionSystemUpdated;

		public bool IsFootSupportRequired()
		{
			Posture posture = ((interactionManager.CurrentPosture != null) ? interactionManager.CurrentPosture.Poses.Posture : null);
			if (_currentPosture != posture)
			{
				_currentPosture = posture;
				if (posture != null)
				{
					_isCurrentPostureStanding = poseManager.StandingPosture.Is(_currentPosture);
				}
				else
				{
					_isCurrentPostureStanding = false;
				}
			}
			if (_isCurrentPostureStanding)
			{
				PoseState pose = interactionManager.PoseClassifier.Encode();
				BitMask<Direction> crossSet = pose.leftFoot.hip.xyz.Intersect(pose.rightFoot.hip.xyz);
				bool nosupport = true;
				if (nosupport && HasSupport(pose.leftFoot.hip.xyz, crossSet))
				{
					nosupport = false;
				}
				if (nosupport && HasSupport(pose.rightFoot.hip.xyz, crossSet))
				{
					nosupport = false;
				}
				if (Time.frameCount % 300 == 0 || requireFootSupport == nosupport)
				{
					logger.Info($"cross set {crossSet} L {pose.leftFoot.hip.xyz} R {pose.rightFoot.hip.xyz}");
				}
				return !nosupport;
			}
			return false;
		}

		private bool HasSupport(BitMask<Direction> mainLeg, BitMask<Direction> crossSet)
		{
			if (!mainLeg.Contains(Direction.down))
			{
				return false;
			}
			return !crossSet.ContainsAny(NON_DOWN_BITMASK);
		}

		public override void OnStart()
		{
			base.OnStart();
			IKFeature.AutoIKService autoik = base.Story.SceneInterviewScope.Lookup<IKFeature.AutoIKService>();
			system = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<FemaleHighHeelSystem>();
			IToesIKEffector toesIK = Traverse.Create((object)system).Property<IToesIKEffector>("toesIKEffectors", (object[])null).Value;
			eToeR = toesIK.derecho;
			eToeL = toesIK.izquierdo;
			poseManager = Lookup<PoseManager>();
			interactionManager = Lookup<InteractionManager>();
			interactionManager.AnimationController.OnCustomPoseStateChanged.Add(delegate(PoseAnimationController.CustomPoseTracker.CustomInteractionState state)
			{
				if (state == PoseAnimationController.CustomPoseTracker.CustomInteractionState.ACTIVE)
				{
					BetterSceneHarmonyPatches.DisableHighHeelIK = true;
				}
				else
				{
					BetterSceneHarmonyPatches.DisableHighHeelIK = false;
				}
			}, base.Scope);
			Armature armature = interactionManager.AnimationController.Armature;
			footL = armature.transform.FindDeepChild("CC_Base_Foot.L");
			footR = armature.transform.FindDeepChild("CC_Base_Foot.R");
			bFootL = footL.GetComponent<ArmatureBone>();
			bFootR = footR.GetComponent<ArmatureBone>();
			toeIdentityRotationR = system.toeR.localRotation;
			toeIdentityRotationL = system.toeL.localRotation;
			system.highHeelHeightUpdated += delegate
			{
				updateToeState = true;
			};
			interactionManager.OnCurrentPostureChanged.Add(delegate
			{
				UpdateHeelsState();
			});
			pHeelAngle = Traverse.Create((object)system).Field<float>("m_minAnimationRotationAngleOffsetRightAxisHeelL");
			UpdateHeelsState();
			InteractionSystemV3 is3 = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<InteractionSystemV3>();
			is3.interacted += RewriteIK;
			pInteractionSystemUpdated = Traverse.Create((object)system).Method("InteractionSystemUpdated", new Type[1] { typeof(InteractionSystemV3) }, (object[])null);
		}

		private void UpdateHeelSystem(InteractionSystemV3 sender)
		{
			bool before = BetterSceneHarmonyPatches.DisableHighHeelIK;
			try
			{
				BetterSceneHarmonyPatches.DisableHighHeelIK = false;
				system.OnUpdateEvent1();
				pInteractionSystemUpdated.GetValue(new object[1] { sender });
			}
			finally
			{
				BetterSceneHarmonyPatches.DisableHighHeelIK = before;
			}
		}

		private void RewriteIK(InteractionSystemV3 obj)
		{
			system.OnUpdateEvent2();
			if (bFootL == null || bFootR == null)
			{
				return;
			}
			if (updateToeState)
			{
				updateToeState = false;
				UpdateHeelSystem(obj);
				system.UpdateHeight();
				toeIdentityRotationR = system.toeR.localRotation;
				toeIdentityRotationL = system.toeL.localRotation;
				UpdateHeelsState();
			}
			if (system.currentHeelLocalHeight > 0f)
			{
				eToeL.rotation = bFootL.transform.rotation * toeIdentityRotationL;
				eToeR.rotation = bFootR.transform.rotation * toeIdentityRotationR;
				if (requireFootSupport != IsFootSupportRequired())
				{
					UpdateHeelsState();
				}
			}
		}

		private void UpdateHeelsState()
		{
			Vector3 bodyOffset = Vector3.zero;
			Quaternion footAngle = Quaternion.identity;
			if (system.currentHeelLocalHeight > 0f)
			{
				float angle = pHeelAngle.Value;
				footAngle = Quaternion.AngleAxis(0f - angle, Vector3.right);
				if (IsFootSupportRequired())
				{
					requireFootSupport = true;
					bodyOffset = Vector3.forward * system.currentHeelLocalHeight;
				}
				else
				{
					requireFootSupport = false;
					bodyOffset = Vector3.zero;
				}
				system.enabled = false;
			}
			if (!(activeAngle == footAngle) || !(activeOffset == bodyOffset))
			{
				activeAngle = footAngle;
				activeOffset = bodyOffset;
				if (footAngle == Quaternion.identity)
				{
					interactionManager.AnimationController.SetHeelsState(null);
					return;
				}
				PoseAnimationClip pac = new PoseAnimationClip(poseManager.StandingPosture.Poses.PostureClip);
				pac.Name = "___Heels";
				pac.Variant = "gen";
				pac.UniqueName = "Stand.___Heels.get";
				pac.FullName = "Stand.___Heels";
				BoneConfiguration frame2 = new BoneConfiguration(pac.Frames[0]);
				frame2.Rotations["CC_Base_Foot.L"] = frame2.Rotations["CC_Base_Foot.L"] * footAngle;
				frame2.Rotations["CC_Base_Foot.R"] = frame2.Rotations["CC_Base_Foot.R"] * footAngle;
				frame2.HipOffset += bodyOffset;
				pac.AddFrameData(1, frame2);
				pac.States.Add(new PoseAnimationFrame
				{
					FadeIn = 0f,
					Key = 0
				});
				pac.States.Add(new PoseAnimationFrame
				{
					FadeIn = 0.1f,
					Key = 1
				});
				pac.States[0].Next = new List<PoseAnimationFrame>();
				pac.States[0].Next.Add(pac.States[1]);
				interactionManager.AnimationController.SetHeelsState(pac);
				logger.Info("Generated heel diff");
			}
		}
	}

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new HeelSupportService());
	}
}
