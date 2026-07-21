using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class TeleportOp : BasicOperation
{
	public PointOfInterest Target { get; }

	public PoseOrientation Orientation { get; }

	public TeleportOp(PointOfInterest target, PoseOrientation orientation)
	{
		Target = target;
		Orientation = orientation;
	}

	public override void Run(InteractionContext context)
	{
		context.InteractionManager.GoTo(Target, Orientation);
	}
}
