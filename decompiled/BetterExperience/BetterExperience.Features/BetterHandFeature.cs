using BepInEx.Configuration;
using BetterExperience.Features.BetterHand;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;

namespace BetterExperience.Features;

internal class BetterHandFeature : PluginFeature
{
	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<bool> enableSurfaceAssistance;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableBetterHand", false, "BetterHand: Enable feature");
		enableSurfaceAssistance = config.Bind<bool>("BetterHand", "EnableSurfaceAssistance", false, "BetterHand: Enable hand auto-depth");
	}

	public override void OnInit()
	{
		PluginOptionsService options = Lookup<PluginOptionsService>();
		options.Expose(enableFeature, base.Scope);
		options.Expose(enableSurfaceAssistance, base.Scope);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().InterviewServices.Add(() => new HandPhysicsService());
		Lookup<SessionTracker>().InterviewServices.Add(() => new SmartHandService());
		Lookup<SessionTracker>().InterviewServices.Add(() => new HandGrabService());
		Lookup<SessionTracker>().InterviewServices.Add(() => new SurfaceAssistanceService
		{
			Enabled = enableSurfaceAssistance.Value
		});
	}
}
