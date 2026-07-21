using System;
using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.Bones.V2;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones.UI;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.GoTo.UI;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.Interacciones.UI;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets._ReusableScripts.UI.Interacciones.Donas;
using Assets.Base.Bones.Gizmos.BeachGirl.Runtime;
using Assets.Base.Bones.Gizmos.Runtime;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Patches;
using BetterExperience.CustomScene.Poser;
using BetterExperience.CustomScene.Poser.Formats;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.CustomScene;

public class InteractionManager : SessionService
{
	public class InteractionQueryContext
	{
		public string activePoseName { get; internal set; }

		public PoseAnimationClip activeClip { get; internal set; }

		public POIPosture CurrentPosture { get; internal set; }

		public CurrentPlace CurrentPlace { get; internal set; }

		public bool IgnoreOrientation { get; set; }

		public PoseOrientation FinalOrientation { get; set; } = PoseOrientation.UNIVERSAL;

		public bool NoValidation { get; set; }

		public bool Teleport { get; set; }
	}

	private class InteractionContextImpl : InteractionContext
	{
		private Logger logger = Logger.Create<InteractionContextImpl>();

		private Queue<Interaction> interactionQueue = new Queue<Interaction>();

		private IEnumerator<BasicOperation> sequence;

		public Interaction ActiveInteraction
		{
			get
			{
				if (interactionQueue.Count <= 0)
				{
					return null;
				}
				return interactionQueue.Peek();
			}
		}

		public int QueueLength => interactionQueue.Count;

		public BasicOperation CurrentOperation
		{
			get
			{
				if (ActiveInteraction == null)
				{
					return null;
				}
				if (sequence == null)
				{
					return null;
				}
				return sequence.Current;
			}
		}

		public InteractionContextImpl(InteractionManager manager)
		{
			base.InteractionManager = manager;
			base.AnimationController = manager.AnimationController;
			base.AnimationController.OnPrimaryClipCompleted.Add(AnimationController_OnPrimaryClipCompleted, base.InteractionManager.Scope);
		}

		private void AnimationController_OnPrimaryClipCompleted()
		{
			if (ActiveInteraction != null && sequence != null)
			{
				if (sequence.Current.IsComplete(this))
				{
					ExecuteNext();
				}
				else if (base.InteractionManager.logger.EnableDebug)
				{
					base.InteractionManager.logger.Debug("Primaty clip is not complete yet");
				}
			}
		}

		public void Submit(Interaction interaction)
		{
			if (base.InteractionManager.logger.EnableDebug)
			{
				logger.Debug("Submit interaction {0}", interaction.DisplayName);
			}
			interactionQueue.Enqueue(interaction);
			if (interactionQueue.Count == 1 && base.InteractionManager.CanExecuteNow())
			{
				ExecuteNext();
			}
		}

		private void ExecuteNext()
		{
			if (base.InteractionManager.logger.EnableDebug)
			{
				base.InteractionManager.logger.Debug("Next sequence state");
			}
			if (sequence == null)
			{
				sequence = ActiveInteraction.CreateSequence();
			}
			while (sequence.MoveNext())
			{
				sequence.Current.Run(this);
				if (base.InteractionManager.logger.EnableDebug)
				{
					base.InteractionManager.logger.Debug("{0}: Executed {1}", Time.time, sequence.Current.ToString());
				}
				if (!sequence.Current.IsComplete(this) || base.InteractionManager.AnimationController.ChangingState)
				{
					if (base.InteractionManager.logger.EnableDebug)
					{
						base.InteractionManager.logger.Debug("{0}: Starting await loop", Time.time);
					}
					return;
				}
			}
			if (base.InteractionManager.logger.EnableDebug)
			{
				base.InteractionManager.logger.Debug("Sequence complete");
			}
			interactionQueue.Dequeue();
			sequence = null;
			if (ActiveInteraction != null && base.InteractionManager.CanExecuteNow())
			{
				ExecuteNext();
			}
		}

		internal void Update(float deltaTime)
		{
			if (ActiveInteraction == null)
			{
				return;
			}
			if (sequence != null)
			{
				sequence.Current.Update(this, deltaTime);
				if (sequence.Current.IsComplete(this))
				{
					if (logger.EnableDebug)
					{
						logger.Debug("{0}: {1} completed", Time.time, sequence.Current.ToString());
					}
					ExecuteNext();
				}
			}
			else
			{
				ExecuteNext();
			}
		}

