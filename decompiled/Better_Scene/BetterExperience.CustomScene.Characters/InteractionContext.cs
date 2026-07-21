using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class InteractionContext
{
	public PoseAnimationController AnimationController { get; protected set; }

	public InteractionManager InteractionManager { get; protected set; }
}
