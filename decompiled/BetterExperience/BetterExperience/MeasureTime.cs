using System;
using System.Diagnostics;

namespace BetterExperience;

public struct MeasureTime : IDisposable
{
	private Stopwatch sw;

	private Func<long, string> message;

	private Logger logger;

	private bool enabled;

	public static MeasureTime Create(Logger logger, Func<long, string> message, bool enabled = true)
	{
		return new MeasureTime(logger, message, enabled);
	}

	public MeasureTime(Logger logger, Func<long, string> message, bool enabled = true)
	{
		this.message = message;
		this.logger = logger;
		this.enabled = enabled;
		if (enabled)
		{
			sw = Profiler._BorrowStopwatch();
			sw.Restart();
		}
		else
		{
			sw = null;
		}
	}

	public void Dispose()
	{
		if (enabled)
		{
			sw.Stop();
			if ((float)sw.ElapsedMilliseconds > TimedAttribute.THRESHOLD)
			{
				logger.Warn(message(sw.ElapsedMilliseconds));
			}
			Profiler._ReleaseStopwatch(sw);
		}
	}
}
