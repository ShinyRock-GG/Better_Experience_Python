using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk.Interacciones;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using RootMotion.Dynamics;
using RootMotion.FinalIK;
using UnityEngine;

namespace BetterExperience.Features;

public class IKFeature : PluginFeature
{
	public class AutoIKService : StoryService
	{
		private IIKUpdater ikUpdater;

		private ManagedEffector mHandR;

		private ManagedEffector mHandL;

		private ManagedEffector mFootR;

		private ManagedEffector mFoorL;

		private List<ManagedEffector> managedEffectors = new List<ManagedEffector>();

		private List<Action> resetAction = new List<Action>();

		private IInputHandle toggle;

		private Armature armature;

		private Transform toeBaseR;

		public ConfigEntry<bool> EnableIKServices { get; internal set; }

		public override void OnStart()
		{
			//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
			base.OnStart();
			armature = Lookup<InteractionManager>().AnimationController.Armature;
			toeBaseR = armature.RootBone.transform.FindDeepChild("CC_Base_ToeBase.R");
			InteractionSystemV3 is3 = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<InteractionSystemV3>();
			is3.interacted += RewriteIK;
			ikUpdater = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<IIKUpdater>();
			ikUpdater.onAllIKsUpdated += IkUpdater_onAllIKsUpdated;
			toggle = Lookup<DispatcherService>().Input.KeyboardEvent(new KeyboardShortcut(KeyCode.U, Array.Empty<KeyCode>()), base.Scope);
			IKSolverFullBodyBiped solver = is3.ik.solver;
			PuppetMaster pm = base.Session.Guest.Puppet.PuppetMaster;
			mHandR = CreateEffector(solver.rightHandEffector, pm.GetMuscle(HumanBodyBones.RightHand), HumanBodyBones.RightHand);
			mHandL = CreateEffector(solver.leftHandEffector, pm.GetMuscle(HumanBodyBones.LeftHand), HumanBodyBones.LeftHand);
			mFootR = CreateEffector(solver.rightFootEffector, pm.GetMuscle(HumanBodyBones.RightFoot), HumanBodyBones.RightFoot);
			mFoorL = CreateEffector(solver.leftFootEffector, pm.GetMuscle(HumanBodyBones.LeftFoot), HumanBodyBones.LeftFoot);
			CreateEffector(solver.leftShoulderEffector, pm.GetMuscle(HumanBodyBones.LeftShoulder), HumanBodyBones.LeftShoulder);
			CreateEffector(solver.rightShoulderEffector, pm.GetMuscle(HumanBodyBones.RightShoulder), HumanBodyBones.RightShoulder);
			CreateEffector(solver.leftThighEffector, pm.GetMuscle(HumanBodyBones.LeftUpperLeg), HumanBodyBones.LeftUpperLeg);
			CreateEffector(solver.rightThighEffector, pm.GetMuscle(HumanBodyBones.RightUpperLeg), HumanBodyBones.RightUpperLeg);
		}

		private ManagedEffector CreateEffector(IKEffector iKEffector, Muscle muscle, HumanBodyBones bone)
		{
			ManagedEffector me = new ManagedEffector(iKEffector, muscle, bone);
			managedEffectors.Add(me);
			return me;
		}

		private void IkUpdater_onAllIKsUpdated(IIKUpdater obj)
		{
			resetAction.ForEach(delegate(Action x)
			{
				x();
			});
			resetAction.Clear();
			foreach (ManagedEffector me in managedEffectors)
			{
				me.Reset();
			}
		}

		private void RewriteIK(InteractionSystemV3 is3)
		{
			if (!EnableIKServices.Value)
			{
				return;
			}
			foreach (ManagedEffector me in managedEffectors)
			{
				me.Prepare();
			}
			foreach (ManagedEffector me2 in managedEffectors)
			{
				if (me2.AllowDepenetration)
				{
					me2.Depenetrate();
				}
			}
			foreach (ManagedEffector me3 in managedEffectors)
			{
				me3.Update();
			}
		}

