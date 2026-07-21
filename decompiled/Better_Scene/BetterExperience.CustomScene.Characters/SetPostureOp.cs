using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class SetPostureOp : BasicOperation
{
	private POIPosture posture;

	public POIPosture TargetPosture => posture;

	public SetPostureOp(POIPosture posture)
	{
		this.posture = posture;
	}

	public override void Run(InteractionContext context)
	{
		context.InteractionManager.SetPosture(posture);
	}

	public override string ToString()
	{
		return base.ToString() + "{ posture=" + posture.Id + "}";
	}
}
