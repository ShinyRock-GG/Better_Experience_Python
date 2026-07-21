using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class GotoOp : TeleportOp
{
	public GotoOp(PointOfInterest target, PoseOrientation orientation)
		: base(target, orientation)
	{
	}
}
