using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets.TValle.BeachGirl.Runtime;
using BetterExperience.Wrappers.Characters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BetterExperience.CustomScene.Poser;

/// <summary>
/// Deserializes Dictionary&lt;int, float&gt; whose JSON keys may be legacy
/// Spanish TipoDeExpresion enum names (sorpresa, alegria, placer, …) or
/// plain integer strings (new format).  Unknown keys are silently skipped.
/// </summary>
internal class FaceExpressionDictConverter : JsonConverter<Dictionary<int, float>>
{
    // Maps old TipoDeExpresion Spanish names → FaceExpressionType int values.
    private static readonly Dictionary<string, int> _nameMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "alegria",      0 }, // happiness
            { "placer",       1 }, // pleasure
            { "dolor",        2 }, // pain
            { "ira",          3 }, // anger
            { "enojo",        3 }, // anger (alternate)
            { "sorpresa",     4 }, // surprise
            { "aburrimiento", 5 }, // boredom
        };

    public override Dictionary<int, float> ReadJson(
        JsonReader reader, Type objectType,
        Dictionary<int, float> existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        var dict = existingValue ?? new Dictionary<int, float>();
        if (reader.TokenType == JsonToken.Null)
            return dict;

        var obj = JObject.Load(reader);
        foreach (var prop in obj.Properties())
        {
            int key;
            if (int.TryParse(prop.Name, out key))
                dict[key] = prop.Value.ToObject<float>();
            else if (_nameMap.TryGetValue(prop.Name, out key))
                dict[key] = prop.Value.ToObject<float>();
            // unknown key → skip silently
        }
        return dict;
    }

    public override void WriteJson(
        JsonWriter writer, Dictionary<int, float> value,
        JsonSerializer serializer)
    {
        writer.WriteStartObject();
        if (value != null)
        {
            foreach (var kv in value)
            {
                writer.WritePropertyName(kv.Key.ToString());
                writer.WriteValue(kv.Value);
            }
        }
        writer.WriteEndObject();
    }
}

public class GesturesData
{
	public static readonly GesturesData NONE = new GesturesData();

	public bool EyesEnabled { get; set; }

	public Dictionary<OjosExpresionController.Tipo, float> Eyes { get; set; } = new Dictionary<OjosExpresionController.Tipo, float>();

	public bool MouthEnabled { get; set; }

	public Dictionary<TiposDeGestosDeBoca, float> Mouth { get; set; } = new Dictionary<TiposDeGestosDeBoca, float>();

	public bool FaceEnabled { get; set; }

	[JsonConverter(typeof(FaceExpressionDictConverter))]
	public Dictionary<int, float> Face { get; set; } = new Dictionary<int, float>(); // TipoDeExpresion namespace unresolvable in SMA 23.1 — keyed by int

	public bool FaceBlendShapesEnabled { get; set; }

	public Dictionary<string, float> FaceBlendShapes { get; set; } = new Dictionary<string, float>();

	public GesturesData()
	{
	}

	public GesturesData(GesturesWeights weights)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected I4, but got Unknown
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected I4, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		EyesEnabled = weights.EyesOverride;
		EyeExpressionType[] keys = weights.EyeExpressions.Keys;
		foreach (EyeExpressionType k in keys)
		{
			float w = weights.EyeExpressions[k];
			if (w > 0f)
			{
				Eyes[(OjosExpresionController.Tipo)k] = w;
			}
		}
		MouthEnabled = weights.MouthOverride;
		MouthGesture[] keys2 = weights.MouthExpression.Keys;
		foreach (MouthGesture k2 in keys2)
		{
			float w2 = weights.MouthExpression[k2];
			if (w2 > 0f)
			{
				Mouth[(TiposDeGestosDeBoca)k2] = w2;
			}
		}
		FaceEnabled = weights.FaceOverride;
		FaceExpressionType[] keys3 = weights.FaceExpression.Keys;
		foreach (FaceExpressionType k3 in keys3)
		{
			float w3 = weights.FaceExpression[k3];
			if (w3 > 0f)
			{
				Face[(int)k3] = w3;
			}
		}
		FaceBlendShapesEnabled = weights.FaceBlendShapesOverride;
		string[] keys4 = weights.FaceBlendShapes.Keys;
		foreach (string k4 in keys4)
		{
			float w4 = weights.FaceBlendShapes[k4];
			if (w4 > 0f)
			{
				FaceBlendShapes[k4] = w4;
			}
		}
	}
}
