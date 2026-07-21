using BepInEx.Configuration;

namespace BetterExperience.GameScopes;

public abstract class PluginFeature : PluginService
{
	public abstract override bool Enabled { get; }

	public virtual void Configure(ConfigFile config)
	{
	}

	public PluginFeature()
	{
		base.Scope.OnInit -= OnInit;
		base.Scope.OnInit += OnConfig;
		base.Scope.OnInit += OnInit;
	}

	private void OnConfig()
	{
		Configure(base.Scope.Lookup<ConfigFile>());
	}
}
