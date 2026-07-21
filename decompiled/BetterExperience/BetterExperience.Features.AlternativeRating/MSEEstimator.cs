using System.Linq;
using UnityEngine;

namespace BetterExperience.Features.AlternativeRating;

internal class MSEEstimator : ScoreEstimator
{
	public float Score(float[] rates)
	{
		return Mathf.Sqrt(rates.Select((float x) => x * x).Average());
	}
}
