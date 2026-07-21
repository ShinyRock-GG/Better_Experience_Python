using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class AnimationKeyFrame
{
	public Quaternion[] Rotations { get; set; }

	public BoneMuscleData Muscles { get; set; } = new BoneMuscleData();

	public IKTargetData IKTargets { get; set; } = new IKTargetData();

	public Vector3 RootMotionOffset { get; set; } = Vector3.zero;

	public Quaternion RootMotionRotation { get; set; } = Quaternion.identity;

	public Vector3 HipOffset { get; set; }

	public Vector3 RootOffset { get; set; }

	public Quaternion RootRotation { get; }

	public GesturesData Gestures { get; set; }

	public AnimationKeyFrame(BoneConfiguration a, List<string> index)
	{
		if (a.Muscles != null)
		{
			Muscles.AddRange(a.Muscles);
		}
		if (a.IKTargets != null)
		{
			IKTargets.AddRange(a.IKTargets);
		}
		if (a.Rotations != null)
		{
			Rotations = new Quaternion[index.Count];
			for (int i = 0; i < index.Count; i++)
			{
				if (a.Rotations.TryGetValue(index[i], out var q))
				{
					Rotations[i] = q;
				}
			}
		}
		HipOffset = a.HipOffset;
		RootOffset = a.RootOffset;
		RootRotation = a.RootRotation;
		Gestures = a.Gestures;
	}
}
