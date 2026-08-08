using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace BetterExperience.CMA;

/// <summary>
/// STAGE 0 — the toolchain proof, and nothing else.
///
/// It reports what it can see and exits. That is deliberate: every later stage adds ported game
/// code, and if the very first build also carried features then a failure could be the
/// references, the target framework, the loader contract, or the port itself. This separates the
/// one from the other four.
///
/// It also answers two questions the port assessment could only infer, by asking the running
/// game instead of a graph:
///   - are the interop assemblies actually loadable from a plugin, or only present on disk
///   - is Monkey here (BE's SceneCamera/PlayerScaler defer to it when it is)
///
/// Note the BepInEx 6 differences already visible in this tiny file, both of which the real port
/// must make everywhere: BasePlugin instead of BaseUnityPlugin, Load() instead of Awake(), and
/// Log instead of Logger.
/// </summary>
[BepInPlugin("rock.betterexperience.cma", "BetterExperience (CMA port, stage 0)", "0.1.0")]
public class Stage0Plugin : BasePlugin
{
    public override void Load()
    {
        Log.LogInfo("=== BetterExperience CMA stage 0 ===");
        Log.LogInfo($"loaded into {IL2CPPChainloader.Instance.Plugins.Count} plugin(s)");

        // Chainloader.PluginInfos (BepInEx 5) moved here. Core BE's Plugin.Awake uses the old
        // form to detect Monkey, so this line is the port of an actual line we will need.
        bool monkey = IL2CPPChainloader.Instance.Plugins.ContainsKey("com.thora.monkey");
        Log.LogInfo($"Monkey present: {monkey}  (false => BE would enable SceneCamera/PlayerScaler)");

        // Touch a real game type through interop. If the reference set is wrong this throws or
        // fails to resolve, which is exactly what stage 0 exists to find out.
        try
        {
            var t = Il2CppInterop.Runtime.Il2CppType.From(
                typeof(Il2CppAssets._ReusableScripts.CuchiCuchi.Dependentes.Controllers
                       .PelvisMovementController));
            Log.LogInfo($"interop reachable: PelvisMovementController -> {t?.FullName ?? "null"}");
        }
        catch (System.Exception e)
        {
            Log.LogError($"interop NOT reachable: {e.GetType().Name}: {e.Message}");
        }
    }
}
