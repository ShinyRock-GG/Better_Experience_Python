using System;

namespace BetterExperience.Features.Console;

public class CommandArg
{
	public string Name { get; set; }

	public string Desc { get; set; }

	public Type Type { get; set; }

	public ConsoleArgMode Mode { get; set; }

	public CommandArg()
	{
	}

	public CommandArg(string name, string desc, Type type)
	{
		Name = name;
		Desc = desc;
		Type = type;
	}
}
