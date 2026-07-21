using BetterExperience.CustomScene.Poser;
using BetterExperience.Wrappers.Characters;

namespace BetterExperience.CustomScene.Characters;

internal class AnimatedPlayerState : AnimatedSystem
{
	private readonly PlayerStateData EMPTY_DATA = new PlayerStateData();

	private PlayerCharacter player;

	public AnimatedPlayerState(ExtensibleAnimator.PrivateAnimatorState state, PlayerCharacter playerCharacter)
	{
		SetState(state);
		player = playerCharacter;
	}

	public override void Apply(ExtensibleAnimator.AnimationClipState __state, float dt)
	{
		ExtensibleAnimator.AnimationClipState state = base.layers[0].TargetState;
		if (state == __state)
		{
			PlayerStateData data = state?.Clip?.PlayerState;
			if (data == null)
			{
				data = EMPTY_DATA;
			}
			if (data.HipY.Enabled)
			{
				player.PelvisY = data.HipY.Value;
			}
			if (data.HipZ.Enabled)
			{
				player.PelvisZ = data.HipZ.Value;
			}
		}
	}

	public override void Initialize(ExtensibleAnimator.AnimationClipState state)
	{
	}

	public override void Update(ExtensibleAnimator.AnimationClipState state, float dt)
	{
	}
}
