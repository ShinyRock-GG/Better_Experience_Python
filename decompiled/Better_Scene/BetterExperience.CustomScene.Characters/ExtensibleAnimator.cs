using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using UnityEngine;
using UnityEngine.Pool;

namespace BetterExperience.CustomScene.Characters;

internal class ExtensibleAnimator : IBoneAnimator
{
	public class PrivateAnimatorState
	{
		public AnimationLayerState[] layers { get; private set; }

		public AnimationLayerState additiveLayer { get; private set; }

		public AnimationLayerState heelsPostureLayer { get; private set; }

		public PrivateAnimatorState(AnimationLayerState[] layers, AnimationLayerState additiveLayer, AnimationLayerState heelsLayer)
		{
			this.layers = layers;
			this.additiveLayer = additiveLayer;
			heelsPostureLayer = heelsLayer;
		}
	}

	public class AnimationLayerState
	{
		public AnimationClipState SourceState { get; set; }

		public AnimationClipState TargetState { get; set; }

		public bool IsPlaying
		{
			get
			{
				if (TargetState == null)
				{
					return false;
				}
				return TargetState.IsPlaying;
			}
		}

		public AnimatorLayer[] AdditiveLayers { get; internal set; }
	}

	public class AnimationClipState : IAnimationClipState
	{
		internal BoneMuscleData seqMuscles;

		internal IKTargetData seqIk;

		public SeqClip seqClip { get; private set; }

		public float time { get; set; }

		public float seqTime
		{
			get
			{
				if (!PingPong)
				{
					return seqClip.length - time;
				}
				return time;
			}
		}

		public bool seqInitialized { get; set; }

		public PoseAnimationClip Clip => seqClip.source;

		public bool IsPlaying { get; internal set; }

		public int Cycles { get; set; }

		public SkeletonBoneAccessors.InMemoryBones Disposition { get; private set; }

		public AnimationCompletionMode CompletionMode { get; set; }

		public bool PingPong { get; set; } = true;

		public float Weight { get; set; } = 1f;

		public float BlendingTime { get; set; }

		public AnimationStage Stage { get; set; }

		public bool Cyclic => seqClip.cyclic;

		public float Time => time;

		public float Length => seqClip.length;

		public void FadeOut()
		{
			if (Stage == AnimationStage.FadeIn || Stage == AnimationStage.Play)
			{
				Stage = AnimationStage.FadeOut;
			}
		}

		public void Dispose()
		{
			GenericPool<AnimationClipState>.Release(this);
		}

		public static AnimationClipState Create(SeqClip clip, IReadOnlyList<Transform> bones, SkeletonBoneAccessors.InMemoryBones boneDisposition)
		{
			AnimationClipState state = GenericPool<AnimationClipState>.Get();
			state.seqMuscles = null;
			state.seqIk = null;
			state.time = 0f;
			state.CompletionMode = AnimationCompletionMode.Default;
			state.PingPong = true;
			state.Weight = 1f;
			state.BlendingTime = 0f;
			state.Stage = AnimationStage.FadeIn;
			state.seqInitialized = false;
			state.IsPlaying = true;
			state.seqClip = clip;
			state.Disposition = boneDisposition;
			return state;
		}
	}

	public class ArmatureBones : IBoneDisposition
	{
		private Transform[] bones;

		private Transform hipBone;

		private Transform rootMotionTransform;

		private Queue<SkeletonBoneAccessors.InMemoryBones> buffers = new Queue<SkeletonBoneAccessors.InMemoryBones>();

		public Armature Skeleton { get; private set; }

		public Vector3 PostureOffset { get; set; }

		public Quaternion PostureRotation { get; set; }

		public int Count => bones.Length;

		public IReadOnlyList<Transform> Bones => bones;

		public Transform RootMotionTransform => rootMotionTransform;

		public Vector3 HipOffset
		{
			get
			{
				return hipBone.localPosition;
			}
			set
			{
				hipBone.localPosition = value;
			}
		}

		public Vector3 RootOffset
		{
			get
			{
				return rootMotionTransform.InverseTransformPoint(Skeleton.RootBone.transform.position);
			}
			set
			{
				Skeleton.RootBone.transform.position = rootMotionTransform.TransformPoint(value);
			}
		}

