namespace BetterExperience.Features.BetterHand;

internal interface IGripController
{
	GripPose CurrentPose { get; }

	bool TransitionComplete { get; }

	bool PoserEnabled { get; set; }

	void SetPose(GripPose pose, bool forcePose);

	void ResetIdlePose();

	void SetContactMatrix(bool[][] contactMatrix);
}
