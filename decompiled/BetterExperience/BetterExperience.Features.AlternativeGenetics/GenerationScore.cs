namespace BetterExperience.Features.AlternativeGenetics;

internal class GenerationScore
{
	public float TotalScore { get; set; }

	public float MinimalScore { get; set; }

	public float ExactScore { get; set; }

	internal bool IsAcceptable(float threshold)
	{
		if (!float.IsNaN(TotalScore))
		{
			return TotalScore > threshold;
		}
		return true;
	}
}
