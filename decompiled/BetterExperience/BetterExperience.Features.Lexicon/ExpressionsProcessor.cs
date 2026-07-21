using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Dialogos;
using Assets._ReusableScripts.CuchiCuchi.Dialogos.Genericos.Objetos;
using Assets._ReusableScripts.CuchiCuchi.Dialogos.Globales.Abstracts;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;

namespace BetterExperience.Features.Lexicon;

internal class ExpressionsProcessor : BaseDialogProcessor<Dictionary<string, ADialogVariant>>
{
	public ExpressionsProcessor(PersistenceService persistence)
		: base("expressions", persistence)
	{
	}

	protected override void TraverseTree()
	{
		IDialogosDePersonalidades[] providers = UnityUtils.FindAndInitializeSingletonsOfType<IDialogosDePersonalidades>();
		IDialogosDePersonalidades[] array = providers;
		foreach (IDialogosDePersonalidades p in array)
		{
			if (!(p is DialogosDePersonalidadesLogic.IDialogos d))
			{
				continue;
			}
			foreach (KeyValuePair<IHolderDeCollecionDeDialogoInfo, IEnvolturaCondicionalDeHolders> item in d.diccDialogoDePersonalidadAEnvolturaCondicional)
			{
				IEnvolturaCondicionalDeHolders holders = item.Value;
				foreach (IHolderDeCollecionDeDialogoInfo y in holders.grupos)
				{
					List<DialogosLocalizadosGenericos> dialogosLocalizados = Traverse.Create((object)y).Field("m_items").GetValue<List<DialogosLocalizadosGenericos>>();
					foreach (DialogosLocalizadosGenericos dl in dialogosLocalizados)
					{
						ADialogVariant variant = base.Data.GetValueOrAdd("@" + dl.name, () => new ADialogVariant());
						Visit(variant, dl);
					}
					if (IsDirty())
					{
						BaseDialogProcessor<Dictionary<string, ADialogVariant>>.Reset(y);
					}
				}
			}
		}
	}
}
