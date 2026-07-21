using System.Linq;

namespace BetterExperience.Features.AlternativeRating;

internal class MeanEstimator : ScoreEstimator
{
	public float Score(float[] rates)
	{
		return rates.Average();
	}
}