		public Quaternion RootRotation
		{
			get
			{
				return Quaternion.Inverse(rootMotionTransform.rotation) * Skeleton.RootBone.transform.rotation;
			}
			set
			{
				Skeleton.RootBone.transform.rotation = rootMotionTransform.rotation * value;
			}
		}

		public ArmatureBones(Armature skeleton, Transform rootMotionTransform)
		{
			Skeleton = skeleton;
			bones = new Transform[skeleton.Bones.Count - 1];
			for (int i = 1; i < skeleton.Bones.Count; i++)
			{
				bones[i - 1] = skeleton.Bones[i];
			}
			hipBone = skeleton.RootBone.transform.FindDeepChild("CC_Base_Hip");
			this.rootMotionTransform = rootMotionTransform;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Quaternion GetRotation(int index)
		{
			return bones[index].localRotation;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SetRotation(int index, Quaternion value)
		{
			bones[index].localRotation = value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsHip(int index)
		{
			return bones[index] == hipBone;
		}

		public SkeletonBoneAccessors.InMemoryBones CreateBuffer()
		{
			if (buffers.Count > 0)
			{
				return buffers.Dequeue();
			}
			return new SkeletonBoneAccessors.InMemoryBones(Count);
		}

		public void ReleaseBuffer(SkeletonBoneAccessors.InMemoryBones bones)
		{
			if (bones != null && !buffers.Contains(bones))
			{
				buffers.Enqueue(bones);
			}
		}
	}

	protected static Logger logger = new Logger
	{
		Prefix = "[Armature Animator] "
	};

	private PrivateAnimatorState state;

	private AnimationLayerState[] layers;

	private AnimationLayerState additiveLayer;

	private AnimationLayerState heelsLayer;

	private AnimatorLayer[] layerParents = new AnimatorLayer[11];

	private List<AnimationClipState> activeStates = new List<AnimationClipState>();

	private AnimSystems animationSystems;

	public static Logger Logger => logger;

	public Armature Armature { get; }

	public Observable<AnimatorLayer> OnLayerClipComplete { get; } = new Observable<AnimatorLayer>
	{
		Buffered = true
	};

	public Observable<AnimatorFrameChangedEvent> OnClipFrameChanged { get; } = new Observable<AnimatorFrameChangedEvent>
	{
		Buffered = true
	};

	public Observable<string, DynamicIKTarget, float> OnIKTargetChange => animatedArmature.OnIKTargetChange;

	public Observable<BoneMuscleData> OnMusclesChange => animatedArmature.OnMusclesChange;

	public Vector3 PostureOffset
	{
		get
		{
			return animatedArmature.PostureOffset;
		}
		set
		{
			animatedArmature.PostureOffset = value;
		}
	}

	public Quaternion PostureRotation
	{
		get
		{
			return animatedArmature.PostureRotation;
		}
		set
		{
			animatedArmature.PostureRotation = value;
		}
	}

	public Vector3 RootMotionOffset
	{
		get
		{
			return animatedArmature.RootMotionOffset;
		}
		set
		{
			animatedArmature.RootMotionOffset = value;
		}
	}

	public Quaternion RootMotionRotationOffset
	{
		get
		{
			return animatedArmature.RootMotionRotationOffset;
		}
		set
		{
			animatedArmature.RootMotionRotationOffset = value;
		}
	}

	private AnimatedArmature animatedArmature => animationSystems.AnimatedArmature;

	public Observable OnBeforePoseWrite => animatedArmature.OnBeforePoseWrite;

	public bool IsPosing => layers[0].TargetState != null;

	public bool IsPlaying => layers[0].IsPlaying;

	public PoseAnimationClip PrimaryClip
	{
		get
		{
			if (!IsPosing)
			{
				return null;
			}
			return layers[0].TargetState.Clip;
		}
	}

	public ExtensibleAnimator(Armature armature, Transform rootMotionTransform, GesturesWeights gestures, PlayerCharacter playerCharacter)
	{
		layerParents[0] = AnimatorLayer.Primary;
		layerParents[1] = AnimatorLayer.Primary;
		layerParents[2] = AnimatorLayer.Body;
		layerParents[5] = AnimatorLayer.Torso;
		layerParents[6] = AnimatorLayer.Torso;
		layerParents[7] = AnimatorLayer.ArmLeft;
		layerParents[8] = AnimatorLayer.ArmRight;
		layerParents[9] = AnimatorLayer.Body;
		layerParents[10] = AnimatorLayer.Body;
		layerParents[4] = AnimatorLayer.Head;
		layerParents[3] = AnimatorLayer.Torso;
		additiveLayer = new AnimationLayerState();
		heelsLayer = new AnimationLayerState();
		layers = new AnimationLayerState[11].Fill(() => new AnimationLayerState());
		state = new PrivateAnimatorState(layers, additiveLayer, heelsLayer);
		Armature = armature;
		animationSystems = new AnimSystems(new AnimatedArmature(state, armature, rootMotionTransform), new AnimatedFace(state, gestures), new AnimatedPlayerState(state, playerCharacter));
		Bind();
	}

	public void AddAnimationSystem(AnimatedSystem system)
	{
		system.SetState(state);
		animationSystems.Add(system);
	}

	internal IAnimationClipState GetActiveLayerClip(AnimatorLayer layer)
	{
		AnimationLayerState l = ((layer == AnimatorLayer.Additive) ? additiveLayer : layers[(int)layer]);
		if (l.TargetState == null)
		{
			return null;
		}
		if (!l.TargetState.IsPlaying)
		{
			return null;
		}
		return l.TargetState;
	}

	internal IAnimationClipState GetLayerClip(AnimatorLayer layer)
	{
		AnimationLayerState l = ((layer == AnimatorLayer.Additive) ? additiveLayer : layers[(int)layer]);
		return l.TargetState;
	}

	public void Bind()
	{
		animationSystems.Bind();
	}

	public void Update(float dt, float at)
	{
		animationSystems.BeforeUpdate();
		AnimationClipState primaryState = layers[0].TargetState;
		if ((primaryState != null || activeStates.Count != 0) && UpdateStates(at))
		{
			animationSystems.Apply(primaryState, dt);
			OnMusclesChange.Flush();
			OnIKTargetChange.Flush();
			OnClipFrameChanged.Flush();
			OnLayerClipComplete.Flush();
		}
	}

	private bool UpdateAnimationStage(float dt, AnimationClipState layer)
	{
		bool anyplaying = false;
		if (layer.Stage == AnimationStage.FadeOut && layer.Weight <= 0f)
		{
			layer.Stage = AnimationStage.Stop;
		}
		if (layer.Stage == AnimationStage.Play && layer.Weight < 1f)
		{
			layer.Stage = AnimationStage.FadeIn;
		}
		if (layer.Stage == AnimationStage.FadeIn && layer.Weight >= 1f)
		{
			layer.Stage = AnimationStage.Play;
		}
		if (layer.Stage == AnimationStage.FadeIn)
		{
			anyplaying = true;
			if (layer.BlendingTime <= 0f)
			{
				layer.Weight = 1f;
			}
			else
			{
				layer.Weight = Mathf.Clamp01(layer.Weight + dt / layer.BlendingTime);
			}
		}
		else if (layer.Stage == AnimationStage.FadeOut)
		{
			anyplaying = true;
			if (layer.BlendingTime <= 0f)
			{
				layer.Weight = 0f;
			}
			else
			{
				layer.Weight = Mathf.Clamp01(layer.Weight - dt / layer.BlendingTime);
			}
		}
		else if (layer.Stage == AnimationStage.Stop)
		{
			if (layer == layers[0].TargetState)
			{
				OnLayerClipComplete.Invoke(AnimatorLayer.Primary);
			}
			else if (layer == additiveLayer.TargetState)
			{
				additiveLayer.TargetState = null;
			}
			else
			{
				AnimationLayerState primarystate = layers[0];
				for (int i = 1; i < layers.Length; i++)
				{
					if (layers[i].TargetState == layer)
					{
						int parent = (int)layerParents[i];
						layers[i].TargetState = layers[parent].TargetState;
						OnLayerClipComplete.Invoke((AnimatorLayer)i);
						logger.Info("Layer {0} completed", (AnimatorLayer)i);
					}
				}
				animationSystems.AnimatedArmature.SignalIkChange(primarystate.TargetState, 0.5f);
			}
		}
		return anyplaying;
	}

	private void UpdateState(AnimationClipState state, float dt)
	{
		if (!state.seqInitialized)
		{
			animationSystems.Initialize(state);
			state.seqInitialized = true;
		}
		SeqClip seq = state.seqClip;
		while (dt > 0f)
		{
			float maxupdate = seq.length - state.time;
			float dt2 = Mathf.Min(maxupdate, dt);
			state.IsPlaying = dt2 > 0f;
			dt -= dt2;
			state.time += dt2;
			animationSystems.Update(state, dt2);
			if (!(dt > 0f))
			{
				continue;
			}
			if (state.Cyclic)
			{
				RolloverAnimation(state);
			}
			else
			{
				state.IsPlaying = false;
				if (state.Stage == AnimationStage.Play && state.CompletionMode == AnimationCompletionMode.FadeOut)
				{
					state.FadeOut();
				}
			}
			if (dt2 <= 0f)
			{
				break;
			}
		}
	}

	private void RolloverAnimation(AnimationClipState state)
	{
		state.time = 0f;
		state.Cycles++;
		animationSystems.OnRollover(state);
		state.time = state.seqClip.loopTimeIndex;
		if (state.seqClip.pingPongCycle)
		{
			state.PingPong = !state.PingPong;
		}
	}

	[Timed]
	private bool UpdateStates(float dt)
	{
		bool anyplaying = false;
		foreach (AnimationClipState state in activeStates)
		{
			anyplaying |= UpdateAnimationStage(dt, state);
			if (state.IsPlaying)
			{
				anyplaying = true;
				UpdateState(state, dt);
			}
		}
		bool invalidateStates = false;
		AnimationLayerState[] array = layers;
		foreach (AnimationLayerState layer in array)
		{
			if (layer.SourceState != null)
			{
				if (layer.TargetState == null)
				{
					layer.SourceState = null;
					invalidateStates = true;
				}
				else if (layer.TargetState.Stage != AnimationStage.FadeIn)
				{
					layer.SourceState = null;
					invalidateStates = true;
				}
			}
		}
		if (invalidateStates)
		{
			InvalidateActiveStates();
		}
		return anyplaying;
	}

	internal void ClearAnimation()
	{
		AnimationLayerState[] array = layers;
		foreach (AnimationLayerState layer in array)
		{
			layer.TargetState = null;
			layer.SourceState = null;
		}
		additiveLayer.SourceState = null;
		additiveLayer.TargetState = null;
		activeStates.Clear();
	}

	public void SetAnimation(PoseAnimationClip clip, AnimatorLayer layer, float blendingTime = -1f, string label = null)
	{
		SetAnimation(clip, new AnimatorLayer[1] { layer }, blendingTime, AnimationCompletionMode.Default, label);
	}

	public void SetAnimation(PoseAnimationClip clip, ICollection<AnimatorLayer> layers, float blendingTime = -1f, AnimationCompletionMode completionMode = AnimationCompletionMode.Default, string label = null)
	{
		if (logger.EnableDebug)
		{
			logger.Debug("Start animation {0}", clip.UniqueName);
		}
		if (clip.RootMotionType != RootMotionType.None && !clip.Cyclic)
		{
			SceneWarnings.Instance.Report("Critical animator problem: {0} but has no loop. Clips with root-motion must be cyclic", clip.UniqueName, clip.RootMotionType);
		}
		if (layers.Contains(AnimatorLayer.Body))
		{
			if (!layers.Contains(AnimatorLayer.Torso))
			{
				layers.Add(AnimatorLayer.Torso);
			}
			if (!layers.Contains(AnimatorLayer.LegLeft))
			{
				layers.Add(AnimatorLayer.LegLeft);
			}
			if (!layers.Contains(AnimatorLayer.LegRight))
			{
				layers.Add(AnimatorLayer.LegRight);
			}
		}
		if (layers.Contains(AnimatorLayer.Torso))
		{
			if (!layers.Contains(AnimatorLayer.HandLeft))
			{
				layers.Add(AnimatorLayer.HandLeft);
			}
			if (!layers.Contains(AnimatorLayer.HandRight))
			{
				layers.Add(AnimatorLayer.HandRight);
			}
			if (!layers.Contains(AnimatorLayer.ArmLeft))
			{
				layers.Add(AnimatorLayer.ArmLeft);
			}
			if (!layers.Contains(AnimatorLayer.ArmRight))
			{
				layers.Add(AnimatorLayer.ArmRight);
			}
		}
		bool primarySwitch = layers.Contains(AnimatorLayer.Primary);
		if (activeStates.Count == 0 || primarySwitch)
		{
			Bind();
		}
		if (label != null && completionMode == AnimationCompletionMode.Default)
		{
			completionMode = AnimationCompletionMode.Stop;
		}
		if (label == null && completionMode == AnimationCompletionMode.Default)
		{
			completionMode = ((!primarySwitch) ? AnimationCompletionMode.FadeOut : (clip.Cyclic ? AnimationCompletionMode.Loop : AnimationCompletionMode.Stop));
		}
		AnimationClipState state = AnimationClipState.Create(GetSeqClip(clip, layers.Contains(AnimatorLayer.Additive), label, completionMode), animationSystems.AnimatedArmature.Bones, animationSystems.AnimatedArmature.CreateBuffer());
		state.CompletionMode = completionMode;
		blendingTime = Mathf.Max(blendingTime, ComputeFadein(state.Clip));
		if (primarySwitch)
		{
			for (int i = 0; i < this.layers.Length; i++)
			{
				SetLayerState(i, state, blendingTime);
			}
			SetAdditiveLayerState(null, blendingTime, null);
		}
		else if (layers.Contains(AnimatorLayer.Additive))
		{
			SetAdditiveLayerState(state, blendingTime, layers);
		}
		else
		{
			foreach (AnimatorLayer layer in layers)
			{
				SetLayerState((int)layer, state, blendingTime);
			}
		}
		InvalidateActiveStates();
	}

	private SeqClip GetSeqClip(PoseAnimationClip pac, bool additive, string label, AnimationCompletionMode completionMode)
	{
		(string, bool, string, AnimationCompletionMode) cacheKey = (pac.UniqueName, additive, label, completionMode);
		if (SeqClipConverter.CLIP_CACHE.TryGetValue(cacheKey, out var cached))
		{
			return cached;
		}
		bool breakOnLabel = label != null;
		SeqClip clip = SeqClipConverter.Create(pac, animationSystems.AnimatedArmature.Bones, additive, label, breakOnLabel);
		if (label != null)
		{
			clip.cyclic = completionMode == AnimationCompletionMode.Loop || completionMode == AnimationCompletionMode.PingPong;
		}
		else if (completionMode == AnimationCompletionMode.Default)
		{
			clip.cyclic = pac.Cyclic;
		}
		else
		{
			clip.cyclic = completionMode == AnimationCompletionMode.Loop || completionMode == AnimationCompletionMode.PingPong;
		}
		clip.pingPongCycle = completionMode == AnimationCompletionMode.PingPong;
		SeqClipConverter.CLIP_CACHE[cacheKey] = clip;
		return clip;
	}

	private float ComputeFadein(PoseAnimationClip clip)
	{
		if (clip.Frames.Count < 1)
		{
			return 0f;
		}
		float maxAngle = 0f;
		BoneConfiguration disposition = clip.Frames[0];
		foreach (Transform t in animatedArmature.Bones)
		{
			if (disposition.Rotations.TryGetValue(t.name, out var quat))
			{
				float angle = Quaternion.Angle(t.localRotation, quat);
				maxAngle = Mathf.Max(maxAngle, Mathf.Abs(angle));
			}
		}
		float aFadein = maxAngle / 90f;
		logger.Debug("blending MaxAngle {0} = {1}", maxAngle, aFadein);
		float distance = 0f;
		if (PrimaryClip != null)
		{
			BoneConfiguration now = PrimaryClip.Frames[0];
			distance = Vector3.Distance(now.RootOffset, disposition.RootOffset) + Vector3.Distance(now.HipOffset, disposition.HipOffset);
		}
		else
		{
			distance = disposition.RootOffset.magnitude + disposition.HipOffset.magnitude;
		}
		float dFadein = distance / 1f;
		logger.Debug("blending Maxdistance {0} = {1}", distance, dFadein);
		return Mathf.Max(aFadein, dFadein);
	}

	private void SetLayerState(int layer, AnimationClipState state, float blendingTime)
	{
		AnimationClipState current = layers[layer].TargetState;
		int parentLayer = (int)layerParents[layer];
		if (current != null && layer > 0 && layers[parentLayer].TargetState != state)
		{
			layers[layer].SourceState = current;
			logger.Info("{0} clip displaced {1} on layer {2}", state.Clip.UniqueName, current.Clip.UniqueName, (AnimatorLayer)layer);
		}
		else
		{
			layers[layer].SourceState = null;
		}
		layers[layer].TargetState = state;
		layers[layer].TargetState.Weight = 0f;
		layers[layer].TargetState.BlendingTime = blendingTime;
		layers[layer].TargetState.Stage = AnimationStage.FadeIn;
	}

	private void SetAdditiveLayerState(AnimationClipState state, float blendingTime, ICollection<AnimatorLayer> layers)
	{
		if (state == null || additiveLayer.TargetState == null || additiveLayer.TargetState.Clip != state.Clip)
		{
			additiveLayer.TargetState = state;
			if (state != null)
			{
				logger.Debug("Set additive animation clip {0}", state.Clip.UniqueName);
				additiveLayer.TargetState.Weight = 0f;
				additiveLayer.TargetState.BlendingTime = blendingTime;
				additiveLayer.TargetState.Stage = AnimationStage.FadeIn;
				additiveLayer.AdditiveLayers = layers.ToArray();
			}
			else
			{
				logger.Debug("Clear additive animation clip");
			}
		}
	}

	private void SetHeelLayerState(AnimationClipState state, float blendingTime, ICollection<AnimatorLayer> layers)
	{
		if (state == null || heelsLayer.TargetState == null || heelsLayer.TargetState.Clip != state.Clip)
		{
			heelsLayer.TargetState = state;
			if (state != null)
			{
				logger.Debug("Set additive animation clip {0}", state.Clip.UniqueName);
				heelsLayer.TargetState.Weight = 0f;
				heelsLayer.TargetState.BlendingTime = blendingTime;
				heelsLayer.TargetState.Stage = AnimationStage.FadeIn;
				heelsLayer.AdditiveLayers = layers.ToArray();
			}
			else
			{
				logger.Debug("Clear additive animation clip");
			}
		}
	}

	private void InvalidateActiveStates()
	{
		List<AnimationClipState> deadLists;
		using (CollectionPool<List<AnimationClipState>, AnimationClipState>.Get(out deadLists))
		{
			deadLists.AddRange(activeStates);
			activeStates.Clear();
			AnimationLayerState[] array = layers;
			foreach (AnimationLayerState s in array)
			{
				if (s.TargetState != null && !activeStates.Contains(s.TargetState))
				{
					activeStates.Add(s.TargetState);
				}
				if (s.SourceState != null && !activeStates.Contains(s.SourceState))
				{
					activeStates.Add(s.SourceState);
				}
			}
			if (additiveLayer.TargetState != null && !activeStates.Contains(additiveLayer.TargetState))
			{
				activeStates.Add(additiveLayer.TargetState);
			}
			if (heelsLayer.TargetState != null && !activeStates.Contains(heelsLayer.TargetState))
			{
				activeStates.Add(heelsLayer.TargetState);
			}
			deadLists.RemoveAll((AnimationClipState x) => activeStates.Contains(x));
			foreach (AnimationClipState d in deadLists)
			{
				animatedArmature.ReleaseBuffer(d.Disposition);
				d.Dispose();
			}
		}
	}

	public void SetHeelsState(PoseAnimationClip clip)
	{
		if (clip != null)
		{
			AnimationClipState state = AnimationClipState.Create(GetSeqClip(clip, additive: true, null, AnimationCompletionMode.Stop), animationSystems.AnimatedArmature.Bones, animationSystems.AnimatedArmature.CreateBuffer());
			SetHeelLayerState(state, 1f, new AnimatorLayer[1] { AnimatorLayer.Body });
		}
		else
		{
			SetHeelLayerState(null, 0f, null);
		}
		InvalidateActiveStates();
	}
}
