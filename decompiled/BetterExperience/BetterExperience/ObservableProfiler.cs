using System;
using System.Diagnostics;
using BetterExperience.GameScopes;
using HarmonyLib;

namespace BetterExperience;

[HarmonyPatch(typeof(_DelegateInvoker), "InvokeDelegate")]
public static class ObservableProfiler
{
	[HarmonyPrefix]
	public static void Begin(out Stopwatch __state)
	{
		__state = Profiler._BorrowStopwatch();
		__state.Restart();
	}

	[HarmonyPostfix]
	public static void End(Delegate fn, Stopwatch __state)
	{
		__state.Stop();
		if (__state.ElapsedMilliseconds > Profiler.Threshold)
		{
			Profiler.logger.Warn(FormatDelegate(fn, __state.ElapsedMilliseconds));
		}
		Profiler._ReleaseStopwatch(__state);
	}

	public static string FormatDelegate(Delegate fn, float t)
	{
		return $"Delegate {fn.Target.GetType().Name}.{fn.Method.Name}: {t}ms";
	}
}
