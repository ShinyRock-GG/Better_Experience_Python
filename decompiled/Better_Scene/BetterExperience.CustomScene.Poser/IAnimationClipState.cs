namespace BetterExperience.CustomScene.Poser;

public interface IAnimationClipState
{
	PoseAnimationClip Clip { get; }

	bool Cyclic { get; }

	float Time { get; }

	float Length { get; }

	float Weight { get; }

	void FadeOut();
}
