using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace BetterExperience.Features;

public class AnimateGotoFeature : PluginFeature
{
	public class GoToAnimatorService : StoryService, InteractionPreprocessor
	{
		public class Settings
		{
			public string WalkClip { get; set; }

			public string TurnLeftClip { get; set; }

			public string TurnRightClip { get; set; }

			public float TimeScale { get; set; } = 1f;

			public bool IsValid()
			{
				if (WalkClip != null && TurnLeftClip != null)
				{
					return TurnRightClip != null;
				}
				return false;
			}
		}

		private InteractionManager interactionManager;

		private PoseManager poseManager;

		private POIManager poiManager;

		public Settings settings { get; private set; }

		public override void OnStart()
		{
			base.OnStart();
			settings = base.Story.VFS.Persisted(() => new Settings(), "\\animate_goto.json");
			interactionManager = Lookup<InteractionManager>();
			poseManager = Lookup<PoseManager>();
			poiManager = Lookup<POIManager>();
			bool enabled = settings.IsValid();
			if (logger.EnableDebug)
			{
				logger.Debug("AnimateGoto {0} {1} {2} {3}", enabled, settings.WalkClip, settings.TurnLeftClip, settings.TurnRightClip);
			}
			enabled &= poseManager.StandingPosture.Poses.FindClips(settings.WalkClip).Count > 0;
			enabled &= poseManager.StandingPosture.Poses.FindClips(settings.TurnRightClip).Count > 0;
			if (enabled & (poseManager.StandingPosture.Poses.FindClips(settings.TurnLeftClip).Count > 0))
			{
				interactionManager.Preprocessors.Add(this);
			}
			else
			{
				interactionManager.AnimationController.ApplyWorkaround();
			}
		}

		public void Process(Interaction interaction)
		{
			for (int i = 0; i < interaction.Sequence.Count; i++)
			{
				BasicOperation op = interaction.Sequence[i];
				if (op is GotoOp gotoOp && op.Preprocessors.Add(this))
				{
					i = ProcessGoto(interaction, gotoOp, i);
				}
			}
		}

		private int ProcessGoto(Interaction interaction, GotoOp gotoOp, int index)
		{
			POIPosture standingPosture = poseManager.StandingPostureAt(interactionManager.CurrentPlace.POI);
			if (!interactionManager.CurrentPosture.Is(standingPosture))
			{
				index = ChangePosture(interaction, standingPosture, index);
			}
			Vector3 targetPos = gotoOp.Target.Transform.position;
			Transform rootmotion = ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;
			Quaternion finalOrientation = gotoOp.Target.Transform.rotation;
			if (gotoOp.Orientation == PoseOrientation.BACK)
			{
				finalOrientation *= Quaternion.AngleAxis(180f, Vector3.up);
			}
			bool standingTarget = interaction.TargetPosture.Is(poseManager.StandingPosture);
			interaction.Sequence.Insert(index++, new NavigateOp(settings, rootmotion, targetPos, finalOrientation));
			if (standingTarget)
			{
				interaction.Sequence.Insert(index++, new AnimateOp(poseManager.StandingPosture.Poses.IdlePoses));
			}
			return index;
		}

		private int ChangePosture(Interaction interaction, POIPosture targetPosture, int index)
		{
			interaction.Sequence.Insert(index++, new SetPostureOp(targetPosture));
			interaction.Sequence.Insert(index++, new AnimateOp(targetPosture.Poses.PostureClip));
			return index;
		}
	}

	public class RotateTowardsOp : BasicOperation
	{
		private Transform transform;

		private Vector3 target;

		private Quaternion rotationTarget;

		private bool ignore;

		public RotateTowardsOp(Transform transform, Vector3 target)
		{
			this.transform = transform;
			this.target = target;
		}

		public override void Run(InteractionContext context)
		{
			Vector3 d = target - transform.position;
			ignore = d.magnitude < 0.1f;
			rotationTarget = Quaternion.LookRotation(d, Vector3.up);
		}

		public override bool IsComplete(InteractionContext context)
		{
			if (!ignore)
			{
				return transform.rotation == rotationTarget;
			}
			return true;
		}

		public override void Update(InteractionContext context, float dt)
		{
			if (!ignore)
			{
				transform.rotation = Quaternion.RotateTowards(transform.rotation, rotationTarget, 100f * dt);
			}
		}
	}

