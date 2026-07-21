using System.Collections.Generic;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GenePoolData
{
	public List<string> GeneOrder { get; set; }

	public Dictionary<GeneGeneration, List<GeneSet>> Generations { get; set; }

	public PoolSettings Settings { get; set; }

	public HashSet<string> Survivors { get; set; } = new HashSet<string>();

	public int Seed { get; internal set; }

	public float DiversitySimilarityThreshold { get; set; }

	public int DiversityPenalty { get; set; }

	public int Iteration { get; set; }

	public int GuaranteedRandoms { get; set; } = 2;

	public bool Enabled { get; set; } = true;

	public bool Active { get; set; } = true;

	public int InitialCapacity { get; set; } = 10;

	public int DiscardThreshold { get; set; } = 4;

	public int ExtendedCapacity { get; set; }

	public bool DilutionPhase { get; set; }

	public GenePoolStatistics Statistics { get; set; } = new GenePoolStatistics();

	public int Epoch { get; set; }

	public float Error { get; set; }

	public bool GuidanceDisabled { get; set; }
}
