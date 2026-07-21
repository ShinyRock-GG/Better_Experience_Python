using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BetterExperience.Utils;
using HarmonyLib;

namespace BetterExperience;

[HarmonyPatch]
public static class ProfilerPatch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		foreach (Type type in AccessTools.AllTypes())
		{
			if (type.IsGenericType || type.IsInterface)
			{
				continue;
			}
			foreach (MethodInfo m in AccessTools.GetDeclaredMethods(type))
			{
				if (Test(m))
				{
					yield return m;
				}
			}
		}
	}

	private static bool Test(MethodInfo m)
	{
		try
		{
			return m.GetAttribute<TimedAttribute>() != null;
		}
		catch
		{
			return false;
		}
	}

	public static void Prefix(MethodBase __originalMethod, out Stopwatch __state)
	{
		__state = Profiler._BorrowStopwatch();
		__state.Restart();
	}

	public static void Postfix(MethodBase __originalMethod, Stopwatch __state)
	{
		__state.Stop();
		if (__state.ElapsedMilliseconds > Profiler.Threshold)
		{
			Profiler.logger.Warn("Slow method {0}: {1}ms", __originalMethod.ReflectedType.Name + ":" + __originalMethod.Name, __state.ElapsedMilliseconds);
		}
		Profiler._ReleaseStopwatch(__state);
	}
}
