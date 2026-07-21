namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal interface PoolingStrategy
{
	void Initialize();

	void Populate();

	GenerationInfo ProduceRandomGeneSet(GeneSet target, int step);

	GeneSet[] GetSpecialDumpVectors();
}
