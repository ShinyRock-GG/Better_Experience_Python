using BetterExperience.GameScopes;
using HarmonyLib;

namespace BetterExperience;

public static class MonkeyInputPatch
{
	[HarmonyPatch("Monkey.Core.PluginBehaviour, Monkey, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", "IsModalLocked")]
	[HarmonyPostfix]
	public static void OnCheckInputIntercepted(ref bool __result)
	{
		if (!__result)
		{
			__result = InputManager.focusTracker.IsTextField();
		}
	}
}
