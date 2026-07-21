using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class BoneConfiguration
{
	public BoneRotationData Rotations { get; set; } = new BoneRotationData();

	public BonePositionData Positions { get; set; } = new BonePositionData();

	public BoneMuscleData Muscles { get; set; } = new BoneMuscleData();

	public IKTargetData IKTargets { get; set; } = new IKTargetData();

	public Vector3 HipOffset { get; set; }

	public Vector3 RootOffset { get; set; }

	public Quaternion RootRotation { get; set; }

	public GesturesData Gestures { get; set; }

	public BoneConfiguration()
	{
	}

	public BoneConfiguration(BoneConfiguration a)
	{
		if (a.Rotations != null)
		{
			Rotations.AddRange(a.Rotations);
		}
		if (a.Muscles != null)
		{
			Muscles.AddRange(a.Muscles);
		}
		if (a.IKTargets != null)
		{
			IKTargets.AddRange(a.IKTargets);
		}
		if (a.Positions != null)
		{
			Positions.AddRange(a.Positions);
		}
		HipOffset = a.HipOffset;
		RootOffset = a.RootOffset;
		RootRotation = a.RootRotation;
	}
}
