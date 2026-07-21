using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BetterExperience.GameScopes;
using BetterExperience.Utils;

namespace BetterExperience.Features.Console;

internal class CommandProcessor
{
	private List<Command> commands = new List<Command>();

	internal void Add(Command command, ScopeSupport scope)
	{
		commands.Add(command);
		commands.Sort((Command a, Command b) => a.Prefix[0].CompareTo(b.Prefix[0]));
		if (scope == null)
		{
			return;
		}
		scope.OnDispose += delegate
		{
			commands.Remove(command);
			commands.Sort((Command a, Command b) => a.Prefix[0].CompareTo(b.Prefix[0]));
		};
	}

	internal void Add(Func<string> processor, ScopeSupport scope)
	{
		ConsoleCommandAttribute attr = processor.Method.GetAttribute<ConsoleCommandAttribute>();
		if (attr == null)
		{
			new Logger().Info("Method {0} is not annotated", processor.Method);
		}
		Command cmd = new Command(attr.Description, attr.Prefix);
		cmd.Processor = (Dictionary<string, object> any) => processor();
		Add(cmd, scope);
	}

	internal void Add<T>(Func<T, string> processor, ScopeSupport scope)
	{
		Type type = typeof(T);
		ConsoleCommandAttribute attr = type.GetAttribute<ConsoleCommandAttribute>();
		TypedCommand<T> tc = new TypedCommand<T>(processor, attr.Description, attr.Prefix);
		PropertyInfo[] properties = type.GetProperties();
		foreach (PropertyInfo prop in properties)
		{
			ConsoleCommandArg argprop = prop.GetAttribute<ConsoleCommandArg>();
			if (argprop != null)
			{
				tc.Param(argprop.Key, argprop.Name, prop.SetValue, prop.PropertyType, argprop.Mode);
			}
		}
		Add(tc, scope);
	}

	public CommandProcessor()
	{
		Add(new Command("List commands", "help")
		{
			Args = 
			{
				new CommandArg
				{
					Name = "command",
					Desc = "optional command prefix",
					Type = typeof(string),
					Mode = ConsoleArgMode.Tail
				}
			},
			CustomParser = true,
			Processor = ProcessHelp
		}, null);
	}

	private string ProcessHelp(Dictionary<string, object> arg)
	{
		StringBuilder sb = new StringBuilder();
		string[] fixedPrefix = null;
		if (arg.ContainsKey("command"))
		{
			string prefix = arg["command"].ToString().ToLower();
			fixedPrefix = prefix.Split(new char[1] { ' ' });
			sb.Append("List of registered commands for prefix ").Append(prefix).AppendLine(":");
		}
		else
		{
			sb.AppendLine("List of registered commands:");
		}
		foreach (Command c in commands)
		{
			if (fixedPrefix != null)
			{
				bool skip = false;
				if (fixedPrefix.Length > c.Prefix.Length)
				{
					skip = true;
				}
				else
				{
					for (int i = 0; i < fixedPrefix.Length; i++)
					{
						if (fixedPrefix[i] != c.Prefix[i])
						{
							skip = true;
							break;
						}
					}
				}
				if (skip)
				{
					continue;
				}
			}
			sb.AppendLine().AppendLine();
			sb.Append(string.Join(" ", c.Prefix)).Append(" - ").AppendLine(c.Description);
			sb.Append("Example: ").AppendLine(GetExample(c));
			if (c.Args.Count <= 0)
			{
				continue;
			}
			sb.AppendLine("Where:");
			foreach (CommandArg a in c.Args)
			{
				sb.Append("   ").Append(a.Name).Append(" - ")
					.Append(a.Desc)
					.AppendLine();
			}
		}
		return sb.ToString();
	}

	public string Run(string cmd)
	{
		string[] words = cmd.Split(new char[1] { ' ' });
		foreach (Command c in commands)
		{
			if (c.Prefix.Length > words.Length)
			{
				continue;
			}
			bool accept = true;
			for (int i = 0; i < c.Prefix.Length; i++)
			{
				if (c.Prefix[i].ToLower() != words[i].ToLower())
				{
					accept = false;
					break;
				}
			}
			if (accept)
			{
				try
				{
					return ExecCommand(c, words);
				}
				catch (Exception ex)
				{
					new Logger().Error(ex, "Command failed: " + cmd);
					return "OOPS! Exception: " + ex.Message + "\n See log for details";
				}
			}
		}
		return "Unrecognized command. Type help for help";
	}

	private string ExecCommand(Command c, string[] words)
	{
		int from = c.Prefix.Length;
		Dictionary<string, object> param = new Dictionary<string, object>();
		Dictionary<string, CommandArg> cargmap = c.Args.ToDictionary((CommandArg x) => x.Name.ToLower());
		List<CommandArg> tails = c.Args.FindAll((CommandArg x) => x.Mode == ConsoleArgMode.Tail);
		if (tails.Count > 1)
		{
			throw new Exception("Too much tails");
		}
		CommandArg tail = ((tails.Count > 0) ? tails[0] : null);
		if (tail != null)
		{
			cargmap.Remove(tail.Name.ToLower());
		}
		for (; from < words.Length; from++)
		{
			string par = words[from];
			if (cargmap.Count == 0)
			{
				break;
			}
			if (!cargmap.TryGetValue(par.ToLower(), out var carg))
			{
				if (tail != null)
				{
					break;
				}
				return "Incorrect syntax: unexpected param " + par + "\nExpected syntax: " + GetExample(c);
			}
			switch (carg.Mode)
			{
			case ConsoleArgMode.KeyValue:
				if (words.Length <= from + 1)
				{
					return "Incorrect syntax: missing required param <" + par + ">\nExpected syntax: " + GetExample(c);
				}
				try
				{
					object mapped = Convert.ChangeType(words[from + 1], carg.Type);
					param[carg.Name] = mapped;
				}
				catch (Exception ex)
				{
					return "Incorrect syntax: unable to convert param <" + par + ">=<" + words[from + 1] + ":> " + ex.Message + "\nExpected syntax: " + GetExample(c);
				}
				from++;
				break;
			case ConsoleArgMode.Flag:
				param[carg.Name] = true;
				break;
			case ConsoleArgMode.Tail:
				throw new Exception();
			}
		}
		if (from < words.Length)
		{
			if (tail == null)
			{
				return "Too much args.\nExpected syntax:" + GetExample(c);
			}
			string[] tailarr = new string[words.Length - from];
			Array.Copy(words, from, tailarr, 0, tailarr.Length);
			param[tail.Name] = string.Join(" ", tailarr);
		}
		return c.Processor(param);
	}

	private string GetExample(Command c)
	{
		List<string> r = new List<string>();
		r.AddRange(c.Prefix);
		c.Args.ForEach(delegate(CommandArg e)
		{
			if (e.Mode != ConsoleArgMode.Tail)
			{
				r.Add(e.Name);
			}
			r.Add("<" + e.Name + ":" + e.Type.Name + ">");
		});
		return string.Join(" ", r);
	}
}
