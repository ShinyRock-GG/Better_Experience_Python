using System;

namespace BetterExperience.GameScopes;

public class SessionService : PluginService
{
	public GameSession Session { get; private set; }

	public override void OnInit()
	{
		base.OnInit();
		Session = Lookup<SessionTracker>().Current;
		if (Session == null)
		{
			throw new Exception("Unable to resolve session");
		}
	}
}
