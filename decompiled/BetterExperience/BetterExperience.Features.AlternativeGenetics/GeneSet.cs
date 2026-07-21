using System.Collections.Generic;
using BetterExperience.Utils;
using Newtonsoft.Json;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GeneSet
{
	public string Id { get; set; }

	public Dictionary<GeneVector, float[]> Vectors { get; set; }

	[JsonIgnore]
	public float[] Vector => GetVector(GeneVector.Data);

	[JsonIgnore]
	public float[] Rating => GetVector(GeneVector.Rating);

	[JsonIgnore]
	public float[] StdDev => GetVector(GeneVector.StdDev);

	public List<string> Ancestors { get; set; } = new List<string>();

	public int Generation { get; set; }

	public int Iteration { get; set; }

	public int Epoch { get; set; }

	public int GenAttempts { get; set; }

	public EvolutionMomentum Momentum { get; set; }

	public string MappedGuestId { get; set; }

	public bool IsIncomplete()
	{
		return ArrayUtil.SelectIndex(Vector, float.IsNaN).Length != 0;
	}

	private float[] GetVector(GeneVector vector)
	{
		if (Vectors == null)
		{
			Vectors = new Dictionary<GeneVector, float[]>();
		}
		return ((IDictionary<GeneVector, float[]>)Vectors).GetValueOrDefault(vector, (float[])null);
	}

	internal void InitVector(GeneVector vectorType, float value)
	{
		float[] vec = GetVector(vectorType);
		if (vec == null)
		{
			float[] array = (Vectors[vectorType] = new float[Vector.Length]);
			vec = array;
		}
		vec.Fill(value);
	}
}
