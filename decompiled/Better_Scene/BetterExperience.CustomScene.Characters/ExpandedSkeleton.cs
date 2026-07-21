using Assets.Base.Bones.Gizmos.BeachGirl.Runtime;
using Assets.Base.Bones.Gizmos.Runtime;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

internal class ExpandedSkeleton : MonoBehaviour
{
	public class ExpandedSkeletonBone : MonoBehaviour
	{
		public Transform Target { get; set; }

		public void Update()
		{
			if (Target != null)
			{
				Target.localPosition = base.transform.localPosition;
				Target.localRotation = base.transform.localRotation;
			}
		}
	}

	public GizmosDeSkeleton Skeleton { get; private set; }

	internal void Init(ScopeSupport scope)
	{
		Skeleton = GetComponent<GizmosDeSkeleton>();
	}

	public ExpandedSkeletonBone CreateCustomBone(string name, Transform target)
	{
		if (target.IsChildOf(Skeleton.rootBone))
		{
			Transform template = Skeleton.rootBone.FindDeepChild("CC_Base_Knee.L");
			Transform handslot = Object.Instantiate(template, target.transform);
			handslot.name = name;
			handslot.localPosition = new Vector3(0f, -0.1f, 0f);
			handslot.localRotation = Quaternion.identity;
			handslot.GetComponent<GizmoDeBone>().boneMuscleConfig.puedeRotar = true;
			ExpandedSkeletonBone boneEx = handslot.gameObject.AddComponent<ExpandedSkeletonBone>();
			Transform animatorRoot = target.GetComponent<GizmoDeBoneRMInfo>().characterBone;
			Transform targetParent = animatorRoot.FindDeepChild(target.name);
			boneEx.Target = UnityUtils.NewTransform(name, targetParent);
			return boneEx;
		}
		return null;
	}
}
