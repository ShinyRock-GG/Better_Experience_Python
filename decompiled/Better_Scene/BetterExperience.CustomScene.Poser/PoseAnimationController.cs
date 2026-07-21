using System.Collections;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets.Base.Bones.Gizmos.BeachGirl.Runtime;
using Assets.Base.Bones.Gizmos.Runtime;
using BetterExperience.CustomScene.Characters;
using BetterExperience.Features;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PoseAnimationController : SessionService
{
	public class CustomPoseTracker
	{
		public enum CustomInteractionState
		{
			ACTIVATE,
			ACTIVATED,
			ACTIVE,
			DEACTIVATE,
			INACTIVE
		}

		private PoseAnimationController parent;

		private GameSession Session;

		private IInteraccionesDeCharacter interactions;

		private InteraccionPrimaria customPoseInteraction;

		private CustomInteractionState _state = CustomInteractionState.INACTIVE;

		public GizmosDeSkeleton Skeleton { get; }

		public CustomInteractionState State
		{
			get
			{
				return _state;
			}
			private set
			{
				if (_state != value)
				{
					_state = value;
					OnStateChanged.Invoke(_state);
				}
			}
		}

		private Logger logger => parent.logger;

		public bool Enabled { get; set; }

		public Observable<CustomInteractionState> OnStateChanged { get; } = new Observable<CustomInteractionState>();

		public bool IsCustomPoseInteraction
		{
			get
			{
				InteraccionDeCharacter interaccionDeCharacter2 = interactions.ObtenerFirstEjecutandosePrimaria();
				if (interaccionDeCharacter2 != null)
				{
					// Compare by reference to the known custom pose InteraccionPrimaria
					// (replaces __InteraccionName_Ext.GetInteractionID source-generator call removed in SMA 23.1)
					return interaccionDeCharacter2.instancia == customPoseInteraction;
				}
				return false;
			}
		}

		public bool IsActive => State == CustomInteractionState.ACTIVE;

		public bool IsChangingState
		{
			get
			{
				if ((State == CustomInteractionState.ACTIVE || State == CustomInteractionState.INACTIVE) && (State != CustomInteractionState.ACTIVE || Enabled))
				{
					if (State == CustomInteractionState.INACTIVE)
					{
						return Enabled;
					}
					return false;
				}
				return true;
			}
		}

		public CustomPoseTracker(PoseAnimationController parent)
		{
			this.parent = parent;
			Session = parent.Session;
			interactions = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<IInteraccionesDeCharacter>();
			// Get the custom pose interaction directly from PrepararCustomPoseOnEditMode
			// (replaces __InteraccionName_Ext.GetInteractionID source-generator call removed in SMA 23.1)
			PrepararCustomPoseOnEditMode prepararCustomPose = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<PrepararCustomPoseOnEditMode>();
			customPoseInteraction = prepararCustomPose.customInteraccion;
			Skeleton = ((Component)(object)customPoseInteraction).GetComponentInChildren<GizmosDeSkeleton>();
			DispatcherService disp = parent.Scope.Lookup<DispatcherService>();
			disp.DoUpdate.Add(Update, parent.Scope);
			disp.MeshGeneralUpdate1.Add(AfterAnimator, parent.Scope);
		}

		private void Update()
		{
			if (State == CustomInteractionState.ACTIVE)
			{
				if (!Enabled && SetState(CustomInteractionState.DEACTIVATE))
				{
					((Interaccion)customPoseInteraction).Detener();
				}
				if (!IsCustomPoseInteraction)
				{
					Disable("Active state but no custom primary interaction");
				}
			}
			else if (State == CustomInteractionState.ACTIVATED)
			{
				if (IsCustomPoseInteraction)
				{
					SetState(CustomInteractionState.ACTIVE);
					return;
				}
				InteraccionDeCharacter interaccionDeCharacter2 = interactions.ObtenerFirstEjecutandosePrimaria();
				if (parent.logger.EnableDebug)
				{
					parent.logger.Debug("Waiting activation {0}", (interaccionDeCharacter2 == null) ? "null" : interaccionDeCharacter2.id.ToString());
				}
			}
			else if (State == CustomInteractionState.DEACTIVATE)
			{
				if (!IsCustomPoseInteraction)
				{
					SetState(CustomInteractionState.INACTIVE);
				}
			}
			else if (State == CustomInteractionState.INACTIVE)
			{
				if (Enabled)
				{
					SetState(CustomInteractionState.ACTIVATE);
				}
				else if (IsCustomPoseInteraction)
				{
					Enable();
					SetState(CustomInteractionState.ACTIVATE);
				}
			}
		}

		public void Disable(string reason)
		{
			if (logger.EnableDebug)
			{
				parent.logger.Debug("Interrupt: {0}", reason);
			}
			Enabled = false;
		}

		private void AfterAnimator()
		{
			if (!SetState(CustomInteractionState.ACTIVATED))
			{
				return;
			}
			PrepararCustomPoseOnEditMode componentInChildren3 = ((Component)(object)Session.Guest.Impl).GetComponentInChildren<PrepararCustomPoseOnEditMode>();
			bool existingCustomPose = IsCustomPoseInteraction;
			if (!existingCustomPose)
			{
				// ActivateSkeletonEditorMode moved from PrepararCustomPoseOnEditMode to the Character class in SMA 23.1
				HarmonyLib.Traverse.Create((object)Session.Guest.Impl).Method("ActivateSkeletonEditorMode").GetValue();
				RetargetSkeleton();
				parent.animator.Armature.ReadPoseFast();
			}
			if (!componentInChildren3.customInteractionFollower.enabled)
			{
				Transform rmt = ((Character)(object)Session.Guest.Impl).animatorRootMotionTransform;
				componentInChildren3.customInteractionFollower.enabled = true;
				componentInChildren3.customInteractionFollower.transform.SetPositionAndRotation(rmt.position, rmt.rotation);
			}
			if (!existingCustomPose)
			{
				((Interaccion)customPoseInteraction).ForzarEjecucion(-1f, 1f, 1f, 1f, true, false);
				if (logger.EnableDebug)
				{
					logger.Debug("Started custom interaction");
				}
				parent.animator.Bind();
			}
		}

		public void Enable()
		{
			Enabled = true;
		}

		private bool SetState(CustomInteractionState targetState)
		{
			switch (targetState)
			{
			case CustomInteractionState.ACTIVATE:
				if (State == CustomInteractionState.INACTIVE)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.ACTIVATE;
					return true;
				}
				parent.logger.Error("Unable to activate from state {0}", State);
				break;
			case CustomInteractionState.DEACTIVATE:
				if (State == CustomInteractionState.ACTIVE)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.DEACTIVATE;
					return true;
				}
				parent.logger.Error("Unable to deactivate from state {0}", State);
				break;
			case CustomInteractionState.ACTIVE:
				if (State == CustomInteractionState.ACTIVATED)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.ACTIVE;
					return true;
				}
				if (State == CustomInteractionState.INACTIVE)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.ACTIVE;
					return true;
				}
				break;
			case CustomInteractionState.ACTIVATED:
				if (State == CustomInteractionState.ACTIVATE)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.ACTIVATED;
					return true;
				}
				break;
			case CustomInteractionState.INACTIVE:
				if (State == CustomInteractionState.DEACTIVATE)
				{
					if (logger.EnableDebug)
					{
						parent.logger.Debug("{0} {1}->{2}", Time.frameCount, State, targetState);
					}
					State = CustomInteractionState.INACTIVE;
					return true;
				}
				break;
			default:
				parent.logger.Error("Unexpected target state {0}", targetState);
				break;
			}
			return false;
		}

		public void RetargetSkeleton()
		{
			Transform rootmotion = ((Character)(object)Session.Guest.Impl).animatorRootMotionTransform;
			Transform hips = Skeleton.rootBone.GetChild(0);
			Vector3 originalpos = hips.position;
			Quaternion originalrot = hips.rotation;
			Skeleton.rootBone.position = rootmotion.TransformPoint(parent.animator.PostureOffset);
			Skeleton.rootBone.rotation = rootmotion.rotation * parent.animator.PostureRotation;
			hips.position = originalpos;
			hips.rotation = originalrot;
		}
	}

	public class InertialVectorBuffer
	{
		private Vector3[] buffer;

		private int wrIndex;

		public Vector3 Value { get; private set; }

		public InertialVectorBuffer(int capacity = 10)
		{
			buffer = new Vector3[capacity];
		}

		public void Update(Vector3 value)
		{
			buffer[wrIndex++] = value;
			if (wrIndex >= buffer.Length)
			{
				wrIndex = 0;
			}
			Vector3 sum = default(Vector3);
			Vector3[] array = buffer;
			foreach (Vector3 v in array)
			{
				sum += v;
			}
			sum /= (float)buffer.Length;
			Value = sum;
		}

		internal void Clear()
		{
			if (Value != Vector3.zero)
			{
				buffer.Fill(Vector3.zero);
				Update(Vector3.zero);
			}
		}
	}

	public static readonly List<PoseAnimationClip> EMPTY_LIST = new List<PoseAnimationClip>();

	private RelIKTargeting limbikTargeting;

	private RelIK2Feature.IKEffectorSet relIk2Effectors;

	private PoseAnimationState currentPose;

	private List<PoseAnimationClip> primaryClips;

	private List<PoseAnimationClip> idleClips;

	private Dictionary<string, PoseAnimationClip> primaryClipsById;

	private Dictionary<string, List<PoseAnimationClip>> primaryClipsByName;

	private ExtensibleAnimator animator;

	private AnimatedIK animatedIk;

	private InertialVectorBuffer rootMotionBuffer = new InertialVectorBuffer();

	private CustomPoseTracker customPose;

	private ExpandedSkeleton skeletonEx;

	private GizmoDeBoneRMInfo[] gizmoDeBones;

	private LoadAplicarEffectorConfigDeBone[] effectorDeBones;

	private LoadAplicarMuscleConfigDeBone[] muscleDeBones;

	public PoseAnimationState ActivePose => currentPose;

	public GizmosDeSkeleton Skeleton => customPose.Skeleton;

	public Observable ActiveStateChanged { get; } = new Observable();

	public Observable OnPrimaryClipCompleted { get; } = new Observable();

	public Observable<AnimatorFrameChangedEvent> OnActiveFrameChanged => animator.OnClipFrameChanged;

	public Observable OnBeforePoseWrite => animator.OnBeforePoseWrite;

	public Observable<AnimatorLayer> _OnLayerClipComplete => animator.OnLayerClipComplete;

	public Armature Armature => animator.Armature;

	public RelIKTargeting IKTargeting => limbikTargeting;

	public Dictionary<string, string> IKTargetObjects { get; } = new Dictionary<string, string>();

	public GesturesWeights Gestures { get; private set; }

	public Transform PostureOffset { get; private set; }

	public RelIK2Feature.IKEffectorSet IKEffectorSet => animatedIk.EffectorSet;

	public float TimeScale { get; set; } = 1f;

	public bool ChangingState => customPose.IsChangingState;

	public bool IsCustomPoseActive => customPose.Enabled;

	public Observable<CustomPoseTracker.CustomInteractionState> OnCustomPoseStateChanged => customPose.OnStateChanged;

	public IAnimationClipState GetPlayingClipByLayer(AnimatorLayer layer)
	{
		return animator.GetActiveLayerClip(layer);
	}

	public IAnimationClipState GetClipByLayer(AnimatorLayer layer)
	{
		return animator.GetLayerClip(layer);
	}

	public void StartCustomPose()
	{
		customPose.Enabled = true;
	}

	public void Clear()
	{
		animator.ClearAnimation();
		currentPose = null;
	}

	public override void OnStart()
	{
		base.OnStart();
		Gestures = base.Session.Guest.GesturesController.RequestWeightsAccessor(base.Scope);
		customPose = new CustomPoseTracker(this);
		PostureOffset = UnityUtils.NewTransform("BECS_PostureOffset", Skeleton.rootBone.parent, base.Scope);
		PostureOffset.localPosition = Vector3.zero;
		PostureOffset.localRotation = Quaternion.identity;
		limbikTargeting = new RelIKTargeting(Skeleton, base.Session.Guest.Puppet, base.Session.Guest.RootObject.transform.FindDeepChild("OwnToConvexPuppetColliders"), base.Scope);
		Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
		IKTargetObjects["%player%"] = UnityUtils.GetNameInHierarchy(base.Session.Player.GameObject.transform, null);
		IKTargetObjects["%guest%"] = UnityUtils.GetNameInHierarchy(((Component)(object)base.Session.Guest.Impl).gameObject.transform, null);
		Transform armatureContainer = UnityUtils.NewTransform("Armatures", Skeleton.rootBone.parent.parent, base.Scope);
		Transform armatureRoot = UnityUtils.NewTransform("PoseArmature", armatureContainer);
		Armature armature = armatureRoot.gameObject.AddComponent<Armature>();
		armature.InitializeArmatureSkeleton(Skeleton.rootBone);
		animator = new ExtensibleAnimator(armature, PostureOffset, Gestures, base.Session.Player);
		animatedIk = new AnimatedIK(Lookup<RelIK2Feature.RelIK2Service>(), base.Scope);
		animator.AddAnimationSystem(animatedIk);
		animator.OnLayerClipComplete.Add(delegate(AnimatorLayer layer)
		{
			if (layer == AnimatorLayer.Primary)
			{
				OnPrimaryClipCompleted.Invoke();
			}
		}, base.Scope);
		gizmoDeBones = Skeleton.GetComponentsInChildren<GizmoDeBoneRMInfo>();
		effectorDeBones = Skeleton.GetComponentsInChildren<LoadAplicarEffectorConfigDeBone>();
		muscleDeBones = Skeleton.GetComponentsInChildren<LoadAplicarMuscleConfigDeBone>();
		animator.OnIKTargetChange.Add(Animator_OnIkTargetsChanged, base.Scope);
		animator.OnMusclesChange.Add(Animator_OnMuscleChange, base.Scope);
	}

	public void ApplyWorkaround()
	{
		Lookup<DispatcherService>().StartCoroutine(CustomPoseWorkaround(), base.Scope);
	}

	private void ExpandSkeleton()
	{
		skeletonEx = Skeleton.gameObject.AddComponent<ExpandedSkeleton>();
		skeletonEx.Init(base.Scope);
		Transform target = Skeleton.rootBone.FindDeepChild("CC_Base_Hand.R");
		Transform handslot = skeletonEx.CreateCustomBone("CS_Slot_CC_Base_Hand.R", target).Target;
		GameObject cap = Lookup<AssetLoader>().LoadPrefab("capsule", "assets/capsule") as GameObject;
		if (cap != null)
		{
			cap.transform.parent = handslot;
			cap.transform.localRotation = Quaternion.identity;
			cap.transform.localPosition = Vector3.zero;
			logger.Error("loaded capsule");
		}
		else
		{
			logger.Error("Not loaded capsule");
		}
	}

	private IEnumerator CustomPoseWorkaround()
	{
		yield return new WaitForSeconds(0.5f);
		customPose.Enable();
		yield return null;
		while (!customPose.IsActive)
		{
			yield return null;
		}
		customPose.Disable("Initial");
		Lookup<PositionManager>().ResetFixGoto();
	}

	private void Animator_OnMuscleChange(BoneMuscleData muscles)
	{
		GizmoDeBoneRMInfo[] array = gizmoDeBones;
		foreach (GizmoDeBoneRMInfo c in array)
		{
			muscles.Apply(c);
		}
		LoadAplicarEffectorConfigDeBone[] array2 = effectorDeBones;
		foreach (LoadAplicarEffectorConfigDeBone c2 in array2)
		{
			c2.SetConfigAEffector();
		}
		LoadAplicarMuscleConfigDeBone[] array3 = muscleDeBones;
		foreach (LoadAplicarMuscleConfigDeBone c3 in array3)
		{
			c3.SetConfigAMuscle(updatePoseConfig: false);
		}
	}

	private void Animator_OnIkTargetsChanged(string bone, DynamicIKTarget ikTarget, float smooth)
	{
	}

	public bool StartAnimation(string name)
	{
		PoseAnimationClip clip = ResolveClip(name);
		if (clip != null)
		{
			return StartAnimation(clip);
		}
		return false;
	}

	public PoseAnimationClip ResolveClip(string name)
	{
		if (primaryClipsById.TryGetValue(name, out var clip))
		{
			return clip;
		}
		if (idleClips.Count > 0 && (idleClips[0].Name == name || idleClips[0].UniqueName == name || idleClips[0].FullName == name))
		{
			return idleClips[Random.Range(0, idleClips.Count)];
		}
		if (primaryClipsByName.TryGetValue(name, out var clips))
		{
			return clips[Random.Range(0, clips.Count)];
		}
		logger.Error("Animation {0} not found", name);
		return null;
	}

	public bool StartAnimation(PoseAnimationClip clip, float blendingTime = -1f, ICollection<AnimatorLayer> layer = null, AnimationCompletionMode completionMode = AnimationCompletionMode.Default, string label = null)
	{
		if (layer == null)
		{
			layer = new AnimatorLayer[1];
		}
		if (label != null && !clip.Labels.ContainsKey(label))
		{
			logger.Error("There is no fragment named {0} inside {1}", label, clip.UniqueName);
			return false;
		}
		currentPose = new PoseAnimationState(animator);
		customPose.Enable();
		animator.SetAnimation(clip, layer, blendingTime, completionMode, label);
		ActiveStateChanged.Invoke();
		return true;
	}

	internal void SetHeelsState(PoseAnimationClip heelState)
	{
		animator.SetHeelsState(heelState);
	}

	internal void SetAnimationRoot((Vector3, Quaternion) rootBone, bool teleport)
	{
		animator.PostureOffset = rootBone.Item1;
		animator.PostureRotation = rootBone.Item2;
		PostureOffset.localPosition = rootBone.Item1;
		PostureOffset.localRotation = rootBone.Item2;
		if (customPose.IsActive)
		{
			if (!teleport)
			{
				customPose.RetargetSkeleton();
			}
			else
			{
				logger.Info("Teleporting skeleton");
			}
			animator.Armature.ReadPoseFast();
			animator.Bind();
		}
	}

	public void AddClip(PoseAnimationClip clip)
	{
		if (primaryClipsById.ContainsKey(clip.UniqueName) || idleClips.Contains(clip))
		{
			return;
		}
		if (clip.IsIdle)
		{
			idleClips.Add(clip);
			return;
		}
		primaryClips.Add(clip);
		primaryClipsById.Add(clip.UniqueName, clip);
		primaryClipsByName.GetValueOrAdd(clip.Name, () => new List<PoseAnimationClip>()).Add(clip);
	}

	internal void SetPrimaryAnimationSet(List<PoseAnimationClip> poses, List<PoseAnimationClip> idlePoses)
	{
		List<PoseAnimationClip> prevPrimary = primaryClips;
		List<PoseAnimationClip> prevIdle = idleClips;
		primaryClips = poses;
		idleClips = idlePoses;
		primaryClipsById = new Dictionary<string, PoseAnimationClip>();
		primaryClipsByName = new Dictionary<string, List<PoseAnimationClip>>();
		poses.ForEach(delegate(PoseAnimationClip p)
		{
			primaryClipsById[p.UniqueName] = p;
			primaryClipsById[p.FullName] = p;
			primaryClipsByName.GetValueOrAdd(p.Name, () => new List<PoseAnimationClip>()).Add(p);
		});
		if (currentPose != null && !primaryClips.Contains(currentPose.PrimaryClip) && !idleClips.Contains(currentPose.PrimaryClip))
		{
			animator.ClearAnimation();
			ActiveStateChanged.Invoke();
		}
	}

	public void InterruptPose(string reason)
	{
		if (currentPose != null)
		{
			customPose.Disable(reason);
			currentPose = null;
			IKTargeting.BeginTransition();
			IKTargeting.EndTransition();
			animator.ClearAnimation();
			ActiveStateChanged.Invoke();
		}
		else if (customPose.Enabled)
		{
			customPose.Disable(reason);
		}
	}

	private void OnUpdate()
	{
		if (customPose.IsActive != Gestures.Enabled)
		{
			Gestures.Enabled = customPose.IsActive;
			Gestures.Dirty = true;
		}
		if (customPose.IsActive != animatedIk.Enabled)
		{
			animatedIk.Enabled = customPose.IsActive;
			if (!animatedIk.Enabled)
			{
				animatedIk.Clear();
			}
		}
		if (!customPose.IsActive)
		{
			if (animator.PrimaryClip != null && !customPose.Enabled)
			{
				if (logger.EnableDebug)
				{
					logger.Debug("Starting custom pose due to clip presence");
				}
				customPose.Enabled = true;
			}
			return;
		}
		float animationTime = Time.deltaTime;
		animator.Update(Time.deltaTime * TimeScale, animationTime * TimeScale);
		if (animator.PrimaryClip != null && animator.PrimaryClip.RootMotionType != RootMotionType.None)
		{
			Vector3 offset = animator.RootMotionOffset;
			offset *= base.Session.Guest.Impl.rootBone.transform.localScale.x;
			rootMotionBuffer.Update(offset);
			if (offset != Vector3.zero)
			{
				animator.RootMotionOffset = Vector3.zero;
			}
		}
		else
		{
			rootMotionBuffer.Clear();
		}
		if (rootMotionBuffer.Value != Vector3.zero)
		{
			Quaternion r = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform.rotation;
			((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform.position += r * rootMotionBuffer.Value;
		}
		if (animator.RootMotionRotationOffset != Quaternion.identity && TimeScale > 0f)
		{
			((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform.rotation *= animator.RootMotionRotationOffset;
			animator.RootMotionRotationOffset = Quaternion.identity;
		}
	}

	public static void ApplySkeletonConfiguration(PoseAnimationController animator, BoneConfiguration keyframe, Transform rootmotion, Vector3 rootOffset, Quaternion rootRotaion, float length)
	{
		GizmosDeSkeleton skeleton = animator.Skeleton;
		skeleton.rootBone.position = rootmotion.TransformPoint(rootOffset);
		skeleton.rootBone.localPosition += keyframe.RootOffset;
		skeleton.rootBone.rotation = rootmotion.rotation * rootRotaion * keyframe.RootRotation;
		skeleton.rootBone.FindDeepChild("CC_Base_Hip").localPosition = keyframe.HipOffset;
		animator.ApplyIK(keyframe.IKTargets, length);
		for (int i = 1; i < skeleton.mainBones.Count; i++)
		{
			Transform t = skeleton.mainBones[i];
			if (keyframe.Rotations.TryGetValue(t.name, out var localAngle))
			{
				t.localRotation = localAngle;
				continue;
			}
			Logger.Global.Error("Missing bone data for {0}", t.name);
		}
		GizmoDeBoneRMInfo[] componentsInChildren = skeleton.GetComponentsInChildren<GizmoDeBoneRMInfo>();
		foreach (GizmoDeBoneRMInfo c in componentsInChildren)
		{
			keyframe.Muscles.Apply(c);
		}
		LoadAplicarEffectorConfigDeBone[] componentsInChildren2 = skeleton.GetComponentsInChildren<LoadAplicarEffectorConfigDeBone>();
		foreach (LoadAplicarEffectorConfigDeBone c2 in componentsInChildren2)
		{
			c2.SetConfigAEffector();
		}
		LoadAplicarMuscleConfigDeBone[] componentsInChildren3 = skeleton.GetComponentsInChildren<LoadAplicarMuscleConfigDeBone>();
		foreach (LoadAplicarMuscleConfigDeBone c3 in componentsInChildren3)
		{
			c3.SetConfigAMuscle(updatePoseConfig: false);
		}
	}

	private void ApplyIK(IKTargetData iKTargets, float length)
	{
		IKTargeting.BeginTransition();
		foreach (KeyValuePair<string, DynamicIKTarget> kv in iKTargets)
		{
			Transform t = Skeleton.rootBone.FindDeepChild(kv.Key);
			if (t == null)
			{
				logger.Error("ApplyIK: Bone {0} not found", kv.Key);
				continue;
			}
			string targetName = kv.Value.Target;
			foreach (KeyValuePair<string, string> sub in IKTargetObjects)
			{
				targetName = targetName.Replace(sub.Key, sub.Value);
			}
			GameObject target = GameObject.Find(targetName);
			if (target == null)
			{
				logger.Error("ApplyIK: Target {0} not found", targetName);
				continue;
			}
			IKTargeting.SetIKTarget(t, new RelIKTargeting.IKTarget
			{
				Transform = target.transform,
				LocalOffset = kv.Value.LocalPosition,
				LocalRotation = kv.Value.LocalRotation
			}, length);
		}
		IKTargeting.EndTransition();
	}
}
