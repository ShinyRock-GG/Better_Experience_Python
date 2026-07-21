using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dialogos;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;

namespace BetterExperience.Features.Lexicon;

internal class BodyPartsProcessor : BaseDialogProcessor<Dictionary<string, ADialogVariant>>
{
	public BodyPartsProcessor(PersistenceService persistence)
		: base("body_parts", persistence)
	{
	}

	protected override void TraverseTree()
	{
		NombresLocalizadosDePartes manager = Singleton<NombresLocalizadosDePartes>.instance;
		DiccionaryEnum<ParteDelCuerpoHumano, ListaDeNombresDeParteDelCuerpo> map = Traverse.Create((object)manager).Field("m_US_Cuerpo").GetValue<DiccionaryEnum<ParteDelCuerpoHumano, ListaDeNombresDeParteDelCuerpo>>();
		foreach (KeyValuePair<int, ListaDeNombresDeParteDelCuerpo> x in map)
		{
			string k = "@" + (ParteDelCuerpoHumano)x.Key/*cast due to constrained. prefix*/;
			ADialogVariant variant = base.Data.GetValueOrAdd(k, () => new ADialogVariant());
			Visit(variant, x.Value);
		}
	}
}
