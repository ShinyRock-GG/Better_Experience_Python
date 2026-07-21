using System;

namespace BetterExperience.Features.Console;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class ConsoleCommandAttribute : Attribute
{
	private readonly string description;

	public string Description => description;

	public string[] Prefix { get; private set; }

	public ConsoleCommandAttribute(string description, params string[] prefix)
	{
		this.description = description;
		Prefix = prefix;
	}
}
