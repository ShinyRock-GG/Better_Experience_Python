using System.Collections.Generic;

namespace BetterExperience.CustomScene.Characters;

public abstract class BasicOperation
{
	public HashSet<InteractionPreprocessor> Preprocessors { get; } = new HashSet<InteractionPreprocessor>();

	public abstract void Run(InteractionContext context);

	public virtual bool IsComplete(InteractionContext context)
	{
		return true;
	}

	public virtual void Update(InteractionContext context, float dt)
	{
	}

	public override string ToString()
	{
		return GetType().Name;
	}
}
