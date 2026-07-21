namespace BetterExperience.CustomScene.Poser;

public class PoseAnimationState
{
	private IBoneAnimator animator;

	public PoseAnimationClip PrimaryClip => animator.PrimaryClip;

	public bool IsPlaying => animator.IsPlaying;

	public PoseAnimationState(IBoneAnimator animator)
	{
		this.animator = animator;
	}
}
