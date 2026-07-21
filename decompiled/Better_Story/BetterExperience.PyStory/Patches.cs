using System;
using HarmonyLib;
using Microsoft.Scripting.Runtime;

namespace BetterExperience.PyStory;

public static class Patches
{
	[HarmonyPatch(typeof(ExceptionHelpers), "UpdateForRethrow", new Type[] { typeof(Exception) })]
	[HarmonyPrefix]
	public static bool NoUpdateForRethrow()
	{
		return false;
	}
}