		internal void Interrupt()
		{
			interactionQueue.Clear();
			sequence = null;
			base.InteractionManager.AnimationController.Clear();
		}
	}

	private POIManager poiManager;

	private PositionManager positionManager;

	private PoseManager poseManager;

	private PoseAnimationController animator;

	private POIPostureCollection currentPostureCollection;

	private InteractionContextImpl interactionContext;

	public Logger _logger => logger;

	public PoseAnimationState ActivePose => animator.ActivePose;

	public PoseAnimationController AnimationController => animator;

	public bool IsEditorActive
	{
		get
		{
			if (Singleton<SkeletonEditorMode>.instance.activado)
			{
				return Singleton<SkeletonEditorMode>.instance.skeletonActivos.Count > 0;
			}
			return false;
		}
	}

	public POIPosture CurrentPosture { get; private set; }

	public Observable OnCurrentPostureChanged { get; } = new Observable();

	public Observable OnCurrentInteractionChanged => animator.ActiveStateChanged;

	public List<InteractionPreprocessor> Preprocessors { get; } = new List<InteractionPreprocessor>();

	public bool HasActiveInteraction => interactionContext.ActiveInteraction != null;

	public CurrentPlace CurrentPlace => positionManager.CurrentPlace;

	public PoseClassifier PoseClassifier { get; private set; }

	public int QueueLength => interactionContext.QueueLength;

	public BasicOperation CurrentOperation => interactionContext.CurrentOperation;

	public override void OnStart()
	{
		base.OnStart();
		poiManager = Lookup<POIManager>();
		poseManager = Lookup<PoseManager>();
		positionManager = Lookup<PositionManager>();
		Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
		positionManager.PlaceChanged.Add(OnPlaceChanged, base.Scope);
		positionManager.OnAnimatorTeleported.Add(OnAfterTeleport, base.Scope);
		animator = base.Scope.AddService(new PoseAnimationController());
		animator.ActiveStateChanged.Add(OnAnimatorStateChanged, base.Scope);
		BetterSceneHarmonyPatches.OnAfterGoToListPopulated.Add(OnGotoListLoaded, base.Scope);
		if (!UnityUtils.DisplaceComponent(((Component)(object)base.Session.Guest.Impl).transform, delegate(GreyOutOnInteraccionPlaying prev, GoToBlocker blocker)
		{
			blocker.InteractionManager = this;
		}))
		{
			logger.Error("Unable to inject GoToBlocker");
		}
		if (!UnityUtils.AddNeighbourComponent<OpcionesDeTHSDonaDeGoToDisponiblesQueIniciaDialogo, PointOfInterestLoader>(((Component)(object)base.Session.Guest.Impl).transform, delegate(PointOfInterestLoader loader)
		{
			loader.InitComponent(base.Scope);
		}))
		{
			logger.Error("Unable to inject PointOfInterestLoader");
		}
		if (!UnityUtils.AddNeighbourComponent<OpcionesDeTHSDonaDeInteraccionesDisponiblesQueIniciaDialogo, ContextualActivitiesLoader>(((Component)(object)base.Session.Guest.Impl).transform, delegate(ContextualActivitiesLoader loader)
		{
			loader.InteractionManager = this;
			loader.POIManager = poiManager;
			loader.PoseManager = poseManager;
			loader.Init();
		}))
		{
			logger.Error("Unable to inject pose loader");
		}
		UnityUtils.AddNeighbourComponent<GenericCheckerIsGrayOut, BusyBlocker>(((Component)(object)base.Session.Guest.Impl).transform, delegate(BusyBlocker loader)
		{
			loader.InitComponent(base.Scope);
		});
		interactionContext = new InteractionContextImpl(this);
		poseManager.OnPosesChanged.Add(AfterPosturePosesChanged, base.Scope);
		PoseClassifier = new PoseClassifier(animator.Armature, ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform);
	}

	public void GoTo(PointOfInterest poi, PoseOrientation orientation = PoseOrientation.UNIVERSAL)
	{
		positionManager.GoTo(poi, orientation);
	}

