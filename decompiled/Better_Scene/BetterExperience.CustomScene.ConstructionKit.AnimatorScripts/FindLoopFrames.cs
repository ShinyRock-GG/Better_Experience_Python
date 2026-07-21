using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;
using UnityEngine;

namespace BetterExperience.CustomScene.ConstructionKit.AnimatorScripts;

internal class FindLoopFrames
{
	private PoseAnimationClip p;

	private AnimatorScriptRegistry animatorScriptRegistry;

	public FindLoopFrames(PoseAnimationClip p, AnimatorScriptRegistry animatorScriptRegistry)
	{
		this.p = p;
		this.animatorScriptRegistry = animatorScriptRegistry;
	}

	internal void Process()
	{
		PoseAnimationFrame frame = animatorScriptRegistry.Service.Model.SelectedFrame;
		if (frame == null)
		{
			return;
		}
		BoneConfiguration goal = p.Frames[frame.Key];
		List<(float, BoneConfiguration)> scores = new List<(float, BoneConfiguration)>();
		foreach (BoneConfiguration bc in p.Frames)
		{
			if (bc != goal)
			{
				float score = Score(goal, bc);
				scores.Add((score, bc));
			}
		}
		scores = scores.OrderBy(((float, BoneConfiguration) x) => x.Item1).ToList();
		Logger logger = Logger.Global;
		foreach (var ab in scores)
		{
			int index = p.Frames.IndexOf(ab.Item2);
			string frames = string.Join(",", (from s in p.States
				where s.Key == index
				select p.States.IndexOf(s).ToString()).ToArray());
			logger.Info("Score {0} key {1} frames {2}", ab.Item1, index, frames);
		}
	}

	private float Score(BoneConfiguration goal, BoneConfiguration bc)
	{
		int count = 0;
		float score = 0f;
		foreach (KeyValuePair<string, Quaternion> kv in goal.Rotations)
		{
			if (!bc.Rotations.TryGetValue(kv.Key, out var q))
			{
				score = float.MaxValue;
				break;
			}
			score += Quaternion.Angle(kv.Value, q);
			count++;
		}
		return score / (float)count;
	}
}
