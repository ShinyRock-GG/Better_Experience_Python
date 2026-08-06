using System;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

/// <summary>
/// Keeps the game's native Graphics-settings Resolution + Refresh-Rate dropdowns showing the
/// TRUE selected values (e.g. 2560x1440 @ 239.97Hz) instead of silently reverting to the rounded
/// nearest entry (640x480 @ 240Hz) the moment Apply is pressed.
///
/// The applied resolution itself is already correct and persists across sessions; only the two
/// dropdowns' DISPLAYED values were wrong, because the game re-derives them through a refresh-rate
/// value that has been rounded to a whole number.
///
/// Root cause (two-part):
///  1. UserData stores Hz as an int (240). ResolutionUnity -> Resolution.set_refreshRate(240) makes
///     Unity-2022 RefreshRate{240,1} = 240.000, but Screen.resolutions reports {239970,1000} = 239.97.
///     GraphicsModel.Bindig()'s exact-value comparison then fails and falls back to the lowest Hz.
///  2. Even once frecuencia is fixed, resolucion.refreshRateRatio is still {240,1}; the resolution
///     dropdown is filtered to the real 239.97 entries, cannot find {w,h,{240,1}} in that list, and
///     falls back to the first entry = 640x480.
///
/// Fix: prefix the private GraphicsModel.Bindig() -- before any comparison runs -- and snap BOTH
/// 'frecuencia' (double) AND 'resolucion' (Resolution struct) to the closest real
/// Screen.resolutions RefreshRate. Uses direct FieldInfo.SetValue (not Traverse) so the struct
/// field writes actually land. Ported verbatim from AIchat's DialogInterceptorMod
/// Fix_GraphicsModel_Bindig so the two mods share one authoritative implementation.
///
/// Toggle: BetterExperience settings window -> Common tab -> "Fix resolution/refresh-rate dropdowns".
/// Defaults ON; read live by the patch, so flipping it takes effect on the next Bindig() rebind.
/// </summary>
internal class ResolutionFixFeature : PluginFeature
{
	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		base.Configure(config);
		enableFeature = config.Bind<bool>("Features", "FixResolutionDropdowns", true,
			"Fix resolution/refresh-rate dropdowns: keep the true values (e.g. 239.97Hz) instead of reverting to rounded (640x480 / 240Hz) after Apply");
	}

	public override void OnInit()
	{
		base.OnInit();
		// Toggle lives in the BetterExperience settings window's Common tab (default ON).
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.general);
		// Expose the live toggle value to the static Harmony prefix.
		Fix_GraphicsModel_Bindig.IsEnabled = () => enableFeature.Value;
		// The Graphics options menu is global (not session-scoped), so patch once at init.
		Fix_GraphicsModel_Bindig.EnsurePatched();
	}

	[HarmonyPatch]
	private static class Fix_GraphicsModel_Bindig
	{
		internal static Func<bool> IsEnabled = () => true;

		private static bool patched;
		private static Type _gfxType;
		private static FieldInfo _frecuenciaField;
		private static FieldInfo _resolucionField;

		internal static void EnsurePatched()
		{
			if (patched)
			{
				return;
			}
			patched = true;
			Harmony.CreateAndPatchAll(typeof(Fix_GraphicsModel_Bindig), (string)null);
		}

		[HarmonyTargetMethod]
		private static MethodBase TargetMethod()
		{
			_gfxType = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
				.FirstOrDefault(t => t.Name == "GraphicsModel");
			_frecuenciaField = _gfxType?.GetField("frecuencia", BindingFlags.Public | BindingFlags.Instance);
			_resolucionField = _gfxType?.GetField("resolucion", BindingFlags.Public | BindingFlags.Instance);
			return _gfxType?.GetMethod("Bindig", BindingFlags.NonPublic | BindingFlags.Instance);
		}

		[HarmonyPrefix]
		private static void Prefix(object __instance)
		{
			if (!IsEnabled())
			{
				return;
			}
			if (_frecuenciaField == null || _resolucionField == null)
			{
				return;
			}

			double frecuencia = (double)_frecuenciaField.GetValue(__instance);
			if (frecuencia <= 0.0)
			{
				return;
			}

			// Snap to the real Screen.resolutions RefreshRate closest to the stored Hz.
			// (240.0 from the legacy int setter -> best = {239970,1000} = 239.97)
			RefreshRate best = Screen.resolutions
				.Select(r => r.refreshRateRatio)
				.Distinct()
				.OrderBy(r => Math.Abs(r.value - frecuencia))
				.FirstOrDefault();
			if (best.denominator == 0)
			{
				return;
			}

			// Fix frecuencia so Bindig()'s exact-match check doesn't fall back to the lowest Hz.
			_frecuenciaField.SetValue(__instance, best.value);

			// Fix resolucion so the resolution dropdown can find the saved w x h inside the
			// resoluciones list (which is filtered by exact Hz value).
			Resolution res = (Resolution)_resolucionField.GetValue(__instance);
			if (Math.Abs(res.refreshRateRatio.value - best.value) >= 0.001)
			{
				Resolution matched = default(Resolution);
				foreach (Resolution r in Screen.resolutions)
				{
					if (r.width == res.width && r.height == res.height
						&& Math.Abs(r.refreshRateRatio.value - best.value) < 0.001)
					{
						matched = r;
						break;
					}
				}
				_resolucionField.SetValue(__instance,
					matched.width > 0 ? matched : new Resolution
					{
						width = res.width,
						height = res.height,
						refreshRateRatio = best
					});
			}
		}
	}
}