	public class RotateOp : BasicOperation
	{
		private Transform transform;

		private Quaternion target;

		public RotateOp(Transform transform, Quaternion target)
		{
			this.transform = transform;
			this.target = target;
		}

		public override void Run(InteractionContext context)
		{
			if (IsComplete(context))
			{
				Logger.Global.Error("Rotation complete from start");
			}
		}

		public override bool IsComplete(InteractionContext context)
		{
			return transform.rotation == target;
		}

		public override void Update(InteractionContext context, float dt)
		{
			transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 100f * dt);
			if (IsComplete(context))
			{
				Logger.Global.Error("Rotation complete after update");
			}
		}
	}

	public class TranslateOp : BasicOperation
	{
		private string clipName;

		private Transform transform;

		private Vector3 target;

		private float lastDistance;

		public TranslateOp(GoToAnimatorService.Settings settings, Transform transform, Vector3 target)
		{
			clipName = settings.WalkClip;
			this.transform = transform;
			this.target = target;
		}

		public override void Run(InteractionContext context)
		{
			if (!IsComplete(context))
			{
				context.AnimationController.StartAnimation(clipName);
				lastDistance = Vector3.Distance(target, transform.position);
			}
			else
			{
				Logger.Global.Error("Ignore translate");
			}
		}

		public override bool IsComplete(InteractionContext context)
		{
			return transform.position == target;
		}

		public override void Update(InteractionContext context, float dt)
		{
			float distance = Vector3.Distance(target, transform.position);
			if (distance > lastDistance)
			{
				transform.position = target;
			}
			lastDistance = distance;
			if (IsComplete(context))
			{
				context.AnimationController.InterruptPose("Translation complete");
			}
			else if (context.AnimationController.ActivePose == null || context.AnimationController.ActivePose.PrimaryClip.Name != clipName)
			{
				context.AnimationController.StartAnimation(clipName);
			}
		}
	}

	public class NavigateOp : BasicOperation
	{
		private enum AnimationState
		{
			None,
			Walk,
			RotateLeft,
			RotateRight
		}

		private Logger logger = Logger.Create<NavigateOp>();

		private GoToAnimatorService.Settings settings;

		private Transform transform;

		private Vector3 target;

		private Quaternion orientation;

		private AnimationState animationState;

		private NavMeshPath navPath = new NavMeshPath();

		private List<Vector3> path = new List<Vector3>();

		private bool animated = true;

		private float scaleFactor;

		private bool useNavmesh;

		private bool freshPath;

		public NavigateOp(GoToAnimatorService.Settings settings, Transform transform, Vector3 target, Quaternion orientation)
		{
			this.settings = settings;
			this.transform = transform;
			this.target = target;
			this.orientation = orientation;
		}

		public override void Run(InteractionContext context)
		{
			Vector3 start = transform.position;
			if (NavMesh.SamplePosition(transform.position, out var hit, 1f, -1))
			{
				start = hit.position;
			}
			else
			{
				logger.Error("Start is not on surface");
			}
			Vector3 end = target;
			if (NavMesh.SamplePosition(transform.position, out hit, 1f, -1))
			{
				end = target;
			}
			if (start != transform.position)
			{
				path.Add(start);
			}
			if (NavMesh.CalculatePath(start, end, -1, navPath))
			{
				NavMeshUtils.ReadPathInto(navPath, path);
				if (navPath.status == NavMeshPathStatus.PathPartial)
				{
					path.Add(end);
				}
				useNavmesh = true;
			}
			// SMA 23.1 walk-float fix: guarantee the real target is the final path point.
			// When NavMesh.CalculatePath returns PathInvalid the path otherwise held only
			// the start point, which UpdatePath immediately consumes -> empty path -> the
			// arrival-slide branch in Update() drags her the whole way with NO Walk clip
			// (the intermittent float). With the target present she routes through the
			// path-following branch, which turns to face then plays the Walk clip.
			if (path.Count == 0 || Vector3.Distance(path[path.Count - 1], target) > 0.15f)
			{
				path.Add(target);
			}
			scaleFactor = context.InteractionManager.Session.Guest.Impl.rootBone.transform.localScale.magnitude;
		}

		public override void Update(InteractionContext context, float dt)
		{
			base.Update(context, dt);
			freshPath = false;
			if (!UpdatePath())
			{
				if (animationState == AnimationState.Walk)
				{
					context.AnimationController.TimeScale = 0f;
					animationState = AnimationState.None;
				}
				if (!IsComplete(context))
				{
					transform.position = Vector3.MoveTowards(transform.position, target, 0.5f * dt);
					UpdateRotation(context, orientation, dt);
				}
				if (IsComplete(context))
				{
					context.AnimationController.TimeScale = 1f;
				}
				return;
			}
			Vector3 immediateTarget = path[0];
			Vector3 heightAlignedTarget = immediateTarget;
			heightAlignedTarget.y = transform.position.y;
			Vector3 d = heightAlignedTarget - transform.position;
			Quaternion targetRotation = ((d != Vector3.zero) ? Quaternion.LookRotation(d, Vector3.up) : transform.rotation);
			if (targetRotation == transform.rotation && animationState != AnimationState.Walk)
			{
				context.AnimationController.TimeScale = settings.TimeScale;
				animated = context.AnimationController.StartAnimation(settings.WalkClip);
				animationState = AnimationState.Walk;
			}
			if (!animated)
			{
				transform.position = Vector3.MoveTowards(transform.position, immediateTarget, 0.5f * dt);
			}
			else if (useNavmesh)
			{
				if (NavMesh.SamplePosition(transform.position, out var nmHit, 1f, -1))
				{
					Vector3 p = transform.position;
					p.y = (p.y * 0.5f + nmHit.position.y * 1.5f) / 2f;
					transform.position = p;
				}
				else
				{
					Vector3 p2 = transform.position;
					p2.y = (p2.y + immediateTarget.y) / 2f;
					transform.position = p2;
				}
			}
			else
			{
				Vector3 p3 = transform.position;
				p3.y = immediateTarget.y;
				transform.position = Vector3.MoveTowards(transform.position, p3, 0.1f * dt);
			}
			UpdateRotation(context, targetRotation, dt);
		}

		private void UpdateRotation(InteractionContext context, Quaternion targetRotation, float dt)
		{
			if (!(targetRotation != transform.rotation))
			{
				return;
			}
			if (animationState != AnimationState.Walk && animated)
			{
				float angle = UnityUtils.FromToAxisAngle(transform.forward, targetRotation * Vector3.forward, Vector3.up);
				bool endRotation = Mathf.Abs(angle) < 1f;
				AnimationState targetAnimation = ((angle > 0f) ? AnimationState.RotateRight : AnimationState.RotateLeft);
				if (!endRotation)
				{
					if (animationState == AnimationState.None)
					{
						if (targetAnimation == AnimationState.RotateLeft)
						{
							animated = context.AnimationController.StartAnimation(settings.TurnLeftClip);
						}
						else
						{
							animated = context.AnimationController.StartAnimation(settings.TurnRightClip);
						}
						context.AnimationController.TimeScale = settings.TimeScale;
						animationState = targetAnimation;
					}
					else if (animationState != targetAnimation)
					{
						endRotation = true;
					}
				}
				if (endRotation)
				{
					if (animationState == AnimationState.RotateLeft || animationState == AnimationState.RotateRight)
					{
						context.AnimationController.TimeScale = 0f;
						animationState = AnimationState.None;
					}
					transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 100f * dt);
				}
			}
			else
			{
				transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 200f * dt);
			}
		}

		private bool UpdatePath()
		{
			if (path.Count == 0)
			{
				return false;
			}
			if (useNavmesh && !freshPath)
			{
				freshPath = true;
				if (NavMesh.CalculatePath(transform.position, target, -1, navPath))
				{
					path.Clear();
					NavMeshUtils.ReadPathInto(navPath, path);
					if (navPath.status == NavMeshPathStatus.PathPartial)
					{
						if (navPath.corners.Length == 1)
						{
							path.Clear();
						}
						path.Add(target);
					}
				}
			}
			Vector3 point = path[0];
			if ((point - transform.position).magnitude < 0.1f * scaleFactor)
			{
				path.RemoveAt(0);
				return UpdatePath();
			}
			return true;
		}

		public override bool IsComplete(InteractionContext context)
		{
			if (base.IsComplete(context) && path.Count == 0 && transform.position == target)
			{
				return transform.rotation == orientation;
			}
			return false;
		}
	}

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new GoToAnimatorService());
	}
}
