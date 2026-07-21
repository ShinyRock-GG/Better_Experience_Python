using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class Armature : MonoBehaviour
{
	private Logger logger = new Logger(typeof(Armature));

	private List<Transform> bonesDescendingOrder = new List<Transform>();

	private Dictionary<string, ArmatureBone> boneNameMap = new Dictionary<string, ArmatureBone>();

	private List<ArmatureBone> bonesList = new List<ArmatureBone>();

	public bool dirtyBonesList;

	public static ObservableValue<bool> DrawArmature { get; } = new ObservableValue<bool>(false);

	public ArmatureBone RootBone { get; private set; }

	public PuppetMaster PuppetMaster { get; private set; }

	public bool FixTransforms { get; set; }

	public IReadOnlyList<Transform> Bones => bonesDescendingOrder;

	public float TranslationVelocity { get; set; } = 10f;

	public float RotationVelocity { get; set; } = 500f;

	public bool NoBendingTargets { get; set; }

	public ArmatureBone Head { get; private set; }

	public ArmatureBone LeftHand { get; private set; }

	public ArmatureBone RightHand { get; private set; }

	public ArmatureBone LeftFoot { get; private set; }

	public ArmatureBone RightFoot { get; private set; }

	public ArmatureBone Hip { get; private set; }

	public void InitializeArmatureSkeleton(Transform target)
	{
		if (base.transform.Find(target.name) != null)
		{
			logger.Error("Cannot initialize armature twice");
			return;
		}
		RootBone = CreateArmatureBone(target, base.transform);
		CopyTransforms(target, RootBone.transform);
		Head = boneNameMap["CC_Base_Head"];
		LeftHand = boneNameMap["CC_Base_Hand.L"];
		RightHand = boneNameMap["CC_Base_Hand.R"];
		LeftFoot = boneNameMap["CC_Base_Foot.L"];
		RightFoot = boneNameMap["CC_Base_Foot.R"];
		Hip = boneNameMap["CC_Base_Hip"];
	}

	public void InitializeArmaturePuppet(PuppetMaster pm)
	{
		PuppetMaster = pm;
		Muscle[] muscles = pm.muscles;
		foreach (Muscle muscle in muscles)
		{
			if (boneNameMap.TryGetValue(muscle.name, out var bone))
			{
				bone.Muscle = muscle;
			}
		}
	}

	private void CopyTransforms(Transform source, Transform target)
	{
		foreach (Transform ct in source)
		{
			if (ct.name.StartsWith("CC_") && (!NoBendingTargets || (!ct.name.Contains("Knee") && !ct.name.Contains("Elbow"))))
			{
				ArmatureBone tt = CreateArmatureBone(ct, target);
				CopyTransforms(ct, tt.transform);
			}
		}
	}

	private ArmatureBone CreateArmatureBone(Transform source, Transform parent)
	{
		Transform target = UnityUtils.NewTransform(source.name, parent);
		target.rotation = source.rotation;
		target.position = source.position;
		target.gameObject.SetActive(value: false);
		ArmatureBone bone = target.gameObject.AddComponent<ArmatureBone>();
		bone.Target = source;
		bone.Armature = this;
		target.gameObject.SetActive(value: true);
		boneNameMap[target.name] = bone;
		bonesDescendingOrder.Add(target);
		return bone;
	}

	public BoneConfiguration Snapshot()
	{
		BoneConfiguration bc = new BoneConfiguration();
		foreach (Transform bone in bonesDescendingOrder)
		{
			bc.Positions[bone.name] = RootBone.transform.InverseTransformPoint(bone.position);
			bc.Rotations[bone.name] = Quaternion.Inverse(RootBone.transform.rotation) * bone.rotation;
		}
		return bc;
	}

	public void ReadPose()
	{
		RootBone.ReadRecursive();
	}

	public void ReadPoseFast()
	{
		if (dirtyBonesList)
		{
			RefreshBones();
		}
		foreach (ArmatureBone x in bonesList)
		{
			x.ReadFrom();
		}
	}

	public void MapToRecursive(float dt)
	{
		if (dirtyBonesList)
		{
			RefreshBones();
		}
		foreach (ArmatureBone x in bonesList)
		{
			x.MapTo(dt);
		}
	}

	private void RefreshBones()
	{
		bonesList.Clear();
		bonesList.Add(RootBone);
		RootBone.ReadHierarchy(bonesList);
		dirtyBonesList = false;
	}
}
