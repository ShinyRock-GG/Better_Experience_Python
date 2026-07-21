namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class SelectionConfiguration : BasePoolingConfiguration
{
	public float Sigma { get; set; } = 1f / 6f;

	public float FragileSigma { get; set; } = 0.0125f;

	public float Step { get; set; }

	public VectorDistribution Distribution { get; set; } = VectorDistribution.normal;

	public float CrossoverThreshold { get; set; } = 0.5f;

	public float PerceptionThreshold { get; set; } = 0.1f;

	public float RateToLifetimeFactor { get; set; } = 2.1f;

	public bool AlwaysUseMorph { get; set; }

	public bool CumulativeGradient { get; set; } = true;

	public int StepsToUpdateWeight { get; set; } = 100;

	public float WeightUpdateScale { get; set; } = 0.1f;

	public VectorInitializer Initializer { get; set; } = VectorInitializer.eve;

	public int FixedSize { get; set; }
}
