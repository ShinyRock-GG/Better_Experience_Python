using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases.Abstracts;
using Assets._ReusableScripts.Genetica.NPCs;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoratingProfile
{
	public class GeneExpectation
	{
		private bool nonContinuous;

		private bool istrait;

		private ErrorEstimator errorFunction;

		public float MaxError { get; set; } = 2f;

		public List<GeneInfoEx> Values { get; } = new List<GeneInfoEx>();

		public float Mean { get; private set; }

		public float RootMeanSquared { get; private set; }

		public float StdDev { get; private set; }

		public bool IsNonContinuous => nonContinuous;

		public bool IsTrait => istrait;

		public bool AGMode { get; set; }

		internal float ComputeError(float value)
		{
			if (istrait)
			{
				value = ConvertTraitValue(value);
			}
			return errorFunction.Error(this, value);
		}

		internal float NormalizeValue(float value)
		{
			if (istrait && !AGMode)
			{
				value = ConvertTraitValue(value);
			}
			return value;
		}

		internal float DenormalizeValue(float value)
		{
			if (istrait && !AGMode)
			{
				value = DeconvertTraitValue(value);
			}
			return value;
		}

		internal float GetExpectationTarget(float value)
		{
			if (nonContinuous)
			{
				return (from x in Values
					select (Mathf.Abs(x.Value - value), Value: x.Value) into x
					orderby x.Item1
					select x).First().Value;
			}
			return Mean;
		}

		internal void ComputeExpectation()
		{
			Mean = Values.Select((GeneInfoEx x) => x.Value).Average();
			RootMeanSquared = Mathf.Sqrt(Values.Select((GeneInfoEx x) => x.Value * x.Value).Average());
			StdDev = (from x in Values
				select x.Value into x
				select Mathf.Abs(x - Mean) into x
				select x * x).Average();
			StdDev = Mathf.Sqrt(StdDev);
			string gene = Values[0].Id.Item1;
			nonContinuous = gene.Contains("Textureador") || gene.Contains("Encojedor") || gene.Contains("Reemplazador") || gene.Contains("Coloreador");
			istrait = gene.Contains("_TraitHumano_");
			if (istrait)
			{
				List<float> traitScores = new List<float>();
				foreach (GeneInfoEx v in Values)
				{
					HumanTraitScore th = (HumanTraitScore)AlteradorDeEnumValueBasePara100Max.ModToIndex(v.Value, AlteradorDeEnumValueConZero<HumanTraitScore>.count);
					float score = Mathf.InverseLerp(-2f, 2f, th.GetValorPolarizadoDeScore());
					traitScores.Add(score);
				}
				Mean = traitScores.Average();
			}
			if (nonContinuous)
			{
				errorFunction = new ParametricError((0f, 0f), (0.1f, 0.9f), (0.2f, 1f));
			}
			else if (istrait)
			{
				errorFunction = new ParametricError((0f, 0f), (0.1f, 0.9f), (0.2f, 1f));
			}
			else
			{
				errorFunction = new ParametricError((0f, 0f), (0.2f, 0.7f), (0.4f, 0.9f), (1f, MaxError));
			}
		}

		private float ConvertTraitValue(float value)
		{
			HumanTraitScore th = (HumanTraitScore)AlteradorDeEnumValueBasePara100Max.ModToIndex(value, AlteradorDeEnumValueConZero<HumanTraitScore>.count);
			return Mathf.InverseLerp(-2f, 2f, th.GetValorPolarizadoDeScore());
		}

		private float DeconvertTraitValue(float value)
		{
			int score = Mathf.RoundToInt(Mathf.Lerp(-2f, 2f, value));
			return Alterador.GetValorDeIndex(score, AlteradorDeEnumValueConZero<HumanTraitScore>.count);
		}
	}

	private ScoreEstimator baseEstimator = new MSEEstimator();

	private ScoreEstimator traitsEstimator = new MeanEstimator();

	private AutoratingSettings Settings { get; set; } = new AutoratingSettings();

	public Dictionary<string, List<ISujetoIdentificableNpc>> Templates { get; } = new Dictionary<string, List<ISujetoIdentificableNpc>>();

	public bool Initialized { get; set; }

	public Dictionary<string, Dictionary<GeneId, GeneExpectation>> Expectatations { get; } = new Dictionary<string, Dictionary<GeneId, GeneExpectation>>();

	public string Name { get; set; } = "unnamed";

	public void Initialize(GuestPool pool, bool agMode)
	{
		foreach (KeyValuePair<string, List<ISujetoIdentificableNpc>> groupAndTemplate in Templates)
		{
			string group = groupAndTemplate.Key;
			List<ISujetoIdentificableNpc> templates = groupAndTemplate.Value;
			foreach (ISujetoIdentificableNpc npc in templates)
			{
				GuestInstance gi = new GuestInstance(npc, pool);
				foreach (GeneInfoEx gene in gi.ExtractAll().Values)
				{
					if (gene.Id.Item1.Contains("_Grupo_") || !gene.Group.Equals(group))
					{
						continue;
					}
					if (gene.Id.Item1.Contains("_TraitHumano_"))
					{
						int i = Mathf.RoundToInt(gene.Value * 100f);
						if (AlteradorDeEnumValueBasePara100Max<HumanTraitScore>.alteradorIndexToEnumValue.ContainsKey(i))
						{
							HumanTraitScore score = (HumanTraitScore)AlteradorDeEnumValueBasePara100Max<HumanTraitScore>.alteradorIndexToEnumValue[i];
							gene.Value = AlteradorDeEnumValueConZero<HumanTraitScore>.EnumToMod(score, AlteradorDeEnumValueConZero<HumanTraitScore>.count);
						}
					}
					Dictionary<GeneId, GeneExpectation> t = Expectatations.GetValueOrAdd(gene.Group, () => new Dictionary<GeneId, GeneExpectation>());
					t.GetValueOrAdd(gene.Id, () => new GeneExpectation()).Values.Add(gene);
				}
			}
		}
		foreach (KeyValuePair<string, Dictionary<GeneId, GeneExpectation>> kv in Expectatations)
		{
			string groupId = kv.Key;
			Dictionary<GeneId, GeneExpectation> group2 = kv.Value;
			float highError = Extensions.GetValueOrDefault(Settings.SpecificMaxError, groupId, Settings.MaxError);
			foreach (GeneExpectation gene2 in group2.Values)
			{
				gene2.ComputeExpectation();
				gene2.MaxError = highError;
				gene2.AGMode = agMode;
			}
		}
	}

	internal Dictionary<string, float> Score(Dictionary<GeneId, GeneInfoEx> dictionary)
	{
		Dictionary<string, float> result = new Dictionary<string, float>();
		foreach (KeyValuePair<string, Dictionary<GeneId, GeneExpectation>> group in Expectatations)
		{
			result[group.Key] = Score(group.Value, dictionary);
			Logger.Global.Info("{0} => {1}", group.Key, result[group.Key]);
		}
		return result;
	}

	private float Score(Dictionary<GeneId, GeneExpectation> value, Dictionary<GeneId, GeneInfoEx> dictionary)
	{
		List<float> baseRates = new List<float>();
		List<float> nonContinuous = new List<float>();
		foreach (KeyValuePair<GeneId, GeneExpectation> kv in value)
		{
			GeneId gid = kv.Key;
			GeneExpectation expectation = kv.Value;
			if (dictionary.TryGetValue(gid, out var x))
			{
				float score = expectation.ComputeError(x.Value);
				if (expectation.IsTrait || expectation.IsNonContinuous)
				{
					nonContinuous.Add(score);
				}
				else
				{
					baseRates.Add(score);
				}
			}
		}
		if (nonContinuous.Count > 0)
		{
			baseRates.Add(nonContinuous.Average());
		}
		return baseEstimator.Score(baseRates.ToArray());
	}
}
