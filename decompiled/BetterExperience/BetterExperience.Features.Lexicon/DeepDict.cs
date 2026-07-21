using System;
using System.Collections.Generic;
using BetterExperience.Utils;

namespace BetterExperience.Features.Lexicon;

internal class DeepDict<T1, T2, T3, T4> : Dictionary<T1, Dictionary<T2, Dictionary<T3, T4>>>
{
	public T4 GetOrAdd(T1 k1, T2 k2, T3 k3, Func<T4> opt)
	{
		return this.GetValueOrAdd(k1, () => new Dictionary<T2, Dictionary<T3, T4>>()).GetValueOrAdd(k2, () => new Dictionary<T3, T4>()).GetValueOrAdd(k3, opt);
	}
}
