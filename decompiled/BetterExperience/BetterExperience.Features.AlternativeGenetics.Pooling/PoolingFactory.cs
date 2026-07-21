using System;

namespace BetterExperience.Features.AlternativeGenetics.Pooling;

internal class PoolingFactory
{
	public PoolingStrategy Create(GenePool pool)
	{
		if (pool.Data.Settings.Pooling.RollingMean != null)
		{
			return new RollingMeanPooling(pool, pool.Data.Settings.Pooling.RollingMean);
		}
		if (pool.Data.Settings.Pooling.Repeater != null)
		{
			return new Repeater(pool, pool.Data.Settings.Pooling.Repeater);
		}
		if (pool.Data.Settings.Pooling.Selection != null)
		{
			return new SelectionPooling(pool, pool.Data.Settings.Pooling.Selection);
		}
		throw new Exception("Pool " + pool.Data.Settings.Name + " has no defined pooling strategy");
	}
}