	private void AfterPosturePosesChanged(Posture obj)
	{
		if (CurrentPosture != null && CurrentPosture.Is(obj))
		{
			animator.SetPrimaryAnimationSet(obj.Poses.InteractivePoses, obj.Poses.IdlePoses);
		}
	}

	private void OnAfterTeleport()
	{
		if (!animator.Enabled)
		{
			return;
		}
		GizmosDeSkeleton Skeleton = animator.Skeleton;
		Transform rootmotion = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;
		Transform preTeleport = Skeleton.rootBone.parent;
		foreach (HitSkin skin in base.Session.Guest.Puppet.Skins)
		{
			Vector3 dp = rootmotion.InverseTransformPoint(skin.transform.position);
			skin.transform.position = preTeleport.TransformPoint(dp);
		}
		Transform hips = Skeleton.rootBone;
		Vector3 originalpos = hips.position;
		Quaternion originalrot = hips.rotation;
		Transform follower = Skeleton.rootBone.parent;
		follower.position = rootmotion.position;
		follower.rotation = rootmotion.rotation;
		hips.position = originalpos;
		hips.rotation = originalrot;
		PuppetMaster puppetMaster = base.Session.Guest.Puppet.PuppetMaster;
		List<(Muscle, TransformDisposition)> muscles = new List<(Muscle, TransformDisposition)>();
		Muscle[] muscles2 = puppetMaster.muscles;
		foreach (Muscle muscle in muscles2)
		{
			muscles.Add((muscle, new TransformDisposition(muscle.transform)));
		}
		puppetMaster.transform.SetPositionAndRotation(rootmotion.position, rootmotion.rotation);
		foreach (var m in muscles)
		{
			TransformDisposition item = m.Item2;
			item.Apply(m.Item1.transform);
		}
	}

	private void OnUpdate()
	{
		bool autoDetectPoi = true;
		if (CanExecuteNow())
		{
			bool hasInteraction = interactionContext.ActiveInteraction != null;
			interactionContext.Update(Time.deltaTime);
			autoDetectPoi = hasInteraction;
			if (!animator.IsCustomPoseActive && !hasInteraction && poseManager.StandingPosture.Is(CurrentPosture) && poseManager.AnimatedIdles)
			{
				List<PoseAnimationClip> idles = poseManager.StandingPosture.Poses.IdlePoses;
				Interaction i = CreatePlayClipInteraction(CreateQueryContext(), idles[0].Name, idles);
				StartInteraction(i);
			}
		}
		positionManager.AutoPoiBlocked = autoDetectPoi;
	}

	private void PlayerArmatureTest()
	{
		RootBone animatorRoot = base.Session.Player.Character.GetComponentInChildren<RootBone>();
		Transform armatureRoot = UnityUtils.NewTransform("Armature", base.Session.Player.GameObject.transform);
		Armature armature = armatureRoot.gameObject.AddComponent<Armature>();
		armature.InitializeArmatureSkeleton(animatorRoot.transform);
		BoneConfiguration biningpose = armature.Snapshot();
		SimpleVAMExtractor extractor = new SimpleVAMExtractor(".\\Better_Exchange\\vam.json");
		PoseAnimationClipData pbc = extractor.Data;
		SimpleVamImporter2 importer = new SimpleVamImporter2(armature);
		importer.BindingPose = biningpose;
		importer.ReadFrom(pbc.ToClip("Stand.Test.test", poseManager));
	}

	private void OnAnimatorStateChanged()
	{
		if (animator.ActivePose == null)
		{
			SetCurrentPosture(poseManager.StandingPostureAt(positionManager.CurrentPlace.POI), teleport: false);
		}
	}

	public bool StartAnimation(string poseName)
	{
		if (logger.EnableDebug)
		{
			logger.Debug("Start pose requested: {0}", poseName);
		}
		return animator.StartAnimation(poseName);
	}

	public bool StartInteraction(Interaction set)
	{
		logger.Debug("Starting interaction:\n{0}", set);
		foreach (InteractionPreprocessor preprocessor in Preprocessors)
		{
			preprocessor.Process(set);
		}
		logger.Debug("Postprocessed interaction:\n{0}", set);
		interactionContext.Submit(set);
		return true;
	}

