using System.Diagnostics;
using BetterExperience.GameScopes;
using HarmonyLib;

namespace BetterExperience;

[HarmonyPatch(typeof(ScopeSupport), "Start")]
public static class ScopeProfiler
{
	[HarmonyPrefix]
	public static void Begin(out Stopwatch __state)
	{
		__state = Profiler._BorrowStopwatch();
		__state.Restart();
	}

	[HarmonyPostfix]
	public static void End(ScopeSupport __instance, Stopwatch __state)
	{
		__state.Stop();
		if (__state.ElapsedMilliseconds > Profiler.Threshold)
		{
			Profiler.logger.Warn("Service start {0}: {1}ms", __instance.Name, __state.ElapsedMilliseconds);
		}
		Profiler._ReleaseStopwatch(__state);
	}
}
