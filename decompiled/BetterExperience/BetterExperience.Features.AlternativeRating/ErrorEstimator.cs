namespace BetterExperience.Features.AlternativeRating;

internal interface ErrorEstimator
{
	float Error(AutoratingProfile.GeneExpectation ge, float value);
}