		public ManagedEffector GetManagedEffector(HumanBodyBones bone)
		{
			foreach (ManagedEffector me in managedEffectors)
			{
				if (me.Bone == bone)
				{
					return me;
				}
			}
			logger.Error("Bo managed effector for bone {0}", bone);
			return null;
		}
	}

	public class EffectorOffset
	{
		public Vector3 Offset { get; set; }

		public Quaternion Angle { get; set; }

		public bool OffsetEnabled { get; set; }

		public bool AngleEnabled { get; set; }

		public float Weight { get; set; }
	}

	public class ManagedEffector
	{
		private const float DEPRESSURIZING_FACTOR = 0.75f;

		private static Collider[] hits = new Collider[10];

		private static int mask = LayerMask.GetMask("Default", "Suelo", "f. Convex Skin");

		private Logger logger;

		private BoxCollider cBox;

		private List<BoxCollider> boxes = new List<BoxCollider>();

		private Vector3 currentOffset = Vector3.zero;

		private Quaternion currentAngle = Quaternion.identity;

		private List<EffectorOffset> modifiers = new List<EffectorOffset>();

		private Vector3 resetPosition;

		private Quaternion resetRotation;

		private Vector3 motionVector;

		private Vector3 correctionOffset;

		private PoseAnimationController.InertialVectorBuffer correctionInertia = new PoseAnimationController.InertialVectorBuffer();

		private bool recovering;

		public IKEffector Effector { get; }

		public Muscle Muscle { get; }

		public HumanBodyBones Bone { get; }

		public Vector3 Offset { get; set; } = Vector3.zero;

		public Quaternion Angle { get; set; } = Quaternion.identity;

		private bool UseMuscle { get; set; }

		public bool AllowDepenetration { get; }

		private bool DrawTransforms => false;

		public ManagedEffector(IKEffector rightHandEffector, Muscle muscle, HumanBodyBones bone)
		{
			logger = new Logger(rightHandEffector.bone.name);
			Effector = rightHandEffector;
			Muscle = muscle;
			cBox = muscle.colliders[0] as BoxCollider;
			Bone = bone;
			Collider[] colliders = muscle.colliders;
			foreach (Collider cc in colliders)
			{
				if (cc is BoxCollider bc)
				{
					boxes.Add(bc);
				}
			}
			if (Muscle != null && Muscle.rigidbody.collisionDetectionMode == CollisionDetectionMode.Discrete)
			{
				Muscle.rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
			}
			UseMuscle = true;
			AllowDepenetration = false;
		}

		public EffectorOffset RequestModifier(ScopeSupport scope)
		{
			EffectorOffset offset = new EffectorOffset();
			modifiers.Add(offset);
			scope.OnDispose += delegate
			{
				modifiers.Remove(offset);
			};
			return offset;
		}

		public void Depenetrate()
		{
			if (recovering)
			{
				Muscle.rigidbody.detectCollisions = true;
			}
			if (UseMuscle)
			{
				UpdateMuscleMotion();
			}
			else
			{
				motionVector = Vector3.zero;
			}
			Vector3 offset = Offset;
			foreach (BoxCollider box in boxes)
			{
				DepenetrateBox(Effector, box);
			}
			correctionInertia.Update(Offset - offset);
			correctionOffset = Vector3.MoveTowards(correctionOffset, correctionInertia.Value, 0.2f * Time.deltaTime);
			Offset = offset + correctionOffset;
		}

		private void UpdateMuscleMotion()
		{
			Vector3 motion = Muscle.rigidbody.velocity;
			float force = motion.magnitude;
			if (force > 0.1f)
			{
				motionVector = motion.normalized;
				logger.InfoRare("Muscle {0}: {1} {2} {3}", new object[4]
				{
					Muscle.name,
					motion,
					Muscle.rigidbody.velocity,
					force
				});
				if (force > 0.3f)
				{
					recovering = true;
					Muscle.rigidbody.detectCollisions = false;
				}
			}
			else
			{
				motionVector = Vector3.zero;
			}
		}

