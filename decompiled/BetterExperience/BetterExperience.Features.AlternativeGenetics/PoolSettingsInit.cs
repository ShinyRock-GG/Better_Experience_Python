using System.Collections.Generic;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolSettingsInit
{
	public PoolingGroupSettings Settings { get; set; } = new PoolingGroupSettings();

	public Dictionary<string, PoolSettings> Pools { get; set; }
}
