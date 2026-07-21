using System.Collections.Generic;
using Assets._ReusableScripts.BoneColliders;
using BetterExperience.CustomScene;
using BetterExperience.GameScopes;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.Features;

internal class GuestSelfCollisionFeature : StoryService
{
	private int ragdollLayer = LayerMask.NameToLayer("Ragdoll");

	private int toSkinConvexCollider = LayerMask.NameToLayer("ToSkinConvexCollider");

	private DispatcherService dispatcher;

	private List<(Transform, Transform)> following = new List<(Transform, Transform)>();

	private PuppetMaster puppetMaster => base.Session.Guest.Puppet.PuppetMaster;

	private Transform rootTransform => ((Component)(object)base.Session.Guest.Impl).gameObject.transform;

	public override void OnStart()
	{
		base.OnStart();
		dispatcher = Lookup<DispatcherService>();
		Lookup<InteractionManager>().AnimationController.ActiveStateChanged.Add(EnableMuscleSelfCollision, base.Scope);
		if (base.Session.Guest.IsMaterialized)
		{
			Install();
		}
		else
		{
			base.Session.Guest.GuestMaterialized += Install;
		}
	}

	private void Install()
	{
		Physics.IgnoreLayerCollision(ragdollLayer, toSkinConvexCollider, ignore: false);
		InstallExtendedColliders();
		dispatcher.DoUpdate.Add(OnUpdate, base.Scope);
	}

	private void OnUpdate()
	{
		foreach (var kv in following)
		{
			kv.Item2.localRotation = kv.Item1.localRotation;
			kv.Item2.localPosition = kv.Item1.localPosition;
			kv.Item2.localScale = kv.Item1.localScale;
		}
	}

	private void InstallExtendedColliders()
	{
		Transform hitskinr = rootTransform.FindDeepChild("HandHitSkin.R");
		Transform puppetr = puppetMaster.gameObject.transform.FindDeepChild("CC_Base_Hand.R");
		CopyTransforms(hitskinr, puppetr);
		puppetMaster.GetMuscle(HumanBodyBones.RightHand).UpdateColliders();
		Transform hitskinl = rootTransform.FindDeepChild("HandHitSkin.L");
		Transform puppetl = puppetMaster.gameObject.transform.FindDeepChild("CC_Base_Hand.L");
		CopyTransforms(hitskinl, puppetl);
		puppetMaster.GetMuscle(HumanBodyBones.LeftHand).UpdateColliders();
	}

	private void CopyTransforms(Transform hitskinr, Transform puppetr)
	{
		foreach (Transform child in hitskinr)
		{
			if (!child.name.Contains("Hand"))
			{
				Transform copy = Object.Instantiate(child, puppetr);
				copy.name = child.name;
				if (copy.name.Contains("01"))
				{
					copy.gameObject.layer = ragdollLayer;
				}
				else
				{
					copy.gameObject.layer = toSkinConvexCollider;
				}
				copy.localPosition = child.localPosition;
				copy.localRotation = child.localRotation;
				copy.localScale = child.localScale;
				AnimatorDedoParteCollider adpc = copy.GetComponent<AnimatorDedoParteCollider>();
				if ((bool)adpc)
				{
					adpc.enabled = false;
				}
				following.Add((child, copy));
			}
		}
		foreach (var item in following)
		{
			Collider c1 = item.Item2.GetComponent<Collider>();
			if (c1 == null)
			{
				continue;
			}
			foreach (var item2 in following)
			{
				Collider c2 = item2.Item2.GetComponent<Collider>();
				if (!(c2 == null) && c1 != c2)
				{
					Physics.IgnoreCollision(c1, c2, ignore: true);
				}
			}
			Physics.IgnoreCollision(c1, puppetr.GetComponent<Collider>(), ignore: true);
		}
	}

	private void EnableMuscleSelfCollision()
	{
		HumanBodyBones[] sourceBones = new HumanBodyBones[4]
		{
			HumanBodyBones.LeftHand,
			HumanBodyBones.LeftLowerArm,
			HumanBodyBones.RightHand,
			HumanBodyBones.RightLowerArm
		};
		HumanBodyBones[] targetBones = new HumanBodyBones[8]
		{
			HumanBodyBones.Spine,
			HumanBodyBones.Hips,
			HumanBodyBones.Chest,
			HumanBodyBones.Neck,
			HumanBodyBones.LeftUpperLeg,
			HumanBodyBones.RightUpperLeg,
			HumanBodyBones.LeftLowerLeg,
			HumanBodyBones.RightLowerLeg
		};
		HumanBodyBones[] array = sourceBones;
		foreach (HumanBodyBones bone in array)
		{
			Muscle m1 = puppetMaster.GetMuscle(bone);
			if (m1 == null)
			{
				continue;
			}
			HumanBodyBones[] array2 = targetBones;
			foreach (HumanBodyBones receiver in array2)
			{
				Muscle m2 = puppetMaster.GetMuscle(receiver);
				if (m2 != null)
				{
					m1.IgnoreCollisions(m2, ignore: false);
				}
			}
		}
	}
}
