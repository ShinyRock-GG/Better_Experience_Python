using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class HyperPinMuscles
{
	private PoseAnimationClip p;

	public HyperPinMuscles(PoseAnimationClip p)
	{
		this.p = p;
	}

	internal void Process()
	{
		foreach (BoneConfiguration f in p.Frames)
		{
			if (f.Muscles == null)
			{
				continue;
			}
			foreach (MuscleConfig v in f.Muscles.Values)
			{
				v.Damper = 0f;
				v.Pin = 1f;
				v.Spring = 5f;
				v.IsSupport = true;
			}
		}
	}
}
