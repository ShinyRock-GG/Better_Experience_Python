using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class AnimatorFrameChangedEvent
{
	public PoseAnimationClip Clip { get; }

	public int Frame { get; }

	public AnimatorFrameChangedEvent(PoseAnimationClip clip, int frame)
	{
		Clip = clip;
		Frame = frame;
	}
}
