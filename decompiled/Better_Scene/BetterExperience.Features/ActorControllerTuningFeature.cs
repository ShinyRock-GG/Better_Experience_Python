using BetterExperience.GameScopes;
using com.ootii.Actors;

namespace BetterExperience.Features;

internal class ActorControllerTuningFeature : PluginService
{
	private class ActorControllerTuningService : SessionService
	{
		public override void OnStart()
		{
			base.OnStart();
			ActorController ac = base.Session.Player.GameObject.GetComponentInChildren<ActorController>();
			ac.MaxStepHeight = 0.2f;
			ac.MaxSlopeAngle = 50f;
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().SessionServices.Add(() => new ActorControllerTuningService());
	}
}
