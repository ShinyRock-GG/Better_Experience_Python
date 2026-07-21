using System;

namespace BetterExperience.Features.Console;

[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ConsoleCommandArg : Attribute
{
	public string Key { get; set; }

	public string Name { get; set; }

	public ConsoleArgMode Mode { get; set; }
}
