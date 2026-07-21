using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using UnityEngine;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class NormalizeMotion
{
	private class FrameInfo
	{
		public PoseAnimationFrame from;

		public PoseAnimationFrame to;

		public float dy;

		public float speed;

		public float time;

		public FrameInfo(PoseAnimationFrame from, PoseAnimationFrame to)
		{
			this.from = from;
			this.to = to;
		}
	}

	private Logger logger = Logger.Create<NormalizeMotion>();

	private PoseAnimationClip clip;

	public NormalizeMotion(PoseAnimationClip clip)
	{
		this.clip = clip;
	}

	public void Process()
	{
		if (clip.Frames.Count == 0)
		{
			logger.Error("Empty clip");
			return;
		}
		PoseAnimationFrame start = clip.States[0];
		PoseAnimationFrame e = start;
		List<FrameInfo> frames = new List<FrameInfo>();
		while (e.Next.Count != 0 && !e.Next.Contains(start))
		{
			if (e.Next.Count > 1)
			{
				throw new ArgumentException("Next frame > 1 is not allowed");
			}
			PoseAnimationFrame a = e.Next[0];
			frames.Add(new FrameInfo(e, a));
			e = a;
		}
		logger.Info("{0} frames found", frames.Count);
		foreach (FrameInfo f in frames)
		{
			BoneConfiguration a2 = clip.Frames[f.from.Key];
			BoneConfiguration b = clip.Frames[f.to.Key];
			f.dy = Mathf.Abs((a2.HipOffset - b.HipOffset).y);
			f.time = f.to.FadeIn;
			if (f.to.FadeIn > 0f)
			{
				f.speed = f.dy / f.time;
			}
			else
			{
				f.speed = 0f;
			}
			logger.Info("delta {0} in {1} => {2}", f.dy, f.time, f.speed);
		}
		float avg = frames.Select((FrameInfo x) => x.speed).Average();
		logger.Info("Avg speed {0}", avg);
		foreach (FrameInfo f2 in frames)
		{
			if (f2.speed > 0f)
			{
				float factor = avg / f2.speed;
				float before = f2.to.FadeIn;
				f2.to.FadeIn *= factor;
				logger.Info("Smooth {0} => {1}", before, f2.to.FadeIn);
			}
		}
	}
}
