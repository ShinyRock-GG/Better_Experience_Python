using System;

namespace BetterExperience.Wrappers.Pools;

internal interface IGeneticsEditor : IDisposable
{
	void Randomize(int cycles = 1);

	void Reset();
}
