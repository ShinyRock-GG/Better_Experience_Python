using System.Collections.Generic;
using BetterExperience.Features.AlternativeGenetics.Pooling;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolSettings
{
	public string Name { get; set; }

	public bool EnforceSymmetry { get; set; }

	public List<string> Groups { get; set; }

	public List<float> ForcedValue { get; set; }

	public Dictionary<string, GeneFineTuning> FineTuning { get; set; } = new Dictionary<string, GeneFineTuning>();

	public bool Hidden { get; set; }

	public bool FastPass { get; set; }

	public bool UseMomentum { get; set; }

	public string Comparator { get; set; }

	public PoolingSettings Pooling { get; set; } = new PoolingSettings();

	public bool StandardizeGrupoGenes { get; set; }
}
