using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.Dialogos;
using Assets._ReusableScripts.CuchiCuchi.Dialogos.Genericos.Globales;
using Assets._ReusableScripts.CuchiCuchi.Dialogos.Objetos;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using HarmonyLib;

namespace BetterExperience.Features.Lexicon;

internal class WordsProcessor : BaseDialogProcessor<DeepDict<string, string, string, ADialogVariant>>
{
	public WordsProcessor(PersistenceService persistence)
		: base("words", persistence)
	{
	}

	protected override void TraverseTree()
	{
		PalabrasDeDialogosGenericos.DialogosDeLocal holder = Singleton<PalabrasDeDialogosGenericos>.instance.US;
		Walk(holder.acciones3PersonaDeTipoDeRespuesta, SpecificHandler("acciones3PersonaDeTipoDeRespuesta"));
		Walk(holder.acciones3PersonaPluralDeTipoDeRespuesta, SpecificHandler("acciones3PersonaPluralDeTipoDeRespuesta"));
		Walk(holder.accionesConjugadasDeTipoDeRespuesta, SpecificHandler("accionesConjugadasDeTipoDeRespuesta"));
		Walk(holder.accionesDeTipoDeRespuesta, SpecificHandler("accionesDeTipoDeRespuesta"));
		Walk(holder.accionesPasadoDeTipoDeRespuesta, SpecificHandler("accionesPasadoDeTipoDeRespuesta"));
		Walk(holder.accionesPluralesDeTipoDeRespuesta, SpecificHandler("accionesPluralesDeTipoDeRespuesta"));
		Walk(holder.accionesPresentesDeTipoDeRespuesta, SpecificHandler("accionesPresentesDeTipoDeRespuesta"));
		Walk(holder.emocionesPersonalesDeTipoDeRespuesta, SpecificHandler("emocionesPersonalesDeTipoDeRespuesta"));
		Walk(holder.emocionesPersonalesPluralesDeTipoDeRespuesta, SpecificHandler("emocionesPersonalesPluralesDeTipoDeRespuesta"));
		Walk(holder.emocionesPresentesDeTipoDeRespuesta, SpecificHandler("emocionesPresentesDeTipoDeRespuesta"));
		Walk(holder.emocionesTerceraPersonaDeTipoDeRespuesta, SpecificHandler("emocionesTerceraPersonaDeTipoDeRespuesta"));
		Walk(holder.emocionesTerceraPersonaPluralesDeTipoDeRespuesta, SpecificHandler("emocionesTerceraPersonaPluralesDeTipoDeRespuesta"));
		Walk(holder.intensidadAdjetivo, SpecificHandler("intensidadAdjetivo"));
		Walk(holder.intensidadAdverbio, SpecificHandler("intensidadAdverbio"));
		Walk(holder.peticionesPersonalesNegativasDeTipoDeRespuesta, SpecificHandler("peticionesPersonalesNegativasDeTipoDeRespuesta"));
		Walk(holder.peticionesPersonalesPositivasDeTipoDeRespuesta, SpecificHandler("peticionesPersonalesPositivasDeTipoDeRespuesta"));
		Walk(holder.peticionesPresentesNegativasDeTipoDeRespuesta, SpecificHandler("peticionesPresentesNegativasDeTipoDeRespuesta"));
		Walk(holder.peticionesPresentesPositivasDeTipoDeRespuesta, SpecificHandler("peticionesPresentesPositivasDeTipoDeRespuesta"));
		Walk(holder.peticionesSerConjugadoDeTipoDeRespuesta, SpecificHandler("peticionesSerConjugadoDeTipoDeRespuesta"));
		Walk(holder.sentimientoPerfectoDeTipoDeRespuesta, SpecificHandler("sentimientoPerfectoDeTipoDeRespuesta"));
		Walk(holder.tipoDeRespuestaDeTipoDePalabras, SpecificHandler("tipoDeRespuestaDeTipoDePalabras"));
	}

	private Action<string, string, ListaDeDialogosBase> SpecificHandler(string v)
	{
		return delegate(string a, string b, ListaDeDialogosBase c)
		{
			a = Translator.Instance.Encode(a);
			b = Translator.Instance.Encode(b);
			Traverse val = Traverse.Create((object)c).Field("text");
			string value = val.GetValue<string>();
			ADialogVariant orAdd = base.Data.GetOrAdd("@" + v, "@" + a, "@" + b, () => new ADialogVariant());
			Visit(orAdd, c);
		};
	}

	private void Walk(DiccionaryEnum<Personalidad.TipoDeRespuestaDeDialogoDeHeroina, ListaDeDialogos> x, Action<string, string, ListaDeDialogosBase> handler)
	{
		foreach (KeyValuePair<int, ListaDeDialogos> kv in x)
		{
			string k = ((Personalidad.TipoDeRespuestaDeDialogoDeHeroina)kv.Key/*cast due to constrained. prefix*/).ToString();
			handler("_", k, kv.Value);
		}
	}

	private void Walk<K>(IDictionary<K, DiccionaryEnum<Personalidad.TipoDeRespuestaDeDialogoDeHeroina, ListaDeDialogos>> input, Action<string, string, ListaDeDialogosBase> mapper)
	{
		foreach (KeyValuePair<K, DiccionaryEnum<Personalidad.TipoDeRespuestaDeDialogoDeHeroina, ListaDeDialogos>> kv in input)
		{
			string k = Encode(kv.Key);
			Walk(kv.Value, delegate(string a, string b, ListaDeDialogosBase c)
			{
				mapper(k, b, c);
			});
		}
	}

	private string Encode<K>(K key)
	{
		if (key is ValueTuple<int, int, int, int> x0)
		{
			return x0.Item1 + "_" + x0.Item2 + "_" + x0.Item3 + "_" + x0.Item4;
		}
		if (key is ReaccionHumana x1)
		{
			return x1.ToString();
		}
		if (key is int x2)
		{
			return x2.ToString();
		}
		throw new ArgumentException("Unsupported type " + key.GetType());
	}
}
