using UnityEngine;

namespace BetterExperience.Features.AlternativeRating;

internal class ParametricError : ErrorEstimator
{
	private (float x, float y)[] points;

	public ParametricError(params (float, float)[] points)
	{
		this.points = points;
	}

	public float Error(AutoratingProfile.GeneExpectation ge, float value)
	{
		float delta = Mathf.Abs(ge.GetExpectationTarget(value) - value);
		if (delta <= points[0].x)
		{
			return points[0].y;
		}
		for (int i = 1; i < points.Length; i++)
		{
			if (delta <= points[i].x)
			{
				(float, float) a = points[i - 1];
				(float, float) b = points[i];
				float t = Mathf.InverseLerp(a.Item1, b.Item1, delta);
				return Mathf.Lerp(a.Item2, b.Item2, t);
			}
		}
		return points[points.Length - 1].y;
	}
}
