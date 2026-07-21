using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

internal class AnimatedArmature : AnimatedSystem
{
	private class RootMotionHelper
	{
		private Logger logger = Logger.Create<RootMotionHelper>();

		public Vector3 RootMotionOffset { get; set; }

		public Quaternion RootMotionRotationOffset { get; set; }

		public Vector3 LastRootMotionOffset { get; set; }

		public Quaternion LastRootMotionRotation { get; set; }
	}

	private Logger logger = Logger.Create<AnimatedArmature>();

	private ExtensibleAnimator.ArmatureBones armaturePose;

	private SkeletonBoneAccessors.InMemoryBones runtimePose;

	private SkeletonBoneAccessors.InMemoryBones initialPose;

	private BitMask<AnimatorLayer>[] layerMapping;

	private BitMask<AnimatorLayer> ikLayers = BitMask<AnimatorLayer>.Of();

	private RootMotionHelper rootMotionHelper = new RootMotionHelper();

	public Armature Armature { get; }

	public Vector3 PostureOffset
	{
		get
		{
			return armaturePose.PostureOffset;
		}
		set
		{
			armaturePose.PostureOffset = value;
		}
	}

	public Quaternion PostureRotation
	{
		get
		{
			return armaturePose.PostureRotation;
		}
		set
		{
			armaturePose.PostureRotation = value;
		}
	}

	public Vector3 RootMotionOffset
	{
		get
		{
			return rootMotionHelper.RootMotionOffset;
		}
		set
		{
			rootMotionHelper.RootMotionOffset = value;
		}
	}

	public Quaternion RootMotionRotationOffset
	{
		get
		{
			return rootMotionHelper.RootMotionRotationOffset;
		}
		set
		{
			rootMotionHelper.RootMotionRotationOffset = value;
		}
	}

	public IReadOnlyList<Transform> Bones => armaturePose.Bones;

	public Observable<string, DynamicIKTarget, float> OnIKTargetChange { get; } = new Observable<string, DynamicIKTarget, float>
	{
		Buffered = true
	};

	public Observable<BoneMuscleData> OnMusclesChange { get; } = new Observable<BoneMuscleData>
	{
		Buffered = true
	};

	public Observable OnBeforePoseWrite { get; } = new Observable();

	public Observable OnAfterPoseWrite { get; } = new Observable();

	public AnimatedArmature(ExtensibleAnimator.PrivateAnimatorState state, Armature armature, Transform rootMotionTransform)
	{
		SetState(state);
		armaturePose = new ExtensibleAnimator.ArmatureBones(armature, rootMotionTransform);
		runtimePose = armaturePose.CreateBuffer();
		initialPose = armaturePose.CreateBuffer();
		Armature = armature;
		layerMapping = new BitMask<AnimatorLayer>[armaturePose.Count].Fill(() => BitMask<AnimatorLayer>.Of(default(AnimatorLayer)));
		InitLayer(AnimatorLayer.Body, "CC_Base_Hip");
		InitLayer(AnimatorLayer.Torso, "CC_Base_Spine02");
		InitLayer(AnimatorLayer.ArmLeft, "CC_Base_Upperarm.L");
		InitLayer(AnimatorLayer.ArmRight, "CC_Base_Upperarm.R");
		InitLayer(AnimatorLayer.HandLeft, "CC_Base_Hand.L");
		InitLayer(AnimatorLayer.HandRight, "CC_Base_Hand.R");
		InitLayer(AnimatorLayer.LegLeft, "CC_Base_Thigh.L");
		InitLayer(AnimatorLayer.LegRight, "CC_Base_Thigh.R");
		InitLayer(AnimatorLayer.Head, "CC_Base_Head");
	}

