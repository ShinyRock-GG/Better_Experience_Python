using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class KeyframeCleanup
{
	private PoseAnimationClip p;

	private AnimatorScriptRegistry animatorScriptRegistry;

	public KeyframeCleanup(PoseAnimationClip p, AnimatorScriptRegistry animatorScriptRegistry)
	{
		this.p = p;
		this.animatorScriptRegistry = animatorScriptRegistry;
	}

	internal void Process()
	{
		List<BoneConfiguration> originalOrder = new List<BoneConfiguration>(p.Frames);
		HashSet<BoneConfiguration> usedKeys = new HashSet<BoneConfiguration>();
		foreach (PoseAnimationFrame f in p.States)
		{
			if (f.Key >= 0 && f.Key < originalOrder.Count)
			{
				usedKeys.Add(originalOrder[f.Key]);
			}
		}
		for (int i = p.Frames.Count - 1; i > -1; i--)
		{
			if (!usedKeys.Contains(p.Frames[i]))
			{
				p.Frames.RemoveAt(i);
			}
		}
		foreach (PoseAnimationFrame f2 in p.States)
		{
			if (f2.Key >= 0 && f2.Key < originalOrder.Count)
			{
				BoneConfiguration o = originalOrder[f2.Key];
				f2.Key = p.Frames.IndexOf(o);
			}
		}
	}
}
