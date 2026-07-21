using System;

namespace BetterExperience;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class TimedAttribute : Attribute
{
	public static float THRESHOLD = 2f;
}
