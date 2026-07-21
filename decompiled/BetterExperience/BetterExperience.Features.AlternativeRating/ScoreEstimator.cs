namespace BetterExperience.Features.AlternativeRating;

internal interface ScoreEstimator
{
	float Score(float[] rates);
}
