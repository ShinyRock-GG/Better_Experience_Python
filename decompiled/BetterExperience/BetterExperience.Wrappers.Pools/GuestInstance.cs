using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica;
using Assets._ReusableScripts.Genetica;
using Assets._ReusableScripts.Genetica.NPCs;

namespace BetterExperience.Wrappers.Pools;

public class GuestInstance
{
	public ISujetoIdentificableNpc Instance { get; private set; }

	public GuestPool Pool { get; private set; }

	public string Id => Instance.NpcID.ToString();

	public bool Classified => false; // ISujetoCalificable.calificado is [Obsolete] in SMA 23.1

	public int Level => ((ISujetoNivel)Instance).nivel;

	public Dictionary<string, float> Rating
	{
		get
		{
			Dictionary<string, float> rating = new Dictionary<string, float>();
			foreach (KeyValuePair<string, IConjuntoDeGenes> x in Instance.aparienciaFisica.conjuntoPorNombre)
			{
				rating[x.Key] = x.Value.fitnes;
			}
			foreach (KeyValuePair<string, IConjuntoDeGenes> x2 in Instance.personalidad.conjuntoPorNombre)
			{
				rating[x2.Key] = x2.Value.fitnes;
			}
			return rating;
		}
	}

	public GuestInstance(ISujetoIdentificableNpc instance, GuestPool pool)
	{
		Instance = instance;
		Pool = pool;
	}

	public Dictionary<GeneId, GeneInfoEx> ExtractAppearance()
	{
		return ExtractGenes(Instance.aparienciaFisica, Pool.AppearanceGeneInfo);
	}

	public Dictionary<GeneId, GeneInfoEx> ExtractAll()
	{
		Dictionary<GeneId, GeneInfoEx> all = new Dictionary<GeneId, GeneInfoEx>();
		foreach (KeyValuePair<GeneId, GeneInfoEx> kv in ExtractAppearance())
		{
			all.Add(kv.Key, kv.Value);
		}
		foreach (KeyValuePair<GeneId, GeneInfoEx> kv2 in ExtractPersonality())
		{
			all.Add(kv2.Key, kv2.Value);
		}
		return all;
	}

	public Dictionary<GeneId, GeneInfoEx> ExtractPersonality()
	{
		return ExtractGenes(Instance.personalidad, Pool.PersonalityGeneInfo);
	}

	private Dictionary<GeneId, GeneInfoEx> ExtractGenes(ISujetoIdentificable geneset, GeneFactoryInfo factory)
	{
		Dictionary<GeneId, GeneInfoEx> result = new Dictionary<GeneId, GeneInfoEx>();
		Dictionary<object, GeneItem> blob = new Dictionary<object, GeneItem>();
		geneset.Decodificar(blob);
		foreach (KeyValuePair<object, GeneItem> kv in blob)
		{
			Tuple<string, int> code = (Tuple<string, int>)kv.Key;
			object geneA = kv.Key;
			GeneId geneId = new GeneId(code);
			GeneItem value = kv.Value;
			if (!factory.GeneToGroup.ContainsKey(geneId.Item1))
			{
				new Logger().Info("No group found for gene {0}", geneId.Item1);
				continue;
			}
			GeneInfoEx info = new GeneInfoEx
			{
				Id = geneId,
				Value = value.valor,
				Group = factory.GeneToGroup[geneId.Item1]
			};
			if (geneset.conjuntoPorNombre.TryGetValue(info.Group, out var rating))
			{
				info.Rating = rating.fitnes;
			}
			else
			{
				new Logger().Info("No rating for {0}", info.Group);
			}
			result[geneId] = info;
		}
		return result;
	}

	internal void Randmize(int appearance = 1, int personality = 1)
	{
		using (IGeneticsEditor editor = OpenModifiersForUpdate(Instance.aparienciaFisica))
		{
			editor.Randomize(appearance);
		}
		using IGeneticsEditor editor2 = OpenModifiersForUpdate(Instance.personalidad);
		editor2.Randomize(personality);
	}

	internal void Reset()
	{
		using (IGeneticsEditor editor = OpenModifiersForUpdate(Instance.aparienciaFisica))
		{
			editor.Reset();
		}
		using IGeneticsEditor editor2 = OpenModifiersForUpdate(Instance.personalidad);
		editor2.Reset();
	}

	private IGeneticsEditor OpenModifiersForUpdate(ISujetoIdentificable obj)
	{
		if (obj is SujetoIdentificableAlteradoresAparienciaFemeninos)
		{
			SujetoIdentificableAlteradoresAparienciaFemeninos x = (SujetoIdentificableAlteradoresAparienciaFemeninos)obj;
			return new AppearanceEditor(x, Pool);
		}
		if (obj is SujetoAlteradoresPersonalidadFemeninos)
		{
			SujetoIdentificableAlteradoresPersonalidadFemeninos x2 = (SujetoIdentificableAlteradoresPersonalidadFemeninos)obj;
			return new PersonalityEditor(x2, Pool);
		}
		throw new Exception("Unexected type " + obj.GetType());
	}

	internal void UpdateAppearance(List<GeneInfo> update)
	{
		WriteGenetics(Instance.aparienciaFisica, update);
	}

	public void UpdateAll(IList<GeneInfo> update)
	{
		List<GeneInfo> aUpdate = new List<GeneInfo>();
		List<GeneInfo> pUpdate = new List<GeneInfo>();
		foreach (GeneInfo gi in update)
		{
			if (Pool.AppearanceGeneInfo.GeneToGroup.ContainsKey(gi.Id.Item1))
			{
				aUpdate.Add(gi);
				continue;
			}
			if (Pool.PersonalityGeneInfo.GeneToGroup.ContainsKey(gi.Id.Item1))
			{
				pUpdate.Add(gi);
				continue;
			}
			throw new Exception("Gene " + gi.Id.Item1 + " has no group mapping");
		}
		WriteGenetics(Instance.aparienciaFisica, aUpdate);
		WriteGenetics(Instance.personalidad, pUpdate);
	}

	private void WriteGenetics(ISujetoIdentificable geneset, List<GeneInfo> update)
	{
		Dictionary<object, GeneItem> target = new Dictionary<object, GeneItem>();
		foreach (GeneInfo gi in update)
		{
			target.Add(gi.Id, new GeneItem
			{
				valor = gi.Value
			});
		}
		geneset.Codificar(target);
	}
}
