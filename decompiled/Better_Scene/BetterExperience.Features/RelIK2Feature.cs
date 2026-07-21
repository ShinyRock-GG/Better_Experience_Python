using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets.TValle.BeachGirl;
using BetterExperience.CustomScene;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Physics;
using com.ootii.Actors;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.Features;

public class RelIK2Feature : PluginService
{
	public class RelIK2Service : SessionService
	{
		private IKFeature.AutoIKService autoIK;

		private List<IKAnchor> ikAnchors = new List<IKAnchor>();

		private Dictionary<string, IKAnchor> ikAnchorIndex = new Dictionary<string, IKAnchor>();

		private List<RelIKEffector> effectors = new List<RelIKEffector>();

		public IKFeature.EffectorOffset playerRootOffset = new IKFeature.EffectorOffset();

		private InteractionManager im;

		private ActorController playerActorCtl;

		public IReadOnlyList<IKAnchor> Anchors => ikAnchors;

		private void AddAnchor(string name, Transform transform)
		{
			IKAnchor a = new IKAnchor(name, transform);
			ikAnchors.Add(a);
			ikAnchorIndex.Add(name, a);
		}

		public override void OnStart()
		{
			base.OnStart();
			autoIK = Lookup<IKFeature.AutoIKService>();
			PhysicalPuppet puppet = new PhysicalPuppet(base.Session.Player.GameObject);
			Muscle[] muscles = puppet.PuppetMaster.muscles;
			foreach (Muscle muscle in muscles)
			{
				string name = "P.Muscle." + muscle.name;
				AddAnchor(name, muscle.transform);
			}
			AddAnchor("P.Pene.Tip", ((IPene)base.Session.Player.Character.pene).partePunta);
			AddAnchor("P.Root", base.Session.Player.RootMotion);
			im = Lookup<InteractionManager>();
			Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
			playerActorCtl = base.Session.Player.GameObject.GetComponentInChildren<ActorController>();
		}

		private void OnUpdate()
		{
			foreach (RelIKEffector effector in effectors)
			{
				effector.Update();
			}
			if (playerRootOffset.Weight > 0f)
			{
				Transform rt = base.Session.Player.RootMotion;
				if (playerRootOffset.OffsetEnabled)
				{
					Vector3 required = rt.position - playerRootOffset.Offset;
					rt.position = Vector3.MoveTowards(rt.position, required, 5f * Time.deltaTime);
				}
				if (playerRootOffset.AngleEnabled)
				{
					Quaternion required2 = rt.rotation * playerRootOffset.Angle;
					Quaternion t = Quaternion.RotateTowards(rt.rotation, required2, 1200f * Time.deltaTime);
					playerActorCtl.SetRotation(t);
				}
			}
		}

		public RelIKEffector RequestEffector(IKEffectorType type, ScopeSupport scope)
		{
			IKFeature.EffectorOffset effectorMod;
			switch (type)
			{
			case IKEffectorType.HandLeft:
				effectorMod = autoIK.GetManagedEffector(HumanBodyBones.LeftHand).RequestModifier(scope);
				break;
			case IKEffectorType.HandRight:
				effectorMod = autoIK.GetManagedEffector(HumanBodyBones.RightHand).RequestModifier(scope);
				break;
			case IKEffectorType.ShoulderLeft:
				effectorMod = autoIK.GetManagedEffector(HumanBodyBones.LeftShoulder).RequestModifier(scope);
				break;
			case IKEffectorType.ShoulderRight:
				effectorMod = autoIK.GetManagedEffector(HumanBodyBones.RightShoulder).RequestModifier(scope);
				break;
			case IKEffectorType.PlayerRoot:
				effectorMod = playerRootOffset;
				break;
			default:
				logger.Error("No effector for {0}", type);
				return null;
			}
			RelIKEffector relIKEffector = new RelIKEffector(type);
			relIKEffector.Private.Mod = effectorMod;
			effectors.Add(relIKEffector);
			scope.OnDispose += delegate
			{
				effectors.Remove(relIKEffector);
			};
			relIKEffector.Private.StaticTransform = im.AnimationController.PostureOffset;
			return relIKEffector;
		}

		public IKEffectorSet RequestEffectorSet(ScopeSupport scope)
		{
			return new IKEffectorSet(this, scope);
		}
	}

	public class RelIKEffector
	{
		public IKEffectorType Type { get; }

		public IKAnchor Anchor { get; set; }

		public bool EnableOffset { get; set; }

		public bool EnableAngle { get; set; }

		public Vector3 Offset { get; set; }

		public Quaternion Angle { get; set; }

		public float Weight { get; set; } = 1f;

