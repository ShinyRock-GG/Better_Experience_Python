using System;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Pool;

namespace BetterExperience;

public class Profiler
{
	private static ObjectPool<Stopwatch> stopwatches = new ObjectPool<Stopwatch>(() => new Stopwatch());

	public static Logger logger { get; } = Logger.Create<Profiler>();

	public static long Threshold { get; private set; } = 2L;

	public static bool Enabled { get; set; } = true;

	public static void Install()
	{
		try
		{
			Harmony.CreateAndPatchAll(typeof(ProfilerPatch), (string)null);
		}
		catch (Exception ex)
		{
			Logger.LoggerImpl.LogWarning((object)ex);
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(ObservableProfiler), (string)null);
		}
		catch (Exception ex2)
		{
			Logger.LoggerImpl.LogWarning((object)ex2);
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(ScopeProfiler), (string)null);
		}
		catch (Exception ex3)
		{
			Logger.LoggerImpl.LogWarning((object)ex3);
		}
	}

	public static IEnumerator GCReporter()
	{
		int gccount = GC.CollectionCount(1);
		Stopwatch sw = new Stopwatch();
		sw.Start();
		int counter = 0;
		while (true)
		{
			int ngc = GC.CollectionCount(1);
			if (ngc > gccount)
			{
				sw.Stop();
				logger.Warn("GC collections: {0} in {1}ms", ngc, sw.ElapsedMilliseconds);
				gccount = ngc;
				sw.Restart();
			}
			yield return new WaitForSeconds(0.1f);
			counter++;
		}
	}

	private static string PrettyNumber(long num)
	{
		int gs = 1000000000;
		int ms = 1000000;
		int ks = 1000;
		long Gs = num / 1000000000;
		long Ms = num / ms;
		long Ks = num / ks;
		if (Gs > 1)
		{
			return (1f * (float)num / (float)gs).ToString("G02") + "G";
		}
		if (Ms > 2)
		{
			return (1f * (float)num / (float)ms).ToString("G02") + "M";
		}
		if (Ks > 2)
		{
			return (1f * (float)num / (float)ks).ToString("G02") + "K";
		}
		return num.ToString();
	}

	public static Stopwatch _BorrowStopwatch()
	{
		lock (stopwatches)
		{
			return stopwatches.Get();
		}
	}

	public static void _ReleaseStopwatch(Stopwatch sw)
	{
		lock (stopwatches)
		{
			stopwatches.Release(sw);
		}
	}
}
