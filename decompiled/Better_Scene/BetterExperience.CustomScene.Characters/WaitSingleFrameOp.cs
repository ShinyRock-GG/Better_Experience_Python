using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class WaitSingleFrameOp : BasicOperation
{
	private int frame;

	public override void Run(InteractionContext context)
	{
		frame = Time.frameCount;
	}

	public override bool IsComplete(InteractionContext context)
	{
		if (base.IsComplete(context))
		{
			return frame < Time.frameCount;
		}
		return false;
	}
}