		public RelIKEffectorPrivate Private { get; } = new RelIKEffectorPrivate();

		private Transform anchorTransform
		{
			get
			{
				if (Anchor == null)
				{
					return null;
				}
				return Anchor.Transform;
			}
		}

		public RelIKEffector(IKEffectorType type)
		{
			Type = type;
		}

		public void SetOffsetFromCurrentState()
		{
			if (anchorTransform != null)
			{
				Offset = Private.StaticTransform.InverseTransformPoint(anchorTransform.position);
			}
			else
			{
				Offset = Vector3.zero;
			}
			if (anchorTransform != null)
			{
				Angle = Quaternion.Inverse(Private.StaticTransform.rotation) * anchorTransform.rotation;
			}
			else
			{
				Angle = Quaternion.identity;
			}
		}

		private bool NeedsUpdate()
		{
			if (EnableAngle && !Private.Mod.AngleEnabled)
			{
				return true;
			}
			if (EnableOffset && !Private.Mod.OffsetEnabled)
			{
				return true;
			}
			_ = Type;
			_ = 4;
			return true;
		}

		public void Update()
		{
			bool update = NeedsUpdate();
			if (EnableOffset && anchorTransform != null)
			{
				if (update)
				{
					Private.AnchorPosition = anchorTransform.position;
				}
				Private.CorrectionOffset = Private.AnchorPosition - Private.StaticTransform.TransformPoint(Offset);
			}
			else
			{
				Private.CorrectionOffset = Vector3.zero;
			}
			if (EnableAngle && anchorTransform != null)
			{
				if (update)
				{
					Private.AnchorRotation = anchorTransform.rotation;
				}
				Quaternion targetRotation = Private.StaticTransform.rotation * Angle;
				Private.CorrectionAngle = Quaternion.Inverse(Private.AnchorRotation) * targetRotation;
			}
			else
			{
				Private.CorrectionAngle = Quaternion.identity;
			}
			Private.Mod.AngleEnabled = EnableAngle;
			Private.Mod.OffsetEnabled = EnableOffset;
			Private.Mod.Offset = Private.CorrectionOffset;
			Private.Mod.Angle = Private.CorrectionAngle;
			Private.Mod.Weight = Weight;
		}

		public void Clear()
		{
			Anchor = null;
			EnableAngle = false;
			EnableOffset = false;
			Offset = Vector3.zero;
			Angle = Quaternion.identity;
		}

		public (Vector3, Quaternion) Capture()
		{
			Vector3 O = ((!(anchorTransform != null)) ? Vector3.zero : Private.StaticTransform.InverseTransformPoint(anchorTransform.position));
			Quaternion A = ((!(anchorTransform != null)) ? Quaternion.identity : (Quaternion.Inverse(Private.StaticTransform.rotation) * anchorTransform.rotation));
			return (O, A);
		}
	}

	public class RelIKEffectorPrivate
	{
		public Transform StaticTransform { get; set; }

		public Vector3 AnchorPosition { get; set; }

		public Quaternion AnchorRotation { get; set; }

		public Vector3 CorrectionOffset { get; set; }

		public Quaternion CorrectionAngle { get; set; }

		public IKFeature.EffectorOffset Mod { get; set; }
	}

	public enum IKEffectorType
	{
		HandLeft,
		HandRight,
		ShoulderLeft,
		ShoulderRight,
		PlayerRoot
	}

	public class IKAnchor
	{
		public string Id { get; }

		public Transform Transform { get; }

		public IKAnchor(string id, Transform transform)
		{
			Id = id;
			Transform = transform;
		}
	}

	public class IKEffectorSet
	{
		public IReadOnlyList<RelIKEffector> All { get; }

		public RelIKEffector HandLeft { get; }

		public RelIKEffector HandRight { get; }

		public RelIKEffector ShoulderLeft { get; }

		public RelIKEffector ShoulderRight { get; }

		public RelIKEffector PlayerRoot { get; }

		public IKEffectorSet(RelIK2Service relIK2Service, ScopeSupport scope)
		{
			HandLeft = relIK2Service.RequestEffector(IKEffectorType.HandLeft, scope);
			HandRight = relIK2Service.RequestEffector(IKEffectorType.HandRight, scope);
			ShoulderLeft = relIK2Service.RequestEffector(IKEffectorType.ShoulderLeft, scope);
			ShoulderRight = relIK2Service.RequestEffector(IKEffectorType.ShoulderRight, scope);
			PlayerRoot = relIK2Service.RequestEffector(IKEffectorType.PlayerRoot, scope);
			All = new List<RelIKEffector> { PlayerRoot, HandLeft, HandRight };
		}
	}

	public override bool Enabled => true;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new RelIK2Service());
	}
}