		public void Update()
		{
			currentOffset = Vector3.MoveTowards(currentOffset, Offset, 0.5f * Time.deltaTime);
			currentAngle = Quaternion.RotateTowards(currentAngle, Angle, 10f * Time.deltaTime);
			resetPosition = Effector.position;
			Effector.position += currentOffset;
		}

		public void Reset()
		{
			Effector.position = resetPosition;
		}

		private void DepenetrateBox(IKEffector effector, BoxCollider boxL)
		{
			Vector3 localOffset = Vector3.zero;
			Quaternion localRotation = Quaternion.identity;
			if (boxL != cBox)
			{
				localOffset = effector.rotation * boxL.transform.localPosition;
				localRotation = boxL.transform.localRotation;
			}
			Matrix4x4 trs = Matrix4x4.TRS(effector.position + localOffset + Offset, effector.rotation * localRotation, Vector3.one);
			Vector3 sz = boxL.size;
			boxL.size = sz * 1.2f;
			Bounds bb = new Bounds(boxL.center, boxL.size);
			Color traceColor = Color.green;
			int hitcount = Physics.OverlapSphereNonAlloc(trs.GetPosition(), 0.1f, hits, mask);
			if (hitcount > 0)
			{
				Vector3 sum = Vector3.zero;
				float count = 0f;
				Vector3 dir = default(Vector3);
				float dist = default(float);
				for (int i = 0; i < hitcount; i++)
				{
					Collider hit = hits[i];
					if (ColliderExtensions.ComputePenetration((Collider)boxL, trs.GetPosition(), trs.rotation, hit, out dir, out dist))
					{
						dist = Mathf.Clamp(dist, 0f, 0.2f);
						if (motionVector != Vector3.zero)
						{
							dir = -motionVector;
						}
						sum += dir * dist * 1.1f;
						count += 1f;
						Offset += dir * dist * 1.1f;
						trs = Matrix4x4.TRS(effector.position + localOffset + Offset, effector.rotation * localRotation, Vector3.one);
					}
				}
				if (((count > 0f) ? (sum / count) : Vector3.zero).sqrMagnitude > 0f)
				{
					traceColor = Color.yellow;
					if (DrawTransforms)
					{
						Tracer.DrawWireBox(Matrix4x4.TRS(trs.GetPosition(), trs.rotation, Vector3.one), bb, (Color?)Color.red);
					}
				}
			}
			if (DrawTransforms)
			{
				Tracer.DrawWireBox(trs, bb, (Color?)traceColor);
			}
			boxL.size = sz;
		}

		public void Prepare()
		{
			Offset = Vector3.zero;
			float offsetSum = 0f;
			foreach (EffectorOffset mod in modifiers)
			{
				if (mod.Weight > 0f && mod.OffsetEnabled)
				{
					Offset += mod.Weight * mod.Offset;
					offsetSum += mod.Weight;
				}
			}
			if (offsetSum > 0f)
			{
				Offset /= offsetSum;
			}
			else
			{
				Offset = Vector3.zero;
			}
		}
	}

	public override bool Enabled => true;

	public ConfigEntry<bool> EnableIKServices { get; private set; }

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		EnableIKServices = config.Bind<bool>("Story", "EnableIKServices", true, "Scene: Enable IK services");
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<BetterExperience.Features.PluginOptions.PluginOptionsService>().Expose(EnableIKServices, base.Scope, (BetterExperience.Features.PluginOptions.PluginOptionsService.SettingsType)3, (string)null);
		Lookup<StoryManager>().StoryInterviewServices.Add(() => new AutoIKService
		{
			EnableIKServices = EnableIKServices
		});
	}
}
