using System.Collections.Generic;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene;

internal class SceneWarnings
{
	public class Warning
	{
		public string Format { get; }

		public string Message { get; }

		public int Count { get; set; } = 1;

		public Warning(string format, string message)
		{
			Format = format;
			Message = message;
		}
	}

	private static readonly SceneWarnings instance;

	private Dictionary<string, Warning> warnings = new Dictionary<string, Warning>();

	public static SceneWarnings Instance => instance;

	public Observable OnNewWarning { get; } = new Observable();

	static SceneWarnings()
	{
		instance = new SceneWarnings();
	}

	public void Report(string format, params object[] args)
	{
		string message = string.Format(format, args);
		if (warnings.TryGetValue(format, out var w))
		{
			w.Count++;
		}
		else
		{
			warnings[format] = new Warning(format, message);
		}
		OnNewWarning.Invoke();
	}

	public List<Warning> CollectWarnings()
	{
		List<Warning> w = new List<Warning>(warnings.Values);
		warnings.Clear();
		return w;
	}
}
