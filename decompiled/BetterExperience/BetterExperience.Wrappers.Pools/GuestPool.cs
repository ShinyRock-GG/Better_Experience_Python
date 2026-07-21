using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.AI.Clases;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases.Abstracts;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Mapas.Genetica.Handlers;
using Assets._ReusableScripts.Genetica.NPCs;
using Assets.Base.Genetica.Runtime.NPCs.Abstracts.Interfaces;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Wrappers.Pools;

public class GuestPool
{
	private List<GuestInstance> guests = new List<GuestInstance>();

	public PiscinaDeNpcsManager Instance { get; private set; }

	public GeneFactoryInfo PersonalityGeneInfo { get; private set; }

	public GeneFactoryInfo AppearanceGeneInfo { get; private set; }

	public GeneFactoryInfo GeneFactory { get; private set; }

	public int Count => Instance.count;

	public int NonClassifiedCount => 0; // ObtenerSujetosAgrupadoNoCalificadoCount is [Obsolete] in SMA 23.1

	public List<GuestInstance> Guests => guests;

	public Dictionary<GeneId, GeneInfoEx> Eve
	{
		get
		{
			Dictionary<GeneId, GeneInfoEx> result = new Dictionary<GeneId, GeneInfoEx>();
			ProductorDeSujetosDeAparienciaFisicaFemenina appearance = Instance.aparienciaFisica.productor as ProductorDeSujetosDeAparienciaFisicaFemenina;
			if (appearance != null)
			{
				appearance.@default.PrepareAlteradoresDicc();
				ReadGenesInto(appearance.@default.preparedAlteradoresDicc, result);
			}
			ProductorDeSujetosDePersonalidadFemenina personality = Instance.personalidad.productor as ProductorDeSujetosDePersonalidadFemenina;
			if (personality != null)
			{
				personality.@default.PrepareAlteradoresDicc();
				ReadGenesInto(personality.@default.preparedAlteradoresDicc, result);
				foreach (GeneInfoEx g in result.Values)
				{
					if (!g.Id.Item1.Contains("_Grupo_") && g.Id.Item1.Contains("_TraitHumano_"))
					{
						int i = Mathf.RoundToInt(g.Value * 100f);
						HumanTraitScore score = (HumanTraitScore)AlteradorDeEnumValueBasePara100Max<HumanTraitScore>.alteradorIndexToEnumValue[i];
						g.Value = AlteradorDeEnumValueConZero<HumanTraitScore>.EnumToMod(score, AlteradorDeEnumValueConZero<HumanTraitScore>.count);
						g.Value = 0.5f;
					}
				}
			}
			return result;
		}
	}

	static GuestPool()
	{
		RuntimeHelpers.RunClassConstructor(typeof(AlteradorDeGrupoQueCompartenValores).TypeHandle);
		RuntimeHelpers.RunClassConstructor(typeof(AlteradorDeTraitDePersonalidad).TypeHandle);
	}

	public GuestPool(PiscinaDeNpcsManager instance)
	{
		Instance = instance;
		InitializeProxies();
	}