	private void InitLayer(AnimatorLayer targetLayer, string rootBone)
	{
		Transform bone = Armature.RootBone.transform.FindDeepChild(rootBone);
		if (bone == null)
		{
			logger.Error("Unable to resolve root layer bone {0} for {1}", rootBone, targetLayer);
			return;
		}
		List<Transform> layerTransforms = new List<Transform>();
		bone.ExecDeepChild(layerTransforms.Add);
		for (int i = 0; i < armaturePose.Count; i++)
		{
			if (layerTransforms.Contains(armaturePose.Bones[i]))
			{
				layerMapping[i] = layerMapping[i].Add(targetLayer);
			}
		}
	}

	public override void BeforeUpdate()
	{
		Armature.transform.position = armaturePose.RootMotionTransform.position;
		Armature.transform.rotation = armaturePose.RootMotionTransform.rotation;
		Armature.ReadPoseFast();
	}

	[Timed]
	public override void Apply(ExtensibleAnimator.AnimationClipState state, float dt)
	{
		ComputeRootBone();
		for (int i = 0; i < armaturePose.Count; i++)
		{
			BitMask<AnimatorLayer> layer = layerMapping[i];
			ComputeRotations(layer, i);
			if (armaturePose.IsHip(i))
			{
				ComputeTranslationLayer(layer);
			}
			if (base.additiveLayer.TargetState != null)
			{
				Quaternion cr = runtimePose.GetRotation(i);
				Quaternion delta = base.additiveLayer.TargetState.Disposition.GetRotation(i);
				Quaternion ar = cr * delta;
				Quaternion tr = Quaternion.Lerp(cr, ar, base.additiveLayer.TargetState.Weight);
				runtimePose.SetRotation(i, tr);
			}
		}
		SkeletonBoneAccessors.CopyTo(runtimePose, armaturePose);
		OnBeforePoseWrite.Invoke();
		Armature.MapToRecursive(dt);
		OnAfterPoseWrite.Invoke();
	}

	private void ComputeRootBone()
	{
		ExtensibleAnimator.AnimationLayerState currentState = base.layers[0];
		if (currentState != null && currentState.TargetState != null)
		{
			float weight = currentState.TargetState.Weight;
			Quaternion rootTargetRotation = currentState.TargetState.Disposition.RootRotation;
			Vector3 rootTargetPosition = currentState.TargetState.Disposition.RootOffset;
			if (weight < 1f)
			{
				rootTargetPosition = Vector3.Lerp(initialPose.RootOffset, rootTargetPosition, weight);
				rootTargetRotation = Quaternion.Slerp(initialPose.RootRotation, rootTargetRotation, weight);
			}
			runtimePose.RootOffset = rootTargetPosition;
			runtimePose.RootRotation = rootTargetRotation;
		}
	}

	[Timed]
	private void ComputeRotationLayer(AnimatorLayer layer, int bone)
	{
		ExtensibleAnimator.AnimationLayerState currentState = base.layers[(int)layer];
		if (currentState == null || currentState.TargetState == null)
		{
			return;
		}
		float weight = currentState.TargetState.Weight;
		Quaternion targetRotation = currentState.TargetState.Disposition.GetRotation(bone);
		if (base.heelsPostureLayer.TargetState != null)
		{
			Quaternion cr = targetRotation;
			Quaternion delta = base.heelsPostureLayer.TargetState.Disposition.GetRotation(bone);
			Quaternion ar = cr * delta;
			targetRotation = Quaternion.Lerp(cr, ar, base.heelsPostureLayer.TargetState.Weight);
		}
		if (weight < 1f)
		{
			if (layer == AnimatorLayer.Primary)
			{
				targetRotation = Quaternion.Lerp(initialPose.GetRotation(bone), targetRotation, weight);
			}
			else
			{
				Quaternion parentRotation = runtimePose.GetRotation(bone);
				if (currentState.SourceState != null)
				{
					parentRotation = Quaternion.Lerp(parentRotation, currentState.SourceState.Disposition.GetRotation(bone), currentState.SourceState.Weight);
				}
				targetRotation = Quaternion.Lerp(parentRotation, targetRotation, weight);
			}
		}
		if (ikLayers.Contains(layer))
		{
			targetRotation = Quaternion.Lerp(targetRotation, armaturePose.GetRotation(bone), 0.99f);
		}
		runtimePose.SetRotation(bone, targetRotation);
	}