	public void InterruptInteraction()
	{
		if (interactionContext.ActiveInteraction != null)
		{
			interactionContext.Interrupt();
		}
	}

	internal bool SetPosture(POIPosture obj)
	{
		if (currentPostureCollection != null)
		{
			if (currentPostureCollection.Contains(obj))
			{
				SetCurrentPosture(obj, teleport: false);
				return true;
			}
			logger.Error("failed to set posture to {0} at {1}", obj.Id, positionManager.CurrentPlace.POI);
		}
		return false;
	}

	private void SetCurrentPosture(POIPosture obj, bool teleport)
	{
		if (CurrentPosture == obj)
		{
			return;
		}
		CurrentPosture = obj;
		if (logger.EnableDebug)
		{
			logger.Debug("Changing active posture to {0} of {1}", obj.Id, obj.Poses.Posture.Id);
		}
		if (obj != null)
		{
			PoseOrientation targetOrientation = obj.Orientation;
			if (obj.Descriptor != null && obj.Descriptor.Orientation != PoseOrientation.UNIVERSAL)
			{
				targetOrientation = obj.Descriptor.Orientation;
			}
			if (logger.EnableDebug)
			{
				logger.Debug("Current orientation {0} requested {1}", positionManager.CurrentPlace.Orientation, targetOrientation);
			}
			positionManager.GoTo(positionManager.CurrentPlace.POI, targetOrientation);
			animator.SetAnimationRoot((obj.Configuration.RootOffset, obj.Configuration.RootRotation), teleport);
			animator.SetPrimaryAnimationSet(obj.Poses.InteractivePoses, obj.Poses.IdlePoses);
		}
		else
		{
			List<PoseAnimationClip> empty = new List<PoseAnimationClip>();
			animator.SetPrimaryAnimationSet(empty, empty);
		}
		OnCurrentPostureChanged.Invoke();
	}

	private void OnGotoListLoaded(HashSetList<string> obj)
	{
		if (IsIdlePose())
		{
			obj.Remove(ActivePose.PrimaryClip.Posture.Id);
		}
	}

	private void OnPlaceChanged(CurrentPlace place)
	{
		if (place == null)
		{
			logger.Warn("Current place changed to null");
			return;
		}
		bool teleport = !animator.Enabled;
		if (poseManager.POIPostures.TryGetValue(place.POI.Id, out var poses))
		{
			currentPostureCollection = poses;
			if (!currentPostureCollection.Contains(CurrentPosture) && CurrentPosture != null)
			{
				SetCurrentPosture(poseManager.StandingPostureAt(positionManager.CurrentPlace.POI), teleport);
			}
			else if (CurrentPosture == null)
			{
				SetCurrentPosture(poseManager.StandingPostureAt(positionManager.CurrentPlace.POI), teleport);
			}
		}
		else
		{
			SetCurrentPosture(poseManager.StandingPostureAt(positionManager.CurrentPlace.POI), teleport);
		}
	}

	public void StartPose(PoseAnimationClip pose)
	{
		animator.StartAnimation(pose);
	}

	internal bool IsIdlePose()
	{
		PoseAnimationState currentPose = ActivePose;
		if (currentPose != null && currentPose.PrimaryClip != null)
		{
			return currentPose.PrimaryClip.IsIdle;
		}
		return false;
	}

	public BoneConfiguration TakeSnapshot(Transform relativeTo)
	{
		BoneConfiguration result = new BoneConfiguration();
		GizmosDeSkeleton skeleton = AnimationController.Skeleton;
		result.RootOffset = relativeTo.InverseTransformPoint(skeleton.rootBone.position);
		result.RootRotation = Quaternion.Inverse(relativeTo.rotation) * skeleton.rootBone.rotation;
		result.HipOffset = skeleton.rootBone.Find("CC_Base_Hip").localPosition;
		GizmoDeBoneRMInfo[] gizmodebones = skeleton.GetComponentsInChildren<GizmoDeBoneRMInfo>();
		GizmoDeBoneRMInfo[] array = gizmodebones;
		foreach (GizmoDeBoneRMInfo x in array)
		{
			result.Muscles.Save(x);
		}
		foreach (Transform t in skeleton.mainBones)
		{
			if (!(t == skeleton.rootBone))
			{
				result.Rotations[t.name] = t.localRotation;
			}
		}
		foreach (KeyValuePair<Transform, RelIKTargeting.IKTarget> t2 in AnimationController.IKTargeting.EnumerateEffectorTargets())
		{
			Transform transform = t2.Key;
			RelIKTargeting.IKTarget target = t2.Value;
			result.IKTargets.Add(transform.name, new DynamicIKTarget(target, base.Session));
		}
		GesturesWeights gestures = AnimationController.Gestures;
		if (gestures.Enabled)
		{
			result.Gestures = new GesturesData(gestures);
		}
		return result;
	}

