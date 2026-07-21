using System;
using BepInEx.Logging;
using UnityEngine;

namespace BetterExperience;

public class Logger
{
	public static ManualLogSource LoggerImpl { get; set; }

	public static Logger Global { get; } = new Logger();

	public string Prefix { get; set; }

	public bool EnableInfo { get; set; } = true;

	public bool EnableDebug { get; set; }

	public static Logger Create<T>()
	{
		return new Logger(typeof(T));
	}

	public Logger()
		: this((string)null)
	{
	}

	public Logger(Type type)
		: this("[" + type.Name + "] ")
	{
	}

	public Logger(string prefix)
	{
		Prefix = prefix;
	}

	public void Info(string format, params object[] args)
	{
		if (EnableInfo && LoggerImpl != null)
		{
			string message = AutoFormat(format, args);
			LoggerImpl.LogMessage((object)message);
		}
	}

	public void InfoRare(int rate, string format, params object[] args)
	{
		if (Time.frameCount % rate == 0)
		{
			Info(format, args);
		}
	}

	public void InfoRare(string format, params object[] args)
	{
		if (Time.frameCount % 60 == 0)
		{
			Info(format, args);
		}
	}

	public void Error(string format, params object[] args)
	{
		if (LoggerImpl != null)
		{
			string message = AutoFormat(format, args);
			LoggerImpl.LogError((object)message);
		}
	}

	public void Error(Exception ex, string format, params object[] args)
	{
		if (LoggerImpl != null)
		{
			string message = AutoFormat(format, args);
			if (ex != null)
			{
				string stactrace = ex.ToString();
				message = message + "\n" + stactrace;
			}
			LoggerImpl.LogError((object)message);
		}
	}

	public void Debug(string format, params object[] args)
	{
		if (EnableDebug && LoggerImpl != null)
		{
			string message = AutoFormat(format, args);
			LoggerImpl.LogInfo((object)message);
		}
	}

	public void Warn(string format, params object[] args)
	{
		if (LoggerImpl != null)
		{
			string message = AutoFormat(format, args);
			LoggerImpl.LogWarning((object)message);
		}
	}

	private string AutoFormat(string format, object[] args)
	{
		for (int i = 0; i < args.Length; i++)
		{
			object obj = args[i];
			args[i] = ((obj != null) ? obj.ToString() : "null");
		}
		string message = string.Format(format, args);
		if (Prefix != null)
		{
			message = Prefix + message;
		}
		return message;
	}
}
