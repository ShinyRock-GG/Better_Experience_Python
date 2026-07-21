using System.Collections.Generic;
using System.Linq;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoPulling
{
	private Logger logger = Logger.Create<AutoPulling>();

	private Dictionary<string, List<Dictionary<GeneId, float>>> earlyValues = new Dictionary<string, List<Dictionary<GeneId, float>>>();

	private AutoratingProfile profile;

	public int MaxAttempts { get; private set; } = 10;

	public float DeterminismFactor { get; set; } = 0.3f;

	private void CollectDiversityStatistics(GuestInstance guest, AutoratingProfile profile)
	{
		earlyValues.Clear();
		List<Dictionary<GeneId, GeneInfoEx>> previousMorphs = (from x in guest.Pool.Guests
			where x != guest && x.Classified
			select x.ExtractAll()).ToList();
		foreach (KeyValuePair<string, Dictionary<GeneId, AutoratingProfile.GeneExpectation>> ekv in profile.Expectatations)
		{
			string group = ekv.Key;
			previousMorphs.ForEach(delegate(Dictionary<GeneId, GeneInfoEx> genes)
			{
				Dictionary<GeneId, float> dictionary = new Dictionary<GeneId, float>();
				foreach (GeneId current in ekv.Value.Keys)
				{
					if (genes.TryGetValue(current, out var value))
					{
						dictionary[current] = value.Value;
					}
				}
				earlyValues.GetValueOrAdd(group, () => new List<Dictionary<GeneId, float>>()).Add(dictionary);
			});
		}
	}

	public bool ApplyProfile(GuestInstance guest, AutoratingProfile profile, float aStrength, float pStrength)
	{
		this.profile = profile;
		Dictionary<GeneId, GeneInfoEx> update = ApplyProfileImpl(guest, profile, aStrength, pStrength, absolutePull: false);
		if (update == null)
		{
			return false;
		}
		return true;
	}

	private Dictionary<GeneId, GeneInfoEx> ApplyProfileImpl(GuestInstance guest, AutoratingProfile profile, float aStrength, float pStrength, bool absolutePull)
	{
		aStrength = Mathf.Max(0f, aStrength);
		pStrength = Mathf.Max(0f, pStrength);
		if (aStrength == 0f && pStrength == 0f)
		{
			return null;
		}
		Dictionary<GeneId, GeneInfoEx> appearance = guest.ExtractAppearance();
		Dictionary<GeneId, GeneInfoEx> personality = guest.ExtractPersonality();
		List<GeneInfo> updates = new List<GeneInfo>();
		Dictionary<GeneId, GeneInfoEx> all = new Dictionary<GeneId, GeneInfoEx>();
		all.AddRange(appearance);
		all.AddRange(personality);
		Dictionary<string, float> score = profile.Score(all);
		foreach (KeyValuePair<string, Dictionary<GeneId, AutoratingProfile.GeneExpectation>> ekv in profile.Expectatations)
		{
			string groupid = ekv.Key;
			score.TryGetValue(groupid, out var _);
			foreach (KeyValuePair<GeneId, AutoratingProfile.GeneExpectation> kv in ekv.Value)
			{
				GeneId gene = kv.Key;
				AutoratingProfile.GeneExpectation exp = kv.Value;
				if (exp.IsTrait)
				{
					continue;
				}
				float strength = 0f;
				if (appearance.TryGetValue(gene, out var value))
				{
					strength = aStrength;
				}
				else if (personality.TryGetValue(gene, out value))
				{
					strength = pStrength;
				}
				if (strength != 0f)
				{
					float target = exp.GetExpectationTarget(value.Value);
					float current = exp.NormalizeValue(value.Value);
					float error = Mathf.Max(0f, Mathf.Abs(target - current) - (absolutePull ? 0f : exp.StdDev));
					if (error > 0f)
					{
						float baseError = exp.ComputeError(value.Value);
						float localStrength = baseError * baseError * strength;
						localStrength *= Random.Range(Mathf.Clamp01(DeterminismFactor), 1f);
						value.Value = (current + target * localStrength) / (1f + localStrength);
						value.Value = exp.DenormalizeValue(value.Value);
						updates.Add(value);
						logger.Info("Pull {0} {1}=>{2}=>{3} {4}", gene.ToString(), current, value.Value, target, localStrength);
					}
				}
			}
		}
		if (updates.Count > 0)
		{
			guest.UpdateAll(updates);
			return all;
		}
		return null;
	}
}