	public void MovePosture(POIPosture pp)
	{
		Transform root = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;
		BoneConfiguration snapshot = TakeSnapshot(root);
		pp.Configuration = snapshot;
		poseManager.UpdatePosture(pp);
	}

	public void CreatePostureNow(string name)
	{
		Transform root = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;
		BoneConfiguration snapshot = TakeSnapshot(root);
		Posture p = new Posture();
		p.Name = name;
		p.Orientation = positionManager.CurrentPlace.Orientation;
		p.Configuration = snapshot;
		poseManager.CreatePosture(p);
	}

	public void CreatePOIPostureNow(PointOfInterest poi, Posture posture, string name)
	{
		Transform root = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;
		BoneConfiguration snapshot = TakeSnapshot(root);
		Posture poiPosture = new Posture();
		poiPosture.Name = name;
		poiPosture.Orientation = positionManager.CurrentPlace.Orientation;
		poiPosture.Id = posture.Id + "." + poi.Id + "." + name;
		poiPosture.Configuration = snapshot;
		poseManager.CreatePoiPosture(poi, posture, poiPosture);
	}

	public IEnumerable<Interaction> EnumerateTransitions(bool clipChange = true, bool postureChange = true)
	{
		InteractionQueryContext queryContext = CreateQueryContext();
		if (clipChange && queryContext.activeClip != null)
		{
			Dictionary<string, List<PoseAnimationClip>> clips = new Dictionary<string, List<PoseAnimationClip>>();
			foreach (PoseAnimationClip t in CurrentPosture.Poses.EnumerateTransitions(queryContext.activeClip))
			{
				string key = (t.IsIdle ? "Idle" : t.Name);
				clips.GetValueOrAdd(key, () => new List<PoseAnimationClip>()).Add(t);
			}
			foreach (KeyValuePair<string, List<PoseAnimationClip>> kv in clips)
			{
				yield return CreatePlayClipInteraction(queryContext, kv.Key, kv.Value);
			}
		}
		if (queryContext.CurrentPosture != null && logger.EnableDebug)
		{
			logger.Debug("Current posture {0} rname {1}", CurrentPosture.Name, (CurrentPosture.Descriptor != null) ? CurrentPosture.Descriptor.CancelDisplayName : "?");
		}
		CurrentPlace place = queryContext.CurrentPlace;
		if (place == null)
		{
			logger.Error("No current place");
		}
		else
		{
			if (!postureChange || !poseManager.POIPostures.TryGetValue(place.POI.Id, out var postures))
			{
				yield break;
			}
			foreach (POIPosture value in postures.ExactPostures.Values)
			{
				Interaction interaction = CreatePostureChangeInteraction(queryContext, value);
				if (interaction != null)
				{
					yield return interaction;
				}
			}
		}
	}

	public IEnumerable<Interaction> EnumerateGotos()
	{
		CurrentPlace place = positionManager.CurrentPlace;
		if (place == null)
		{
			logger.Error("Current place is unresolved");
			yield break;
		}
		foreach (PointOfInterest point in poiManager.Points)
		{
			if (point.Desc != null && point.Desc.IsWaypoint)
			{
				continue;
			}
			if (point == place.POI)
			{
				if (ActivePose == null)
				{
					Interaction i = new Interaction();
					i.DisplayName = "Turn Around";
					i.SourcePosture = CurrentPosture;
					i.TargetPosture = CurrentPosture;
					i.Enqueue(new GotoOp(point, (place.Orientation == PoseOrientation.FRONT) ? PoseOrientation.BACK : PoseOrientation.FRONT));
					yield return i;
				}
			}
			else if (point.ParentPoiId == null || point.ParentPoiId == place.POI.Id || point.ParentPoiId == place.POI.ParentPoiId || point.Id == place.POI.ParentPoiId)
			{
				Interaction i2 = new Interaction();
				i2.DisplayName = point.Name;
				if (point.Id == place.POI.ParentPoiId && point.Desc.LocalDisplayName != null)
				{
					i2.DisplayName = point.Desc.LocalDisplayName;
				}
				i2.SourcePosture = CurrentPosture;
				i2.TargetPosture = poseManager.StandingPostureAt(point);
				i2.Enqueue(new GotoOp(point, place.Orientation));
				yield return i2;
			}
		}
	}

