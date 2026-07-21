using System;
using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones.Handlers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ai;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using Assets._ReusableScripts.Genetica.NPCs;
using Assets._ReusableScripts.PhysicsScripts;
using Assets._ReusableScripts.Respiracion;
using Assets.Base.Plugins.Runtime;
using Assets.Productos.Juegos.Reception;
using Assets.Productos.Juegos.Reception.Scripts.AutoRatingsProfiles;
using Assets.Productos.Juegos.Reception.Scripts.Dependientes.ScenaManagers;
using Assets.Productos.Juegos.Reception.Scripts.Entrevistas;
using Assets.Productos.Juegos.Reception.Scripts.Genetica.Globales;
using BetterExperience.GameScopes;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.HarmonyPatches;

internal class SMAGlobalPatches
{
	public class UpdateRangeEvent
	{
		public RangeValueV2 Range { get; set; }
	}

	public class AutoratingInterception
	{
		public bool Suppress { get; set; }
	}

	private static Logger logger = new Logger();

	public static readonly Observable<PanelDeEntrevistaCalificacion> AfterRatingModelRefreshed = new Observable<PanelDeEntrevistaCalificacion>();

	public static readonly Observable<ISujetoIdentificableNpc> BeforeCharacterLoaded = new Observable<ISujetoIdentificableNpc>();

	public static readonly Observable<PiscinaDeNpcsManager> OnNewPoolCreated = new Observable<PiscinaDeNpcsManager>();

	public static readonly Observable<PiscinaDeNpcsManager> OnPoolDestroyed = new Observable<PiscinaDeNpcsManager>();

	public static readonly Observable<PiscinaDeNpcsManager, ISujetoIdentificableNpc> OnGuestClassified = new Observable<PiscinaDeNpcsManager, ISujetoIdentificableNpc>();

	public static readonly Observable OnBeforeSave = new Observable();

	public static readonly Observable<HitSkin, HitSkin.Colision> OnContinousSkinCollision = new Observable<HitSkin, HitSkin.Colision>();

	public static readonly Observable<UpdateRangeEvent> OnComputeMaxVelocity = new Observable<UpdateRangeEvent>();

	public static readonly Observable<UpdateRangeEvent> OnComputeMaxDepth = new Observable<UpdateRangeEvent>();

	public static readonly Observable<bool> OnLoaderScreenUpdate = new Observable<bool>();

	public static readonly Observable<JointDistancesAdmin> BeforeUpdateJointDistances = new Observable<JointDistancesAdmin>();

	public static readonly Observable<AutoratingInterception> BeforeAutorating = new Observable<AutoratingInterception>();

	private static SMASceneLoaderState SCENE_LOADER_STATE = new SMASceneLoaderState();

	private static bool _afterAutorating = false;

	public static Observable<StringKeyFloatValueDictionary, StringKeyFloatValueDictionary> AfterAutorating { get; } = new Observable<StringKeyFloatValueDictionary, StringKeyFloatValueDictionary>();

	[HarmonyPatch(typeof(PanelDeEntrevistaCalificacion), "ActualizarValoresDeModelo")]
	[HarmonyPostfix]
	public static void OnRefreshRating(PanelDeEntrevistaCalificacion __instance)
	{
		AfterRatingModelRefreshed.Invoke(__instance);
	}

	// EntrevistaConFemaleDePoolDelDia is [Obsolete(error:true)] in SMA 23.1.
	// Use [HarmonyTargetMethod] for runtime type resolution to avoid a compile-time reference.
	[HarmonyPatch]
	private static class EntrevistaCalificarPatch
	{
		[HarmonyTargetMethod]
		static System.Reflection.MethodBase TargetMethod()
		{
			var type = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
				.FirstOrDefault(t => t.Name == "EntrevistaConFemaleDePoolDelDia");
			return type?.GetMethod("CalificarCurrentFemale",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		}

		[HarmonyPrefix]
		static void Prefix(object __instance)
		{
			if (_afterAutorating)
			{
				Traverse t = Traverse.Create(__instance);
				AfterAutorating.Invoke(
					t.Field<StringKeyFloatValueDictionary>("flagScoreAparienciaCurrentFemaleV2").Value,
					t.Field<StringKeyFloatValueDictionary>("flagScorePersonalidadCurrentFemaleV2").Value
				);
			}
			_afterAutorating = false;
		}
	}

	[HarmonyPatch(typeof(LoaderDeNpcFemeninos), "Load")]
	[HarmonyPrefix]
	public static void BeforeCharacterLoad(ISujetoIdentificableNpc npc)
	{
		BeforeCharacterLoaded.Invoke(npc);
	}

	[HarmonyPatch(typeof(LoadingPanel), "Update")]
	[HarmonyPostfix]
	public static void OnUpdateLoaderScreen(LoadingPanel __instance, bool ___m_showing)
	{
		bool loaderScreenVisibleNow = ___m_showing;
		if (SCENE_LOADER_STATE.SceneLoaderStarted && SCENE_LOADER_STATE.LoadingScreenIsVisible && !loaderScreenVisibleNow)
		{
			SCENE_LOADER_STATE.SceneLoaderStarted = false;
		}
		if (SCENE_LOADER_STATE.LoadingScreenIsVisible != loaderScreenVisibleNow)
		{
			OnLoaderScreenUpdate.Invoke(loaderScreenVisibleNow);
		}
		SCENE_LOADER_STATE.LoadingScreenIsVisible = loaderScreenVisibleNow;
	}

