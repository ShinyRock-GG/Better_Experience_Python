using System;

namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class UniformRandom : PoolingStrategy
{
	private GenePool pool;

	public UniformRandom(GenePool pool)
	{
		this.pool = pool;
	}

	public GeneSet[] GetSpecialDumpVectors()
	{
		return Array.Empty<GeneSet>();
	}

	public void Initialize()
	{
	}

	public void Populate()
	{
	}

	public GenerationInfo ProduceRandomGeneSet(GeneSet gs, int step)
	{
		for (int i = 0; i < gs.Vector.Length; i++)
		{
			gs.Vector[i] = pool.Random.NextFloat(1f);
		}
		return new GenerationInfo();
	}
}
