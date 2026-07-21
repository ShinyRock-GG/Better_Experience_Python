using System;

namespace BetterExperience.GameScopes;

public class PluginService
{
	protected Logger logger = new Logger();

	public ScopeSupport Scope { get; } = new ScopeSupport();

	public virtual bool Enabled { get; set; } = true;

	public PluginService()
	{
		Scope.Name = GetType().Name;
		logger.Prefix = "[" + GetType().Name + "]:";
		Scope.OnDispose += OnStop;
		Scope.OnStart += delegate
		{
			if (Enabled)
			{
				logger.Debug("Starting service");
				OnStart();
			}
			else
			{
				logger.Debug("Service disabled");
			}
		};
		Scope.OnInit += OnInit;
	}

	public virtual void OnInit()
	{
	}

	public virtual void OnStart()
	{
	}

	public virtual void OnStop()
	{
	}

	protected T Lookup<T>() where T : class
	{
		return Scope.Lookup<T>();
	}

	protected T TryLookup<T>() where T : class
	{
		try
		{
			return Scope.Lookup<T>();
		}
		catch (Exception)
		{
			return null;
		}
	}
}
