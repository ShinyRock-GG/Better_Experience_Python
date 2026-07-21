using System;
using Newtonsoft.Json;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GeneFineTuning
{
	[JsonIgnore]
	private (float, float)? _minmax;

	public float? DeviationFactor { get; set; }

	public float? Guidance { get; set; }

	public bool? NonDiverse { get; set; }

	public float? SimilarityWeight { get; set; }

	public string Value { get; set; }

	public float? EvolutionFactor { get; set; }

	public string MomentumGroup { get; set; }

	public float? InitialValue { get; set; }

	[JsonIgnore]
	public (float, float)? MinMax
	{
		get
		{
			if (Value != null)
			{
				if (!_minmax.HasValue)
				{
					_minmax = ParseTuple(Value);
				}
				return _minmax;
			}
			return null;
		}
	}

	private (float, float) ParseTuple(string repr)
	{
		string[] parts = repr.Split(new char[1] { ':' });
		if (parts.Length > 2)
		{
			throw new Exception("Illegal range value " + repr);
		}
		float min;
		float max;
		if (parts.Length == 1)
		{
			min = float.Parse(parts[0]);
			max = min;
		}
		else
		{
			min = float.Parse(parts[0]);
			max = float.Parse(parts[1]);
		}
		return (min, max);
	}
}
