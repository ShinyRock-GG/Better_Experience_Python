using System;
using System.Collections.Generic;

namespace BetterExperience.Features.Console;

public class Command
{
	public string Description { get; set; }

	public string[] Prefix { get; }

	public bool CustomParser { get; set; }

	public List<CommandArg> Args { get; } = new List<CommandArg>();

	public Func<Dictionary<string, object>, string> Processor { get; set; }

	public Command(string description, params string[] prefix)
		: this((Func<Dictionary<string, object>, string>)null, description, prefix)
	{
	}

	public Command(Func<string> proc, string description, params string[] prefix)
		: this((Dictionary<string, object> any) => proc(), description, prefix)
	{
	}

	public Command(Func<Dictionary<string, object>, string> proc, string description, params string[] prefix)
	{
		Description = description;
		Prefix = prefix;
		Processor = proc;
	}
}
