using System.Collections.Generic;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolingGroupData
{
	public Dictionary<string, GenePoolData> Pools { get; set; } = new Dictionary<string, GenePoolData>();

	public PoolingGroupSettings Settings { get; set; } = new PoolingGroupSettings();

	public bool Enabled { get; internal set; } = true;

	public bool Active { get; internal set; } = true;
}
