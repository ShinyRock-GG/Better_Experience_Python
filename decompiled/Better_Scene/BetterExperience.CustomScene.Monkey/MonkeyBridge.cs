using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Monkey;

internal class MonkeyBridge : PluginService
{
	public override void OnStart()
	{
		base.OnStart();
		if (BetterExperience.Plugin.MonkeyPresent)
		{
			base.Scope.AddService(new MonkeyCompanion());
		}
		else
		{
			logger.Info("Monkey is not available");
		}
	}
}
