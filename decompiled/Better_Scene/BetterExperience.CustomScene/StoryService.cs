using System;
using System.Collections.Generic;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene;

public class StoryService : SessionService
{
	public Story Story { get; private set; }

	public List<AsyncTask> AsyncHandles { get; } = new List<AsyncTask>();

	public override void OnInit()
	{
		base.OnInit();
		Story = Lookup<StoryManager>().Current;
		if (Story == null)
		{
			throw new Exception("Unable to resolve story");
		}
	}
}
