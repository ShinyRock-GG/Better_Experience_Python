namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class PoolingSettings
{
	public RollingMeanConfiguration RollingMean { get; set; }

	public RepeaterPoolConfiguration Repeater { get; set; }

	public SelectionConfiguration Selection { get; set; }
}
