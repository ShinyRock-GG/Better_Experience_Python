using System;

namespace BetterExperience.CustomScene.Characters;

public class WaitUntilOp : BasicOperation
{
	public Func<InteractionContext, bool> predicate;

	public WaitUntilOp(Func<InteractionContext, bool> predicate)
	{
		this.predicate = predicate;
	}

	public override void Run(InteractionContext context)
	{
	}

	public override bool IsComplete(InteractionContext context)
	{
		if (base.IsComplete(context))
		{
			return predicate(context);
		}
		return false;
	}
}
