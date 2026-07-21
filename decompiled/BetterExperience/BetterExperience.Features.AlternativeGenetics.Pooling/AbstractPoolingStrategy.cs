namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal abstract class AbstractPoolingStrategy<TSettings> : PoolingStrategy where TSettings : BasePoolingConfiguration
{
	public TSettings Settings { get; set; }

	public GenePool Pool { get; set; }

	public GenePoolData Data => Pool.Data;

	public virtual float MinimalAcceptedRating => 0.1f;

	public AbstractPoolingStrategy(GenePool pool, TSettings settings)
	{
		Pool = pool;
		Settings = settings;
	}

	public abstract GeneSet[] GetSpecialDumpVectors();

	public abstract void Initialize();

	public abstract void Populate();

	public abstract GenerationInfo ProduceRandomGeneSet(GeneSet target, int step);
}
