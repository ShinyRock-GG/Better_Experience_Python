namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolStateSnapshot
{
	public int OldCount { get; set; }

	public int MatureCount { get; set; }

	public int YoungCount { get; set; }

	public int Level { get; set; }

	public float Threshold { get; set; }
}
