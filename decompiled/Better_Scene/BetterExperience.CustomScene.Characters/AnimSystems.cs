using System.Collections.Generic;

namespace BetterExperience.CustomScene.Characters;

internal class AnimSystems
{
	private List<AnimatedSystem> systems = new List<AnimatedSystem>();

	public AnimatedArmature AnimatedArmature { get; private set; }

	public AnimatedFace AnimatedFace { get; private set; }

	public IReadOnlyList<AnimatedSystem> OtherSystems => systems;

	public AnimSystems(AnimatedArmature animatedArmature, AnimatedFace animatedFace, AnimatedPlayerState animatedPlayerState)
	{
		AnimatedArmature = animatedArmature;
		AnimatedFace = animatedFace;
		systems.Add(animatedPlayerState);
	}

	public void Add(AnimatedSystem system)
	{
		systems.Add(system);
	}

	internal void Bind()
	{
		AnimatedArmature.Bind();
	}

	internal void BeforeUpdate()
	{
		AnimatedArmature.BeforeUpdate();
		AnimatedFace.BeforeUpdate();
		foreach (AnimatedSystem s in systems)
		{
			s.BeforeUpdate();
		}
	}

	internal void Apply(ExtensibleAnimator.AnimationClipState primaryState, float dt)
	{
		AnimatedArmature.Apply(primaryState, dt);
		AnimatedFace.Apply(primaryState, dt);
		foreach (AnimatedSystem s in systems)
		{
			s.Apply(primaryState, dt);
		}
	}

	internal void Initialize(ExtensibleAnimator.AnimationClipState state)
	{
		AnimatedArmature.Initialize(state);
		AnimatedFace.Initialize(state);
		foreach (AnimatedSystem s in systems)
		{
			s.Initialize(state);
		}
	}

	internal void Update(ExtensibleAnimator.AnimationClipState state, float dt0)
	{
		AnimatedArmature.Update(state, dt0);
		AnimatedFace.Update(state, dt0);
		foreach (AnimatedSystem s in systems)
		{
			s.Update(state, dt0);
		}
	}

	internal void OnRollover(ExtensibleAnimator.AnimationClipState state)
	{
		AnimatedArmature.OnRollover(state);
	}
}
