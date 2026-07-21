using System;
using System.Collections.Generic;

namespace BetterExperience.Features.Console;

public class TypedCommand<T> : Command
{
	private Dictionary<string, Action<T, object>> setters = new Dictionary<string, Action<T, object>>();

	public Func<T, string> TypedProcessor { get; private set; }

	public TypedCommand(Func<T, string> processor, string description, params string[] prefix)
		: base(description, prefix)
	{
		TypedProcessor = processor;
		base.Processor = LocalProcessor;
	}

	private string LocalProcessor(Dictionary<string, object> arg)
	{
		T req = (T)Activator.CreateInstance(typeof(T));
		foreach (KeyValuePair<string, object> kv in arg)
		{
			Action<T, object> setter = setters[kv.Key.ToLower()];
			setter(req, kv.Value);
		}
		return TypedProcessor(req);
	}

	protected void Param<K>(string name, string description, Action<T, K> setter)
	{
		base.Args.Add(new CommandArg
		{
			Name = name,
			Desc = description,
			Type = typeof(K)
		});
		setters[name.ToLower()] = delegate(T obj, object value)
		{
			setter(obj, (K)value);
		};
	}

	internal void Param(string name, string description, Action<object, object> setter, Type t, ConsoleArgMode mode)
	{
		base.Args.Add(new CommandArg
		{
			Name = name,
			Desc = description,
			Type = t,
			Mode = mode
		});
		setters[name.ToLower()] = delegate(T obj, object value)
		{
			setter(obj, value);
		};
	}
}
