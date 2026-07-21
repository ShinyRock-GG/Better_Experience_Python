using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using UnityEngine;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class SmoothMotionLoop
{
	private Logger logger = Logger.Create<SmoothMotionLoop>();

	private PoseAnimationClip clip;

	public bool HasForwardMotion { get; set; }

	public bool TransferPose { get; set; } = true;

	public SmoothMotionLoop(PoseAnimationClip clip)
	{
		this.clip = clip;
	}

	public void Process()
	{
		logger.Info("Starting process");
		if (clip.States.Count < 2)
		{
			logger.Info("No frames to smooth");
			return;
		}
		if (clip.RootMotionType == RootMotionType.None)
		{
			logger.Error("No root motion type");
		}
		PoseAnimationFrame lastState = clip.States.Last();
		BoneConfiguration lastFrame = clip.Frames[lastState.Key];
		PoseAnimationFrame firstState = clip.States[0];
		BoneConfiguration firstFrame = clip.Frames[firstState.Key];
		BoneConfiguration newFrame = SmoothFrames(firstFrame, lastFrame);
		if (!HasForwardMotion)
		{
			for (int i = 0; i < clip.Frames.Count; i++)
			{
				clip.Frames[i].HipOffset = firstFrame.HipOffset;
			}
		}
		int ndx = clip.Frames.Count;
		clip.Frames.Add(newFrame);
		lastState.Key = ndx;
	}

	private BoneConfiguration SmoothFrames(BoneConfiguration firstFrame, BoneConfiguration lastFrame)
	{
		BoneConfiguration result = new BoneConfiguration(lastFrame);
		Vector3 h = firstFrame.HipOffset;
		result.HipOffset = new Vector3(h.x, HasForwardMotion ? lastFrame.HipOffset.y : h.y, h.z);
		if (TransferPose && lastFrame.Rotations != null)
		{
			foreach (KeyValuePair<string, Quaternion> kv in firstFrame.Rotations)
			{
				if (!(kv.Key == "CC_Base_BoneRoot") && (!(kv.Key == "CC_Base_Hip") || HasForwardMotion) && firstFrame.Rotations.TryGetValue(kv.Key, out var val))
				{
					result.Rotations[kv.Key] = val;
				}
			}
		}
		return result;
	}
}