	public InteractionQueryContext CreateQueryContext(bool ignoreOrientation = false, PoseOrientation finalOrientation = PoseOrientation.UNIVERSAL)
	{
		InteractionQueryContext queryContext = new InteractionQueryContext();
		queryContext.CurrentPosture = CurrentPosture;
		queryContext.CurrentPlace = positionManager.CurrentPlace;
		if (CurrentPosture != null && (ActivePose == null || ActivePose.PrimaryClip != null))
		{
			if (ActivePose != null)
			{
				queryContext.activePoseName = ActivePose.PrimaryClip.Name;
				queryContext.activeClip = ActivePose.PrimaryClip;
			}
			else
			{
				queryContext.activeClip = new PoseAnimationClip(CurrentPosture, "Idle", "default");
				queryContext.activeClip.IsIdle = true;
				queryContext.activeClip.IsGenerated = true;
			}
			if (queryContext.activePoseName == "Binding")
			{
				queryContext.activePoseName = "Idle";
			}
		}
		queryContext.IgnoreOrientation = ignoreOrientation;
		queryContext.FinalOrientation = PoseOrientation.UNIVERSAL;
		return queryContext;
	}

	public Interaction CreatePlayClipInteraction(InteractionQueryContext qCtx, string key, List<PoseAnimationClip> value, float blendingTime = -1f, List<AnimatorLayer> layers = null, AnimationCompletionMode completionMode = AnimationCompletionMode.Default, string label = null)
	{
		Interaction interaction = new Interaction();
		interaction.SourcePosture = qCtx.CurrentPosture;
		interaction.TargetPosture = qCtx.CurrentPosture;
		if (layers == null)
		{
			layers = new List<AnimatorLayer>();
			layers.Add(AnimatorLayer.Primary);
		}
		if (layers.Contains(AnimatorLayer.Primary))
		{
			if (CurrentPosture.Poses.TransitionClips.TryGetValue((qCtx.activePoseName, key), out var transitions))
			{
				interaction.Enqueue(new AnimateOp(transitions, blendingTime, layers));
				interaction.DisplayName = ResolveDescriptorValue(transitions, (InteractionDescriptor d) => d.DisplayName);
			}
			else if (logger.EnableDebug)
			{
				logger.Debug("Transitions: requested {0} available {1}", qCtx.activePoseName + "->" + key, string.Join(",", CurrentPosture.Poses.TransitionClips.Keys.Select(((string, string) x) => x.Item1 + "->" + x.Item2).ToArray()));
			}
		}
		if (interaction.DisplayName == null)
		{
			if (key == "Idle")
			{
				if (ActivePose != null && ActivePose.PrimaryClip != null)
				{
					List<PoseAnimationClip> tmp = new List<PoseAnimationClip>();
					tmp.Add(ActivePose.PrimaryClip);
					interaction.DisplayName = ResolveDescriptorValue(tmp, (InteractionDescriptor d) => d.CancelDisplayName);
				}
				if (interaction.DisplayName == null)
				{
					interaction.DisplayName = "Relax";
				}
			}
			else
			{
				interaction.DisplayName = ResolveDescriptorValue(value, (InteractionDescriptor d) => d.DisplayName);
				if (interaction.DisplayName == null)
				{
					interaction.DisplayName = key;
				}
			}
		}
		if (key == "Idle" && CurrentPosture.Is(poseManager.StandingPosture) && value.Count == 1 && value[0].IsGenerated)
		{
			interaction.Enqueue(new LambdaOp(delegate(InteractionContext lctx)
			{
				lctx.AnimationController.InterruptPose("NativeStandIdle");
			}));
		}
		else
		{
			interaction.Enqueue(new AnimateOp(value, blendingTime, layers, completionMode)
			{
				Label = label
			});
		}
		return interaction;
	}

