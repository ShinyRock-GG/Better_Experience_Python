using System.Collections.Generic;

namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class Repeater : PoolingStrategy
{
	private GenePool pool;

	private RepeaterPoolConfiguration settings;

	public Repeater(GenePool pool, RepeaterPoolConfiguration settings)
	{
		this.pool = pool;
		this.settings = settings;
	}

	public GeneSet[] GetSpecialDumpVectors()
	{
		return new GeneSet[0];
	}

	public void Initialize()
	{
	}

	public void Populate()
	{
		List<GeneSet> og = pool.GetGeneration(GeneGeneration.Old);
		List<GeneSet> yg = pool.GetGeneration(GeneGeneration.Young);
		foreach (GeneSet gs in yg)
		{
			if (pool.Data.Survivors.Contains(gs.Id) && gs.Rating != null && gs.Rating[0] > 0.4f)
			{
				og.Add(gs);
			}
		}
		yg.Clear();
		while (og.Count > pool.Capacity)
		{
			og.RemoveAt(0);
		}
		foreach (GeneSet g in og)
		{
			yg.Add(pool.CopyGeneSet(g));
		}
		pool.Data.DiversityPenalty = pool.Data.InitialCapacity - yg.Count;
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