	// instanciarNuevaYGuardarEnMemoria signature changed in SMA 23.1 — find by name to avoid parameter mismatch
	[HarmonyPatch]
	private static class AfterNewPoolCreatedPatch
	{
		[HarmonyTargetMethod]
		static System.Reflection.MethodBase TargetMethod()
		{
			var method = typeof(PiscinasDeNPCs)
				.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
				.FirstOrDefault(m => m.Name == "instanciarNuevaYGuardarEnMemoria");
			UnityEngine.Debug.LogWarning("[BE:AfterNewPoolCreatedPatch] target=" + (method?.ToString() ?? "NULL"));
			return method;
		}

		[HarmonyPostfix]
		static void Postfix(object __result)
		{
			if (__result is PiscinaDeNpcsManager pm)
				OnNewPoolCreated.Invoke(pm);
		}
	}

	[HarmonyPatch(typeof(PiscinasDeNPCs), "BorrarCompletamentePiscina")]
	[HarmonyPostfix]
	public static void AfterPoolDestroyed(PiscinaDeNpcsManager piscina)
	{
		OnPoolDestroyed.Invoke(piscina);
	}

	[HarmonyPatch(typeof(PiscinaDeNpcsManager), "FlagSujetoComoCalificado")]
	[HarmonyPostfix]
	public static void AfterGuestClassified(PiscinaDeNpcsManager __instance, ISujetoIdentificableNpc sujeto)
	{
		OnGuestClassified.Invoke(__instance, sujeto);
	}

	// PanelComenzarATrabajar.OnSaving() became event onSaving in SMA 23.1.
	// Patch Awake to subscribe our handler to the event via reflection.
	[HarmonyPatch]
	private static class BeforeGameSavedPatch
	{
		[HarmonyTargetMethod]
		static System.Reflection.MethodBase TargetMethod() =>
			typeof(PanelComenzarATrabajar).GetMethod("Awake",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		[HarmonyPostfix]
		static void Postfix(PanelComenzarATrabajar __instance)
		{
			var evt = typeof(PanelComenzarATrabajar).GetEvent("onSaving",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			evt?.AddEventHandler(__instance, (Action)(() => OnBeforeSave.Invoke()));
		}
	}

	[HarmonyPatch(typeof(HitSkin), "OnStay")]
	[HarmonyPrefix]
	public static void OnContiniousCollision(HitSkin __instance, HitSkin.Colision collision)
	{
		OnContinousSkinCollision.Invoke(__instance, collision);
	}

	// RespiracionEngine param removed from VelocidadToleranciaGetter in SMA 23.1 — only keep __result
	[HarmonyPatch(typeof(AutoSexRangosDeToleranciaCalculador), "VelocidadToleranciaGetter")]
	[HarmonyPostfix]
	public static void _OnComputeMaxVelocity(ref RangeValueV2 __result)
	{
		UpdateRangeEvent evt = new UpdateRangeEvent();
		evt.Range = __result;
		OnComputeMaxVelocity.Invoke(evt);
		__result = evt.Range;
	}

	// RespiracionEngine param removed from ProfundidadToleranciaGetter in SMA 23.1 — only keep __result
	[HarmonyPatch(typeof(AutoSexRangosDeToleranciaCalculador), "ProfundidadToleranciaGetter")]
	[HarmonyPostfix]
	public static void _OnComputeMaxDepth(ref RangeValueV2 __result)
	{
		UpdateRangeEvent evt = new UpdateRangeEvent();
		evt.Range = __result;
		OnComputeMaxDepth.Invoke(evt);
		__result = evt.Range;
	}

	[HarmonyPatch(typeof(JointDistancesAdmin), "UpdateDistaceAndTargetMods")]
	[HarmonyPrefix]
	public static void Before_JointDistancesAdmin_UpdateDistaceAndTargetMods(JointDistancesAdmin __instance)
	{
		BeforeUpdateJointDistances.Invoke(__instance);
	}

	[HarmonyPatch(typeof(AutoRatingFemaleLogic), "DoAutoRating", new Type[] { })]
	[HarmonyPrefix]
	public static bool Before_AutoRatingFemaleLogic_DoAutoRating0()
	{
		_afterAutorating = true;
		AutoratingInterception i = new AutoratingInterception();
		BeforeAutorating.Invoke(i);
		return !i.Suppress;
	}

	[HarmonyPatch(typeof(GoToScenaManager), "Remove", new Type[] { typeof(GoToScenaManager.GoTo) })]
	[HarmonyPostfix]
	public static void After_GoToSceneManager_Remove_GoTo(bool __result, GoToScenaManager.GoTo item, Dictionary<Transform, GoToScenaManager.GoTo> ___m_porTransform)
	{
		if (__result)
		{
			___m_porTransform.Remove(item.transform);
		}
	}
}
