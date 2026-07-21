using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class SeqClipConverter
{
	public static Dictionary<(string, bool, string, AnimationCompletionMode), SeqClip> CLIP_CACHE = new Dictionary<(string, bool, string, AnimationCompletionMode), SeqClip>();

	public static SeqClip Create(PoseAnimationClip pac, IReadOnlyList<Transform> bones, bool additive = false, string startLabel = null, bool breakOnLabel = false)
	{
		(List<string>, List<int>) tuple = CreateIndices(pac, bones);
		List<string> namesIndex = tuple.Item1;
		List<int> boneIndices = tuple.Item2;
		float loopTimeIndex = 0f;
		Dictionary<PoseAnimationFrame, float> closedset = new Dictionary<PoseAnimationFrame, float>();
		List<float> timelineBuilder = new List<float>();
		List<AnimationKeyFrame> keyframesBuilder = new List<AnimationKeyFrame>();
		float time = 0f;
		if (pac.States.Count > 0)
		{
			PoseAnimationFrame start = pac.States[0];
			if (startLabel != null)
			{
				start = pac.Labels[startLabel];
			}
			PoseAnimationFrame state = start;
			int lastKeyFrame = -1;
			while (state != null && (!breakOnLabel || state.Label == null || !(state.Label != startLabel)))
			{
				if (closedset.TryGetValue(state, out var _loopTimestamp))
				{
					if (state.Key != lastKeyFrame && state.FadeIn > 0f && pac.RootMotionType == RootMotionType.None)
					{
						BoneConfiguration bc0 = pac.Frames[state.Key];
						time += state.FadeIn;
						keyframesBuilder.Add(CreateFrame(bc0, keyframesBuilder, namesIndex, additive, pac));
						timelineBuilder.Add(time);
					}
					loopTimeIndex = _loopTimestamp;
					break;
				}
				lastKeyFrame = state.Key;
				BoneConfiguration bc1 = pac.Frames[state.Key];
				if (state.FadeIn >= 0f)
				{
					time += state.FadeIn;
					keyframesBuilder.Add(CreateFrame(bc1, keyframesBuilder, namesIndex, additive, pac));
					timelineBuilder.Add(time);
					closedset[state] = time;
				}
				else
				{
					closedset[state] = time;
				}
				if (state.MaxDuration > 0f)
				{
					time += state.MaxDuration;
					keyframesBuilder.Add(CreateFrame(bc1, keyframesBuilder, namesIndex, additive, pac));
					timelineBuilder.Add(time);
				}
				if (state.Next.Count <= 0)
				{
					break;
				}
				state = state.Next[0];
			}
		}
		else
		{
			BoneConfiguration f = pac.Frames[0];
			timelineBuilder.Add(1f);
			keyframesBuilder.Add(new AnimationKeyFrame(f, namesIndex));
		}
		float[] timeline = timelineBuilder.ToArray();
		AnimationKeyFrame[] armatureKeys = keyframesBuilder.ToArray();
		DeduplicateFrameEntities(armatureKeys);
		return new SeqClip(pac, additive, timeline, armatureKeys, boneIndices.ToArray(), loopTimeIndex, startLabel);
	}

	private static (List<string>, List<int>) CreateIndices(PoseAnimationClip source, IReadOnlyList<Transform> bones)
	{
		BoneConfiguration a = source.Frames[0];
		List<int> indices = new List<int>();
		List<string> index = new List<string>();
		for (int i = 0; i < bones.Count; i++)
		{
			if (a.Rotations.ContainsKey(bones[i].name))
			{
				indices.Add(i);
				index.Add(bones[i].name);
			}
		}
		return (index, indices);
	}

	private static AnimationKeyFrame CreateFrame(BoneConfiguration bc, List<AnimationKeyFrame> keyframes, List<string> index, bool Additive, PoseAnimationClip source)
	{
		int hipIndex = index.IndexOf("CC_Base_Hip");
		AnimationKeyFrame bce = new AnimationKeyFrame(bc, index);
		if (Additive)
		{
			if (keyframes.Count == 0)
			{
				bce.Rotations.Fill(Quaternion.identity);
				bce.HipOffset = Vector3.zero;
			}
			else
			{
				int k = source.States[0].Key;
				AnimationKeyFrame refPose = new AnimationKeyFrame(source.Frames[k], index);
				for (int i = 0; i < refPose.Rotations.Length; i++)
				{
					Quaternion a = bce.Rotations[i];
					Quaternion b = refPose.Rotations[i];
					bce.Rotations[i] = Quaternion.Inverse(b) * a;
				}
				bce.HipOffset -= refPose.HipOffset;
			}
			return bce;
		}
		if (keyframes.Count == 0)
		{
			return bce;
		}
		if (source.RootMotionType == RootMotionType.HipForward)
		{
			float baseY = keyframes[0].HipOffset.y;
			float dy = bc.HipOffset.y - baseY;
			Vector3 motion = Vector3.forward * dy;
			bce.HipOffset -= new Vector3(0f, dy, 0f);
			bce.RootMotionOffset = motion;
			return bce;
		}
		if (source.RootMotionType == RootMotionType.HipSpin)
		{
			Quaternion baseRot = keyframes[0].Rotations[hipIndex];
			Quaternion currRot = bc.Rotations["CC_Base_Hip"];
			Vector3 mappingVector = Vector3.up;
			float angle = UnityUtils.FromToAxisAngle(baseRot * mappingVector, currRot * mappingVector, Vector3.forward);
			bce.Rotations[hipIndex] = currRot * Quaternion.Inverse(Quaternion.AngleAxis(angle, Vector3.forward));
			bce.RootMotionRotation = Quaternion.AngleAxis(angle, Vector3.up);
			return bce;
		}
		return bce;
	}

	private static void DeduplicateFrameEntities(AnimationKeyFrame[] armatureKeys)
	{
		Dictionary<BoneMuscleData, BoneMuscleData> muscleDatas = new Dictionary<BoneMuscleData, BoneMuscleData>();
		Dictionary<IKTargetData, IKTargetData> ikDatas = new Dictionary<IKTargetData, IKTargetData>();
		foreach (AnimationKeyFrame k in armatureKeys)
		{
			if (k.Muscles != null)
			{
				if (!muscleDatas.TryGetValue(k.Muscles, out var value))
				{
					BoneMuscleData boneMuscleData = (muscleDatas[k.Muscles] = k.Muscles);
					value = boneMuscleData;
				}
				k.Muscles = value;
			}
			if (k.IKTargets != null)
			{
				if (!ikDatas.TryGetValue(k.IKTargets, out var value2))
				{
					IKTargetData iKTargetData = (ikDatas[k.IKTargets] = k.IKTargets);
					value2 = iKTargetData;
				}
				k.IKTargets = value2;
			}
		}
	}
}
