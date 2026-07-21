using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class AnimateOp : BasicOperation
{
	private List<PoseAnimationClip> clips;

	private List<string> animations;

	private string activeClipName;

	private List<AnimatorLayer> layers = new List<AnimatorLayer>();

	private AnimationCompletionMode completionMode;

	public float BlendingTime { get; set; } = -1f;

	public string Label { get; internal set; }

	public AnimateOp(string animation, AnimatorLayer layer = AnimatorLayer.Primary)
	{
		animations = new List<string>();
		animations.Add(animation);
		layers.Add(layer);
	}

	public AnimateOp(IReadOnlyList<string> animations)
	{
		this.animations = new List<string>(animations);
	}

	public AnimateOp(IReadOnlyList<PoseAnimationClip> clips)
	{
		this.clips = new List<PoseAnimationClip>(clips);
	}

	public AnimateOp(IReadOnlyList<PoseAnimationClip> clips, float blendingTime, List<AnimatorLayer> layers, AnimationCompletionMode completionMode = AnimationCompletionMode.Default)
	{
		this.clips = new List<PoseAnimationClip>(clips);
		BlendingTime = blendingTime;
		if (layers != null)
		{
			this.layers.AddRange(layers);
		}
		this.completionMode = completionMode;
	}

	public AnimateOp(PoseAnimationClip clip, AnimatorLayer layer = AnimatorLayer.Primary)
	{
		clips = new List<PoseAnimationClip>();
		clips.Add(clip);
		layers.Add(layer);
	}

	public override void Run(InteractionContext context)
	{
		if (layers.Count == 0)
		{
			layers.Add(AnimatorLayer.Primary);
		}
		if (clips == null)
		{
			clips = new List<PoseAnimationClip>();
			foreach (string animationName in animations)
			{
				PoseAnimationClip aClip = context.AnimationController.ResolveClip(animationName);
				if (aClip != null)
				{
					clips.Add(aClip);
				}
			}
		}
		if (clips.Count > 0)
		{
			PoseAnimationClip clip = clips[Random.Range(0, clips.Count)];
			if (clip.IsNativeIdle)
			{
				context.AnimationController.InterruptPose("NativeIdle");
			}
			else if (context.AnimationController.StartAnimation(clip, BlendingTime, layers, completionMode, Label))
			{
				activeClipName = clip.UniqueName;
			}
		}
	}

	public override bool IsComplete(InteractionContext context)
	{
		PoseAnimationController ctl = context.AnimationController;
		if (layers.Contains(AnimatorLayer.Primary) || layers.Count == 0)
		{
			IAnimationClipState state = ctl.GetPlayingClipByLayer(AnimatorLayer.Primary);
			if (state == null)
			{
				return true;
			}
			if (state.Clip.UniqueName != activeClipName)
			{
				return true;
			}
			if (state.Cyclic)
			{
				return true;
			}
			return false;
		}
		if (layers[0] == AnimatorLayer.Additive)
		{
			return true;
		}
		IAnimationClipState clip = ctl.GetPlayingClipByLayer(layers[0]);
		if (clip == null)
		{
			return true;
		}
		if (clip.Cyclic)
		{
			return true;
		}
		return false;
	}

	public List<PoseAnimationClip> TryResolveClipsForContext(InteractionManager.InteractionQueryContext ctx)
	{
		if (clips != null)
		{
			return clips;
		}
		List<PoseAnimationClip> result = new List<PoseAnimationClip>();
		foreach (string name in animations)
		{
			result.AddRange(ctx.CurrentPosture.Poses.FindClips(name));
		}
		return result;
	}

	public override string ToString()
	{
		string buf = "";
		if (animations != null)
		{
			buf = buf + " anims " + string.Join(",", animations.ToArray());
		}
		if (clips != null)
		{
			buf = buf + " clips " + string.Join(",", clips.Select((PoseAnimationClip x) => x.UniqueName).ToArray());
		}
		buf = buf + " blendingtime " + BlendingTime;
		buf = buf + " layers " + string.Join(",", layers.Select((AnimatorLayer x) => x.ToString()).ToArray());
		return base.ToString() + buf;
	}
}
