using System.Collections.Generic;

namespace BetterExperience.Features.Lexicon;

internal class Translator
{
	private static Translator instance = new Translator();

	private Dictionary<string, string> map = new Dictionary<string, string>();

	public static Translator Instance => instance;

	public Translator()
	{
		map["amable"] = "gentle";
		map["timida"] = "shy";
		map["grosera"] = "rude";
		map["pervertida"] = "perverted";
		map["{prefijo}"] = "{prefix}";
		map["{sufijo}"] = "{suffix}";
		map["{peticionPersonal}"] = "{personalReq}";
		map["{peticionPresente}"] = "{currentReq}";
		map["{accion3Persona}"] = "{action3rdPerson}";
		map["{accion3PersonaPlural}"] = "{action3rdPersonPlural}";
		map["{accionConjugada}"] = "{actionConjugate}";
		map["{accionPlural}"] = "{actionPlural}";
		map["{accionPresente}"] = "{actionPresent}";
		map["{emocionPersonal}"] = "{emotionContinuous}";
		map["{emocionPresente}"] = "{emotionPresent}";
		map["{mi}"] = "{my}";
		map["{miPlural}"] = "{myPlural}";
		map["{eso}"] = "{that}";
		map["{esoEsta}"] = "{thisIs}";
		map["{esoEs}"] = "{thatsIs}";
		map["{estas}"] = "{are}";
		map["{tuyo}"] = "{your}";
		map["{tuyoPlural}"] = "{yourPlural}";
		map["{con}"] = "{with}";
		map["{cosaPropia}"] = "{myPart}";
		map["{cosaOther}"] = "{yourPart}";
		map["{hacerConjugado}"] = "{doConjugate}";
		map["{haciendo}"] = "{doing}";
		map["{sentimientoPerfecto}"] = "{feel}";
		map["{esoCosa}"] = "{thatPart}";
		map["{cuando}"] = "{when}";
		map["{tu}"] = "{you}";
		map["{yo}"] = "{I}";
		map["{ponerPerfecto}"] = "{put}";
		map["{tomarPerfecto}"] = "{take}";
		map["{voltearPerfecto}"] = "{flip}";
		map["{lejos}"] = "{away}";
		map["{desde}"] = "{from}";
		map["{hacerPasado}"] = "{did}";
		map["{hacerPlural}"] = "{do}";
		map["{peticionSerConjugado}"] = "{requestConjugated}";
		map["{muy}"] = "{very}";
		map["{intensidadAdverbio}"] = "{intensityAdverb}";
		map["{intensidadAdjetivo}"] = "{intensityAdjective}";
		map["{muymuy}"] = "{veryvery}";
		map["{exclamacionPlacer}"] = "{exclamationPleasure}";
		map["{enAccionAuto}"] = "{actionAuto}";
		map["{enCosaPropiaAuto}"] = "{partAuto}";
		map["{yoEstoy}"] = "{Iam}";
		map["{esoCosaPlural}"] = "{thatPartPlural}";
		map["{accion}"] = "{action}";
		map["{yoMismo}"] = "{myself}";
		map["{esta}"] = "{is}";
		map["{emocionPersonalPlural}"] = "{emotionPlural}";
		map["{emocionTerceraPersonaPlural}"] = "{emotion3rdPersonPlural}";
		map["{porYoMismo}"] = "{byMySelf}";
		Dictionary<string, string> copy = new Dictionary<string, string>(map);
		foreach (KeyValuePair<string, string> kv in copy)
		{
			if (kv.Key.StartsWith("{"))
			{
				string k = kv.Key.Substring(1);
				string v = kv.Value.Substring(1);
				map.Add("{US=" + k, "{US=" + v);
				map.Add("{ES=" + k, "{ES=" + v);
			}
		}
	}

	public string Encode(string k)
	{
		foreach (KeyValuePair<string, string> kv in map)
		{
			k = k.Replace(kv.Key, kv.Value);
		}
		return k;
	}

	public string Decode(string k)
	{
		foreach (KeyValuePair<string, string> kv in map)
		{
			k = k.Replace(kv.Value, kv.Key);
		}
		return k;
	}
}
