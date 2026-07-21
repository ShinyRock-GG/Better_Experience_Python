using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.Globales.Mapas;
using Assets.Base.Bones.Gizmos.Runtime;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Physics;
using RootMotion.FinalIK;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class RelIKTargeting
{
	private class RelIKEffector
	{
		private List<Collider> collidersToDisable = new List<Collider>();

		public InteractionTarget InteractionTarget { get; }

		public BoxCollider BoxCollider { get; set; }

		public IKTarget ImmediateIKTarget { get; set; }

		public IKTransitionState Transition { get; set; } = new IKTransitionState();

		public RelIKEffector(InteractionTarget effector)
		{
			InteractionTarget = effector;
		}

		public void DisableCollision(string hitskinName, Transform t)
		{
			List<Collider> list = collidersToDisable;
			Transform hitskin = t.FindDeepChild(hitskinName);
			if (!(BoxCollider != null) || !(hitskin != null))
			{
				return;
			}
			Collider[] componentsInChildren = hitskin.GetComponentsInChildren<Collider>();
			foreach (Collider hsCollider in componentsInChildren)
			{
				if (!list.Contains(hsCollider))
				{
					list.Add(hsCollider);
				}
			}
		}

		public void SetSelfCollisionState(bool state)
		{
			foreach (Collider c in collidersToDisable)
			{
				if (c != null)
				{
					c.enabled = state;
				}
			}
		}
	}

	public class IKTarget
	{
		public Transform Transform { get; set; }

		public Vector3 LocalOffset { get; set; }

		public Quaternion LocalRotation { get; set; }
	}

	private class IKTransitionState
	{
		public float Duration { get; set; }

		public float Length { get; set; }

		public IKTarget From { get; set; }

		public IKTarget To { get; set; }

		public TransformDisposition TargetDisposition { get; internal set; }

		public float Weight { get; internal set; }
	}

	private static List<string> MANDATORY_EFFECTORS = new List<string>(new string[2] { "CC_Base_Hand.L", "CC_Base_Hand.R" });

	private Logger logger = Logger.Create<RelIKTargeting>();

	private GizmosDeSkeleton skeleton;

	private PhysicalPuppet puppet;

	private List<RelIKEffector> ikEffectors = new List<RelIKEffector>();

	public int LayerMask { get; private set; }

	public int SkinDepenetrationLayerMask { get; }

	public bool EffectorTracking { get; set; }

	public IEnumerable<KeyValuePair<Transform, IKTarget>> EnumerateEffectorTargets()
	{
		foreach (RelIKEffector kv in ikEffectors)
		{
			if (kv.Transition.To != null)
			{
				yield return new KeyValuePair<Transform, IKTarget>(kv.InteractionTarget.transform, kv.Transition.To);
			}
		}
	}

	public void BeginTransition()
	{
		if (logger.EnableDebug)
		{
			logger.Debug("LIMBIK begin transition");
		}
		foreach (RelIKEffector s in ikEffectors)
		{
			s.Transition.From = s.Transition.To;
			s.Transition.To = null;
		}
	}

	public void EndTransition()
	{
		if (logger.EnableDebug)
		{
			logger.Debug("LIMBIK end transition");
		}
	}

	public RelIKTargeting(GizmosDeSkeleton skeleton, PhysicalPuppet puppet, Transform ownToConvex, ScopeSupport scope)
	{
		this.skeleton = skeleton;
		this.puppet = puppet;
		InteractionTarget[] componentsInChildren = skeleton.GetComponentsInChildren<InteractionTarget>();
		foreach (InteractionTarget effector in componentsInChildren)
		{
			RelIKEffector ik = new RelIKEffector(effector);
			ik.BoxCollider = (BoxCollider)puppet.ColliderByName(ik.InteractionTarget.name).FirstOrDefault((Collider x) => x is BoxCollider);
			if (ik.BoxCollider == null && MANDATORY_EFFECTORS.Contains(effector.name))
			{
				SceneWarnings.Instance.Report("RelIK error: failed to locate BoxCollider for " + effector.name);
			}
			ikEffectors.Add(ik);
		}
		RelIKEffector handL = LimbIKByBone("CC_Base_Hand.L");
		if (handL != null)
		{
			handL.DisableCollision("HandHitSkin.L", puppet.Root.transform);
			handL.DisableCollision("CC_Base_Hand.L", puppet.Root.transform);
			handL.DisableCollision("HitAnim.AnteBrazo.L", puppet.Root.transform);
			handL.DisableCollision("HitAnim.Brazo.L", puppet.Root.transform);
			handL.DisableCollision("CC_Base_Forearm.L_Dynamic", ownToConvex);
			handL.DisableCollision("CC_Base_Upperarm.L_Dynamic", ownToConvex);
			handL.DisableCollision("CC_Base_Hand.L_Dynamic", ownToConvex);
		}
		RelIKEffector handR = LimbIKByBone("CC_Base_Hand.R");
		if (handR != null)
		{
			handR.DisableCollision("HandHitSkin.R", puppet.Root.transform);
			handR.DisableCollision("CC_Base_Hand.R", puppet.Root.transform);
			handR.DisableCollision("HitAnim.AnteBrazo.R", puppet.Root.transform);
			handR.DisableCollision("HitAnim.Brazo.R", puppet.Root.transform);
			handR.DisableCollision("CC_Base_Forearm.R_Dynamic", ownToConvex);
			handR.DisableCollision("CC_Base_Upperarm.R_Dynamic", ownToConvex);
			handR.DisableCollision("CC_Base_Hand.R_Dynamic", ownToConvex);
		}
		ConfiguracionGlobal.Layers layers = MapaSingleton<ConfiguracionGlobal>.instance.layers;
		LayerMask = layers.ragdoll.ToLayerMask();
		SkinDepenetrationLayerMask = layers.skins.ToLayerMask() | layers.convexSkins.ToLayerMask() | layers.toSkinConvexCollider.ToLayerMask() | layers.toSkinCollider.ToLayerMask();
		puppet.OnBeforeIKsUpdate.Add(OnBeforeIKsUpdate, scope);
		puppet.OnIKsUpdated.Add(OnAfterIKsUpdated, scope);
	}

	private void OnAfterIKsUpdated()
	{
		if (EffectorTracking)
		{
			UpdateImmediateIKTargets();
		}
		foreach (RelIKEffector e in ikEffectors)
		{
			if (e.Transition.To != null)
			{
				UpdateEffectorTarget(e);
			}
		}
	}

	private void OnBeforeIKsUpdate()
	{
		foreach (RelIKEffector kv in ikEffectors)
		{
			if (kv.BoxCollider != null && kv.Transition.To != null)
			{
				RetargetLimb2(kv);
			}
		}
	}

	private void UpdateEffectorTarget(RelIKEffector effector)
	{
		if (effector.Transition.To != null)
		{
			IKTransitionState transition = effector.Transition;
			float blend = ((transition.Length > 0f) ? (transition.Duration / transition.Length) : 1f);
			Vector3 targetPos;
			Quaternion targetRot;
			if (transition.From == null || transition.Length <= 0f || transition.Duration >= transition.Length)
			{
				IKTarget value = transition.To;
				targetPos = value.Transform.TransformPoint(value.LocalOffset);
				targetRot = value.Transform.rotation * value.LocalRotation;
			}
			else
			{
				IKTarget aValue = transition.From;
				Vector3 aPos = aValue.Transform.TransformPoint(aValue.LocalOffset);
				Quaternion aRot = aValue.Transform.rotation * aValue.LocalRotation;
				IKTarget bValue = transition.To;
				Vector3 bPos = bValue.Transform.TransformPoint(bValue.LocalOffset);
				Quaternion bRot = bValue.Transform.rotation * bValue.LocalRotation;
				targetPos = Vector3.Lerp(aPos, bPos, blend);
				targetRot = Quaternion.Lerp(aRot, bRot, blend);
			}
			effector.Transition.Weight = blend;
			effector.Transition.TargetDisposition = new TransformDisposition(targetPos, targetRot);
			TransformDisposition bak = new TransformDisposition(effector.InteractionTarget.transform);
			effector.Transition.TargetDisposition.Apply(effector.InteractionTarget.transform);
			effector.SetSelfCollisionState(state: false);
			DepenetrateBox(effector);
			effector.SetSelfCollisionState(state: true);
			effector.Transition.TargetDisposition = new TransformDisposition(effector.InteractionTarget.transform);
			bak.Apply(effector.InteractionTarget.transform);
			if (transition.Duration < transition.Length)
			{
				transition.Duration += Time.deltaTime;
			}
		}
	}

	private void RetargetLimb2(RelIKEffector effector)
	{
		if (!(effector.BoxCollider == null))
		{
			IKTransitionState transition = effector.Transition;
			InteractionTarget target = effector.InteractionTarget;
			TransformDisposition animatedTarget = new TransformDisposition(target.transform);
			Vector3 targetPos = transition.TargetDisposition.Position;
			Quaternion targetRot = transition.TargetDisposition.Rotation;
			float weight = transition.Weight;
			targetPos = Vector3.Lerp(animatedTarget.Position, targetPos, weight);
			targetRot = Quaternion.Lerp(animatedTarget.Rotation, targetRot, weight);
			target.transform.SetPositionAndRotation(targetPos, targetRot);
		}
	}

	private void DepenetrateBox(RelIKEffector effector)
	{
		InteractionTarget target = effector.InteractionTarget;
		Vector3 queryOffset = target.transform.up * 0.25f * 0.5f;
		target.transform.position += queryOffset;
		Vector3 dir = -target.transform.up;
		if (effector.BoxCollider.QueryRaycast(target.transform, dir, out var hit, SkinDepenetrationLayerMask, 0.25f))
		{
			target.transform.position = target.transform.position + dir * hit.distance;
		}
	}

	private void UpdateImmediateIKTargets()
	{
		foreach (RelIKEffector e in ikEffectors)
		{
			BoxCollider box = e.BoxCollider;
			if (!(box == null))
			{
				Transform target = e.InteractionTarget.transform;
				Tracer.DrawWireBox(target, box.bounds, (Color?)null);
				e.SetSelfCollisionState(state: false);
				if (box.QueryRaycast(target, -target.up, out var hit, LayerMask, 0.25f))
				{
					e.ImmediateIKTarget = new IKTarget
					{
						Transform = hit.collider.transform,
						LocalOffset = hit.collider.transform.InverseTransformPoint(target.position),
						LocalRotation = Quaternion.Inverse(hit.collider.transform.rotation) * target.rotation
					};
				}
				else
				{
					e.ImmediateIKTarget = null;
				}
				e.SetSelfCollisionState(state: true);
			}
		}
	}

	private RelIKEffector LimbIKByBone(string boneName, bool probe = false)
	{
		foreach (RelIKEffector limbik in ikEffectors)
		{
			if (limbik.InteractionTarget.name == boneName)
			{
				return limbik;
			}
		}
		if (!probe)
		{
			logger.Error("Limbik by bone bone not found {0}", boneName);
		}
		return null;
	}

	public IKTarget GetImmediateIKTarget(Transform t)
	{
		return LimbIKByBone(t.name)?.ImmediateIKTarget;
	}

	public IKTarget GetIKTarget(Transform t)
	{
		return LimbIKByBone(t.name, probe: true)?.Transition.To;
	}

	public void SetIKTarget(Transform t, IKTarget target, float length)
	{
		RelIKEffector limbik = LimbIKByBone(t.name);
		if (limbik == null)
		{
			return;
		}
		if (target == null)
		{
			limbik.Transition.To = null;
			return;
		}
		IKTransitionState state = limbik.Transition;
		if (state.To != null)
		{
			state.From = state.To;
		}
		state.To = target;
		state.Duration = 0f;
		state.Length = length;
	}
}
