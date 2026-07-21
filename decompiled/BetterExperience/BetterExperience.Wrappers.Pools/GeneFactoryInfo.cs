using System.Collections.Generic;

namespace BetterExperience.Wrappers.Pools;

public class GeneFactoryInfo
{
	public List<string> Groups { get; } = new List<string>();

	public Dictionary<string, string> GeneToGroup { get; } = new Dictionary<string, string>();

	public Dictionary<string, List<string>> GroupToGenes { get; } = new Dictionary<string, List<string>>();

	public Dictionary<GeneId, GeneSensitivity> SensitivityMap { get; } = new Dictionary<GeneId, GeneSensitivity>();

	public Dictionary<GeneId, GeneInfoEx> Eve { get; internal set; }
}