	public Interaction CreatePostureChangeInteraction(InteractionQueryContext queryContext, POIPosture value)
	{
		CurrentPlace place = queryContext.CurrentPlace;
		if (place == null)
		{
			return null;
		}
		if (CurrentPosture == value)
		{
			return null;
		}
		if (!queryContext.NoValidation && queryContext.activeClip != null && !queryContext.activeClip.IsIdle && !value.Is(poseManager.StandingPosture))
		{
			logger.Info("Posture change disabled because active pose is not idle");
			return null;
		}
		POIPosture currentPosture = queryContext.CurrentPosture;
		if (currentPosture != null && currentPosture.Descriptor != null && currentPosture.Descriptor.ParentPosture != null)
		{
			POIPosture pp = poseManager.FindPOIPostureById(currentPosture.Descriptor.ParentPosture);
			if (!value.Is(pp))
			{
				return null;
			}
		}
		PoseAnimationClip activeClip = queryContext.activeClip;
		if (value.Descriptor != null)
		{
			if (value.Descriptor.ParentPosture != null)
			{
				if (!currentPosture.Is(poseManager.FindPOIPostureById(value.Descriptor.ParentPosture)))
				{
					return null;
				}
				if (activeClip != null && !activeClip.IsIdle)
				{
					return null;
				}
			}
			if (!queryContext.IgnoreOrientation && value.Descriptor.Orientation != PoseOrientation.UNIVERSAL && place.Orientation != value.Descriptor.Orientation)
			{
				return null;
			}
		}
		PosturePoseCollection poses = value.Poses;
		Interaction interaction = new Interaction();
		interaction.SourcePosture = currentPosture;
		interaction.TargetPosture = value;
		if (value.Descriptor != null)
		{
			interaction.DisplayName = value.Descriptor.DisplayName;
		}
		if (currentPosture != null && value.Poses.Posture == poseManager.StandingPosture && CurrentPosture.Descriptor != null)
		{
			interaction.DisplayName = currentPosture.Descriptor.CancelDisplayName;
		}
		if (interaction.DisplayName == null)
		{
			interaction.DisplayName = value.Name;
		}
		if (interaction.SourcePosture.PoiId != interaction.TargetPosture.PoiId)
		{
			if (queryContext.Teleport)
			{
				interaction.Enqueue(new TeleportOp(poiManager.FindPOI(interaction.TargetPosture.PoiId), queryContext.FinalOrientation));
			}
			else
			{
				interaction.Enqueue(new GotoOp(poiManager.FindPOI(interaction.TargetPosture.PoiId), queryContext.FinalOrientation));
			}
		}
		if (interaction.SourcePosture != interaction.TargetPosture)
		{
			interaction.Enqueue(new SetPostureOp(interaction.TargetPosture));
		}
		if (poses.IdlePoses.Count != 0)
		{
			interaction.Enqueue(new AnimateOp(poses.IdlePoses));
		}
		else if (poses.Posture != poseManager.StandingPosture)
		{
			interaction.Enqueue(new AnimateOp(poses.PostureClip));
		}
		else
		{
			if (currentPosture.Poses.Posture != poseManager.StandingPosture)
			{
				interaction.Enqueue(new AnimateOp(poses.PostureClip));
			}
			interaction.Enqueue(new AnimateOp(poses.IdlePoses));
		}
		return interaction;
	}

	private T ResolveDescriptorValue<T>(List<PoseAnimationClip> value, Func<InteractionDescriptor, T> extractor) where T : class
	{
		if (CurrentPosture == null)
		{
			return null;
		}
		foreach (PoseAnimationClip clip in value)
		{
			if (CurrentPosture.Poses.ClipDescriptors.TryGetValue(clip, out var desc))
			{
				T result = extractor(desc);
				if (result != null)
				{
					return result;
				}
			}
		}
		return null;
	}

	private bool CanExecuteNow()
	{
		PuppetMaster pm = base.Session.Guest.Puppet.PuppetMaster;
		if (!animator.ChangingState)
		{
			return pm.mode != PuppetMaster.Mode.Disabled;
		}
		return false;
	}
}
