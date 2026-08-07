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
/// Root cause (CONFIRMED in-game 2026-08-06, SMA 23.1):
///  1. ConfiguracionGeneralDeGraficos persists the mode as a Vector3Int, so the refresh rate is
///     stored as an INTEGER (Resoulucion.z). A true 239.97 comes back as 240 via the legacy
///     Resolution.refreshRate setter -> RefreshRate{240,1}.
///  2. The trap: 240 IS a legitimate rate in Screen.resolutions -- but only for small modes
///     (640x480 etc). So GraphicsModel.Bindig()'s own check, Any(f => f.value == frecuencia),
///     PASSES and the game never detects a problem.
///  3. Bindig then calls ReloadResolutions(240), which filters 'resoluciones' to just those
///     small modes. The user's 2560x1440 is NOT in that list, so the resolution dropdown falls
///     back to entry 0 = 640x480. (Measured: resoluciones count=10, first=640x480@240/1, and
///     the current mode absent by both struct equality AND width/height.)
///
/// Fix: prefix the private GraphicsModel.Bindig() and anchor on the RESOLUTION, not the rate --
/// among Screen.resolutions entries matching the stored width x height, take the one whose rate
/// is closest to the stored (rounded) Hz, then write BOTH 'frecuencia' and 'resolucion' from that
/// exact struct so the dropdown's equality-based selection can find it. Uses direct
/// FieldInfo.SetValue (not Traverse) so the struct field write lands.
///
/// NOTE: snapping the RATE to the nearest available value (as AIchat's Fix_GraphicsModel_Bindig
/// does) is a no-op here -- 240 is already present, so it "corrects" 240 to 240. That approach
/// was tried and REFUTED in-game before this one.
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
		// Deferred: GraphicsModel lives in TValle.BeachGirl.UI, which may not be loaded this
		// early. EnsurePatched retries until the type resolves (see TryPatch).
		if (!Fix_GraphicsModel_Bindig.EnsurePatched())
		{
			// Not resolvable yet — retry each update until it patches.
			Lookup<DispatcherService>().DoUpdate.Add(RetryPatch, base.Scope);
		}
	}

	private void RetryPatch()
	{
		if (Fix_GraphicsModel_Bindig.EnsurePatched())
		{
			Lookup<DispatcherService>().DoUpdate.Remove(RetryPatch);
		}
	}

	[HarmonyPatch]
	private static class Fix_GraphicsModel_Bindig
	{
		internal static Func<bool> IsEnabled = () => true;
		internal static readonly Logger logger = new Logger { Prefix = "[ResolutionFix]:" };

		private static bool patched;
		private static Type _gfxType;
		private static FieldInfo _frecuenciaField;
		private static FieldInfo _resolucionField;

		/// <summary>Returns true once the patch is applied. Safe to call repeatedly.</summary>
		internal static bool EnsurePatched()
		{
			if (patched)
			{
				return true;
			}
			// Resolve first: GraphicsModel lives in TValle.BeachGirl.UI, which may not be
			// loaded yet. Patching with a null target would throw, so bail and retry later.
			if (Resolve() == null)
			{
				return false;
			}
			try
			{
				Harmony.CreateAndPatchAll(typeof(Fix_GraphicsModel_Bindig), (string)null);
				patched = true;
				logger.Info("patched GraphicsModel.Bindig (frecuencia={0}, resolucion={1})",
					_frecuenciaField != null, _resolucionField != null);
			}
			catch (Exception e)
			{
				patched = true; // don't spin forever on a hard failure
				logger.Error(e, "failed to patch GraphicsModel.Bindig — dropdown fix inactive");
			}
			return patched;
		}

		private static MethodBase Resolve()
		{
			_gfxType = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
				.FirstOrDefault(t => t.Name == "GraphicsModel");
			if (_gfxType == null)
			{
				return null;
			}
			_frecuenciaField = _gfxType.GetField("frecuencia", BindingFlags.Public | BindingFlags.Instance);
			_resolucionField = _gfxType.GetField("resolucion", BindingFlags.Public | BindingFlags.Instance);
			return _gfxType.GetMethod("Bindig", BindingFlags.NonPublic | BindingFlags.Instance)
				?? _gfxType.GetMethod("Bindig", BindingFlags.Public | BindingFlags.Instance);
		}

		[HarmonyTargetMethod]
		private static MethodBase TargetMethod()
		{
			return Resolve();
		}

		[HarmonyPrefix]
		private static void Prefix(object __instance)
		{
			if (!IsEnabled())
			{
				logger.Info("Bindig: fix disabled by toggle — leaving values alone");
				return;
			}
			if (_frecuenciaField == null || _resolucionField == null)
			{
				logger.Warn("Bindig: fields unresolved (frecuencia={0}, resolucion={1}) — no fix applied",
					_frecuenciaField != null, _resolucionField != null);
				return;
			}

			double frecuencia = (double)_frecuenciaField.GetValue(__instance);
			if (frecuencia <= 0.0)
			{
				logger.Warn("frecuencia<=0 — no fix applied");
				return;
			}

			// THE FIX — anchor on the RESOLUTION, not the rate.
			//
			// The stored Hz is an integer (the config keeps it as Vector3Int.z), so a true
			// 239.97 comes back as 240. Crucially, 240 IS a legitimate rate in
			// Screen.resolutions — but only for tiny modes (640x480 etc). So the game's own
			// check (Any(f => f.value == frecuencia)) PASSES, nothing looks wrong, and
			// ReloadResolutions(240) then filters the list down to those small modes. The
			// user's 2560x1440 is absent from that list, so the dropdown falls back to entry
			// 0 = 640x480.
			//
			// Therefore: pick the rate that actually SUPPORTS the stored width x height,
			// choosing the candidate closest to the stored (rounded) Hz. That restores
			// 239.97 for 2560x1440 instead of "correcting" 240 to itself.
			Resolution res = (Resolution)_resolucionField.GetValue(__instance);
			Resolution[] all = Screen.resolutions;

			Resolution match = all
				.Where(r => r.width == res.width && r.height == res.height)
				.OrderBy(r => Math.Abs(r.refreshRateRatio.value - frecuencia))
				.FirstOrDefault();

			if (match.width == 0)
			{
				// Stored resolution isn't offered by the display at all — leave the game's
				// own fallback to handle it rather than inventing a mode.
				logger.Warn("Bindig: {0}x{1} not offered by display — leaving game default",
					res.width, res.height);
				return;
			}

			if (Math.Abs(match.refreshRateRatio.value - frecuencia) > 0.0001)
			{
				logger.Info("{0}x{1}: restored refresh rate {2} -> {3} ({4}/{5})",
					match.width, match.height, frecuencia, match.refreshRateRatio.value,
					match.refreshRateRatio.numerator, match.refreshRateRatio.denominator);
			}

			// Both must agree, and resolucion must be the EXACT struct from Screen.resolutions
			// so the dropdown's equality-based selection finds it.
			_frecuenciaField.SetValue(__instance, match.refreshRateRatio.value);
			_resolucionField.SetValue(__instance, match);
		}

	}
}
