namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class RollingMeanConfiguration : BasePoolingConfiguration
{
	public float Threshold { get; set; } = 1f;

	public float Step { get; set; }

	public float GenerationWeight { get; set; } = 0.1f;

	public float EvolutionFactor { get; set; } = 1f;

	public float GradLockThreshold { get; set; }

	public float SigmaScale { get; set; } = 1f;

	public float CollapseThreshold { get; set; } = 0.125f;

	public float CollapsedSigmaScale { get; set; } = 0.25f;

	public int Capacity { get; set; } = 10;

	public float CollapseRetainProb { get; set; } = 0.7f;

	public VectorInitializer Initializer { get; set; }

	public float InitializerStdDev { get; set; } = 0.1f;

	public float MinStdDev { get; set; } = 0.05f;

	public float SigmaStdDevWeight { get; set; } = 0.5f;

	public float SigmaDeltaWeight { get; set; } = 1f;

	public float DeltaToStdDev { get; set; } = 0.3f;

	public float BiasThreshold { get; set; } = 0.45f;

	public float BiasFactor { get; set; }

	public int StepsToUpdateWeight { get; set; } = 100;

	public float WeightUpdateScale { get; set; } = 0.1f;

	public float GradientScale { get; set; } = 1f;
}
