using System;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.GoTo.UI;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk.Interacciones;
using Assets.Base.RootMotion.BeachGirl.Runtime.FinalIk.HighHeelScripts;
using BetterExperience.GameScopes;
using HarmonyLib;

namespace BetterExperience.CustomScene;

public static class BetterSceneHarmonyPatches
{
	public static Observable<HashSetList<string>> OnAfterGoToListPopulated { get; } = new Observable<HashSetList<string>>();

	public static bool DisableHighHeelIK { get; set; }

	[HarmonyPatch(typeof(OpcionesDeTHSDonaDeGoToDisponibles), "LoadKeys")]
	[HarmonyPostfix]
	public static void After_OpcionesDeTHSDonaDeGoToDisponibles_LoadKeys(HashSetList<string> resultado)
	{
		OnAfterGoToListPopulated.Invoke(resultado);
	}

	[HarmonyPatch(typeof(FemaleHighHeelSystem), "InteractionSystemUpdated", new Type[] { typeof(InteractionSystemV3) })]
	[HarmonyPrefix]
	public static bool Before_FemaleHighHeelSystem_InteractionSystemUpdated()
	{
		return !DisableHighHeelIK;
	}

	[HarmonyPatch(typeof(FemaleHighHeelSystem), "OnUpdateEvent1")]
	[HarmonyPrefix]
	public static bool Before_FemaleHighHeelSystem_OnUpdateEvent1()
	{
		return !DisableHighHeelIK;
	}
}
