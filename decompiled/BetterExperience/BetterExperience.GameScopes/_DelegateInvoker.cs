using System;

namespace BetterExperience.GameScopes;

internal static class _DelegateInvoker
{
	public static void InvokeDelegate(object[] args, Delegate fn)
	{
		fn.DynamicInvoke(args);
	}
}