	[Timed]
	private void ComputeTranslationLayer(AnimatorLayer layer)
	{
		ExtensibleAnimator.AnimationLayerState currentState = base.layers[(int)layer];
		if (currentState != null && currentState.TargetState != null)
		{
			float weight = currentState.TargetState.Weight;
			Vector3 hipTargetPosition = currentState.TargetState.Disposition.HipOffset;
			if (base.heelsPostureLayer.TargetState != null)
			{
				Vector3 delta = base.heelsPostureLayer.TargetState.Disposition.HipOffset;
				Vector3 ar = hipTargetPosition + delta;
				hipTargetPosition = Vector3.Lerp(hipTargetPosition, ar, base.heelsPostureLayer.TargetState.Weight);
			}
			if (weight < 1f)
			{
				hipTargetPosition = ((layer != AnimatorLayer.Primary) ? Vector3.Lerp(runtimePose.HipOffset, hipTargetPosition, weight) : Vector3.Lerp(initialPose.HipOffset, hipTargetPosition, weight));
			}
			runtimePose.HipOffset = hipTargetPosition;
		}
	}

	[Timed]
	private void ComputeRotations(BitMask<AnimatorLayer> layerMask, int bone)
	{
		for (int i = 0; i < 11; i++)
		{
			if (layerMask.Contains((AnimatorLayer)i))
			{
				ComputeRotationLayer((AnimatorLayer)i, bone);
			}
		}
	}

	private void ComputeTranslationLayer(BitMask<AnimatorLayer> layerMask)
	{
		for (int i = 0; i < 11; i++)
		{
			if (layerMask.Contains((AnimatorLayer)i))
			{
				ComputeTranslationLayer((AnimatorLayer)i);
			}
		}
	}

	public override void Update(ExtensibleAnimator.AnimationClipState state, float dt)
	{
		SeqClip seq = state.seqClip;
		(float t0, float t1, AnimationKeyFrame c0, AnimationKeyFrame c1) tuple = seq.FindConfiguration(state.seqTime);
		float t0 = tuple.t0;
		float t1 = tuple.t1;
		AnimationKeyFrame c0 = tuple.c0;
		AnimationKeyFrame c1 = tuple.c1;
		float a = Mathf.InverseLerp(t0, t1, state.seqTime);
		BoneMuscleData activeMuscles = c0.Muscles;
		IKTargetData activeIk = c1.IKTargets;
		float ikSmooth = t1 - t0;
		for (int i = 0; i < seq.Indices.Length; i++)
		{
			Quaternion q0 = c0.Rotations[i];
			Quaternion q1 = c1.Rotations[i];
			Quaternion q2 = Quaternion.Lerp(q0, q1, a);
			state.Disposition.SetRotation(seq.Indices[i], q2);
		}
		state.Disposition.HipOffset = Vector3.Lerp(c0.HipOffset, c1.HipOffset, a);
		state.Disposition.RootOffset = Vector3.Lerp(c0.RootOffset, c1.RootOffset, a);
		state.Disposition.RootRotation = Quaternion.Lerp(c0.RootRotation, c1.RootRotation, a);
		if (state.Clip.RootMotionType == RootMotionType.HipForward)
		{
			Vector3 offset = Vector3.Lerp(c0.RootMotionOffset, c1.RootMotionOffset, a);
			Vector3 delta = rootMotionHelper.LastRootMotionOffset - offset;
			rootMotionHelper.RootMotionOffset = delta;
			rootMotionHelper.LastRootMotionOffset = offset;
		}
		else if (state.Clip.RootMotionType == RootMotionType.HipSpin)
		{
			Quaternion offset2 = Quaternion.Lerp(c0.RootMotionRotation, c1.RootMotionRotation, a);
			Quaternion delta2 = Quaternion.Inverse(rootMotionHelper.LastRootMotionRotation) * offset2;
			rootMotionHelper.RootMotionRotationOffset = delta2;
			rootMotionHelper.LastRootMotionRotation = offset2;
		}
		if (activeMuscles != null && activeMuscles != state.seqMuscles)
		{
			if (base.layers[0].TargetState == state)
			{
				OnMusclesChange.Invoke(activeMuscles);
			}
			state.seqMuscles = activeMuscles;
		}
		if (activeIk != null && activeIk != state.seqIk)
		{
			state.seqIk = activeIk;
			SignalIkChange(state, 1f + ikSmooth);
		}
	}

