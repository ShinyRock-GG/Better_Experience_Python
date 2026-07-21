namespace BetterExperience.CustomScene.Poser;

public interface IBoneAnimator
{
	PoseAnimationClip PrimaryClip { get; }

	bool IsPlaying { get; }
}
