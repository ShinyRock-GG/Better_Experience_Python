using System;

namespace BetterExperience.CustomScene.Characters;

public class LambdaOp : BasicOperation
{
	private Action<InteractionContext> lambda;

	public LambdaOp(Action<InteractionContext> lambda)
	{
		this.lambda = lambda;
	}

	public override void Run(InteractionContext context)
	{
		lambda(context);
	}
}