	private void InitializeProxies()
	{
		Dictionary<ISujetoIdentificableNpc, GuestInstance> initial = new Dictionary<ISujetoIdentificableNpc, GuestInstance>();
		foreach (GuestInstance gi in guests)
		{
			initial.Add(gi.Instance, gi);
		}
		guests.Clear();
		foreach (Object ptr in Instance.NPCs)
		{
			ISujetoIdentificableNpc npc = (ISujetoIdentificableNpc)ptr;
			if (!initial.TryGetValue(npc, out var gi2))
			{
				gi2 = new GuestInstance(npc, this);
			}
			guests.Add(gi2);
		}
		IProductorDeConjuntos genegroupGen = Instance.GetComponent<IProductorDeConjuntos>();
		PersonalityGeneInfo = CreateGeneFactoryInfo(genegroupGen.conjuntosParaPersonalidad);
		AppearanceGeneInfo = CreateGeneFactoryInfo(genegroupGen.conjuntosParaApariencia);
		InitializeSensitivityMap();
		GeneFactory = new GeneFactoryInfo();
		PersonalityGeneInfo.Groups.ForEach(GeneFactory.Groups.Add);
		AppearanceGeneInfo.Groups.ForEach(GeneFactory.Groups.Add);
		PersonalityGeneInfo.GeneToGroup.ForEach(GeneFactory.GeneToGroup.Add);
		AppearanceGeneInfo.GeneToGroup.ForEach(GeneFactory.GeneToGroup.Add);
		PersonalityGeneInfo.GroupToGenes.ForEach(GeneFactory.GroupToGenes.Add);
		AppearanceGeneInfo.GroupToGenes.ForEach(GeneFactory.GroupToGenes.Add);
		PersonalityGeneInfo.SensitivityMap.ForEach(GeneFactory.SensitivityMap.Add);
		AppearanceGeneInfo.SensitivityMap.ForEach(GeneFactory.SensitivityMap.Add);
		GeneFactory.Eve = Eve;
	}

	private void InitializeSensitivityMap()
	{
		IReadOnlyList<ModificadoresDeAlterador> aparencia = ((ProductorDeSujetosDeAparienciaFisicaFemenina)Instance.aparienciaFisica.productor).@default.ObtenerAlteradorModificadores();
		SaveSensitivity(AppearanceGeneInfo, aparencia);
		IReadOnlyList<ModificadoresDeAlterador> personalidad = ((ProductorDeSujetosDePersonalidadFemenina)Instance.personalidad.productor).@default.ObtenerAlteradorModificadores();
		SaveSensitivity(PersonalityGeneInfo, personalidad);
	}

	private void SaveSensitivity(GeneFactoryInfo factory, IReadOnlyList<ModificadoresDeAlterador> list)
	{
		foreach (ModificadoresDeAlterador modifier in list)
		{
			GeneSensitivity value = GeneSensitivity.Normal;
			if (modifier.sensible)
			{
				value = GeneSensitivity.Soft;
			}
			else if (modifier.volatil)
			{
				value = GeneSensitivity.Hard;
			}
			for (int i = 0; i < modifier.modificadores.Length; i++)
			{
				factory.SensitivityMap[new GeneId(modifier.alteradorName, i)] = value;
			}
		}
	}

	private GeneFactoryInfo CreateGeneFactoryInfo(IReadOnlyDictionary<string, IReadOnlyCollection<object>> groups)
	{
		GeneFactoryInfo factory = new GeneFactoryInfo();
		foreach (string group in groups.Keys)
		{
			factory.Groups.Add(group);
			IReadOnlyCollection<object> genes = groups[group];
			List<string> geneSet = new List<string>();
			factory.GroupToGenes[group] = geneSet;
			foreach (object geneObj in genes)
			{
				string geneId = (string)geneObj;
				factory.GeneToGroup[geneId] = group;
				geneSet.Add(geneId);
			}
		}
		return factory;
	}

	internal void Invalidate()
	{
		InitializeProxies();
	}

	private void ReadGenesInto(IReadOnlyDictionary<string, ModificadoresDeAlterador> preparedAlteradoresDicc, Dictionary<GeneId, GeneInfoEx> result)
	{
		foreach (KeyValuePair<string, ModificadoresDeAlterador> kv in preparedAlteradoresDicc)
		{
			string geneBase = kv.Key;
			ModificadoresDeAlterador components = kv.Value;
			for (int i = 0; i < components.modificadores.Length; i++)
			{
				GeneId geneid = new GeneId(geneBase, i);
				result[geneid] = new GeneInfoEx
				{
					Id = geneid,
					Value = components.modificadores[i],
					Group = ((IDictionary<string, string>)GeneFactory.GeneToGroup).GetValueOrDefault(geneBase, (string)null)
				};
			}
		}
	}
}
