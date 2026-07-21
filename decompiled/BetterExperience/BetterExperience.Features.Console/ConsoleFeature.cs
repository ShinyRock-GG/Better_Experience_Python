using BepInEx.Configuration;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.Console;

internal class ConsoleFeature : PluginFeature
{
	private ConfigEntry<bool> enableConsole;

	public override bool Enabled => enableConsole.Value;

	public override void Configure(ConfigFile config)
	{
		enableConsole = config.Bind<bool>("Features", "Console", true, (ConfigDescription)null);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().SessionServices.Add(() => new ConsoleService());
	}
}