	internal void Bind()
	{
		SkeletonBoneAccessors.CopyTo(armaturePose, runtimePose);
		SkeletonBoneAccessors.CopyTo(armaturePose, initialPose);
	}

	private bool HasAnimationData(ExtensibleAnimator.AnimationClipState state)
	{
		if (state != null)
		{
			return state.Clip.Frames.Count > 0;
		}
		return false;
	}

	internal void SignalIkChange(ExtensibleAnimator.AnimationClipState state, float fadein)
	{
		if (CanSignalIkTargetChange(AnimatorLayer.HandLeft, state, "CC_Base_Hand.L", out var dt))
		{
			OnIKTargetChange.Invoke("CC_Base_Hand.L", dt, fadein);
		}
		if (CanSignalIkTargetChange(AnimatorLayer.HandRight, state, "CC_Base_Hand.R", out dt))
		{
			OnIKTargetChange.Invoke("CC_Base_Hand.R", dt, fadein);
		}
		if (CanSignalIkTargetChange(AnimatorLayer.LegLeft, state, "CC_Base_Foot.L", out dt))
		{
			OnIKTargetChange.Invoke("CC_Base_Foot.L", dt, fadein);
		}
		if (CanSignalIkTargetChange(AnimatorLayer.LegRight, state, "CC_Base_Foot.R", out dt))
		{
			OnIKTargetChange.Invoke("CC_Base_Foot.R", dt, fadein);
		}
	}

	private bool CanSignalIkTargetChange(AnimatorLayer layerType, ExtensibleAnimator.AnimationClipState state, string v, out DynamicIKTarget dt)
	{
		ExtensibleAnimator.AnimationLayerState layer = base.layers[(int)layerType];
		ExtensibleAnimator.AnimationClipState ts = layer.TargetState;
		if (ts == state)
		{
			dt = null;
			if (HasAnimationData(ts) && ts.seqIk.TryGetValue(v, out dt))
			{
				ikLayers = ikLayers.Add(layerType);
			}
			else
			{
				ikLayers = ikLayers.Remove(layerType);
			}
			return true;
		}
		dt = null;
		return false;
	}

	public void OnRollover(ExtensibleAnimator.AnimationClipState state)
	{
		rootMotionHelper.LastRootMotionOffset = Vector3.zero;
		rootMotionHelper.LastRootMotionRotation = Quaternion.identity;
	}

	public override void Initialize(ExtensibleAnimator.AnimationClipState state)
	{
		SkeletonBoneAccessors.CopyTo(armaturePose, state.Disposition);
		rootMotionHelper.LastRootMotionOffset = Vector3.zero;
		rootMotionHelper.LastRootMotionRotation = Quaternion.identity;
	}

	public SkeletonBoneAccessors.InMemoryBones CreateBuffer()
	{
		return armaturePose.CreateBuffer();
	}

	public void ReleaseBuffer(IBoneDisposition boneDisposition)
	{
		armaturePose.ReleaseBuffer(boneDisposition as SkeletonBoneAccessors.InMemoryBones);
	}
}
