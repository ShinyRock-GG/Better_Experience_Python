namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolingGroupSettings
{
	public string Name { get; set; } = "Unnamed";

	public float Threshold { get; set; }

	public int GenerationAttempts { get; set; } = 5000;

	public int RecentCapacity { get; set; } = 10;

	public string Profile { get; set; } = "default";

	public float PerceptionThreshold { get; set; }

	public bool SortByError { get; set; } = true;
}
