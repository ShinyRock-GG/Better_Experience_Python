using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Wrappers.Physics;

public static class Constants
{
	public static readonly HumanBodyBones[][] limbs;

	internal static ListDictionary<PuppetReceiverPart, HumanBodyBones> skinToBoneMap;

	static Constants()
	{
		limbs = new HumanBodyBones[4][]
		{
			new HumanBodyBones[3]
			{
				HumanBodyBones.LeftFoot,
				HumanBodyBones.LeftLowerLeg,
				HumanBodyBones.LeftUpperLeg
			},
			new HumanBodyBones[3]
			{
				HumanBodyBones.RightFoot,
				HumanBodyBones.RightLowerLeg,
				HumanBodyBones.RightUpperLeg
			},
			new HumanBodyBones[4]
			{
				HumanBodyBones.LeftHand,
				HumanBodyBones.LeftLowerArm,
				HumanBodyBones.LeftUpperArm,
				HumanBodyBones.LeftShoulder
			},
			new HumanBodyBones[4]
			{
				HumanBodyBones.RightHand,
				HumanBodyBones.RightLowerArm,
				HumanBodyBones.RightUpperArm,
				HumanBodyBones.RightShoulder
			}
		};
		skinToBoneMap = new ListDictionary<PuppetReceiverPart, HumanBodyBones>();
		skinToBoneMap[PuppetReceiverPart.armL] = HumanBodyBones.LeftLowerArm;
		skinToBoneMap[PuppetReceiverPart.forearmL] = HumanBodyBones.LeftUpperArm;
		skinToBoneMap[PuppetReceiverPart.hand_L] = HumanBodyBones.LeftHand;
		skinToBoneMap[PuppetReceiverPart.armR] = HumanBodyBones.RightLowerArm;
		skinToBoneMap[PuppetReceiverPart.forearmR] = HumanBodyBones.RightUpperArm;
		skinToBoneMap[PuppetReceiverPart.hand_R] = HumanBodyBones.RightHand;
		skinToBoneMap[PuppetReceiverPart.foot_L] = HumanBodyBones.LeftFoot;
		skinToBoneMap[PuppetReceiverPart.canilla_L] = HumanBodyBones.LeftUpperLeg;
		skinToBoneMap[PuppetReceiverPart.leg_L] = HumanBodyBones.LeftLowerLeg;
		skinToBoneMap[PuppetReceiverPart.foot_R] = HumanBodyBones.RightFoot;
		skinToBoneMap[PuppetReceiverPart.canilla_R] = HumanBodyBones.RightUpperLeg;
		skinToBoneMap[PuppetReceiverPart.leg_R] = HumanBodyBones.RightLowerLeg;
		skinToBoneMap[PuppetReceiverPart.head] = HumanBodyBones.Head;
		skinToBoneMap[PuppetReceiverPart.torso] = HumanBodyBones.Spine;
	}
}
