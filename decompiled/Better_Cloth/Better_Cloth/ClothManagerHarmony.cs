using Assets._ReusableScripts.CuchiCuchi.Ropa;
using BetterExperience;
using BetterExperience.GameScopes;
using HarmonyLib;
using UnityEngine;

namespace Better_Cloth;

internal static class ClothManagerHarmony
{
	private static BetterExperience.Logger logger = new BetterExperience.Logger
	{
		Prefix = "ClothManagerHarmony "
	};

	public static Observable<PiezaDeRopaBase> OnClothHidden { get; } = new Observable<PiezaDeRopaBase>();

	[HarmonyPatch(typeof(PiezasDeRopaLoader), "OcultarPieza")]
	[HarmonyPostfix]
	public static void After_ConjuntoDeRopaLoader_OcultarPieza(PiezasDeRopaLoader __instance, string piezaId, bool ocultar, ref PiezaDeRopaBase paraCambiarEstado, bool __result)
	{
		if (__result && (Object)(object)paraCambiarEstado != null)
		{
			OnClothHidden.Invoke(paraCambiarEstado);
		}
	}
}
