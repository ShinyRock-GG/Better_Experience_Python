using System;
using System.Collections.Generic;

namespace BetterExperience.CustomScene.Characters;

internal abstract class AnimatedSystem
{
	protected ExtensibleAnimator.PrivateAnimatorState state;

	protected ExtensibleAnimator.AnimationLayerState[] layers => state.layers;

	protected ExtensibleAnimator.AnimationLayerState additiveLayer => state.additiveLayer;

	protected ExtensibleAnimator.AnimationLayerState heelsPostureLayer => state.heelsPostureLayer;

	public List<PropertyAnimation> Properties { get; private set; } = new List<PropertyAnimation>();

	protected FloatAnimation CreateFloatPropery(string id, string name, Func<float> getter, Action<float> setter)
	{
		FloatAnimation fa = new FloatAnimation(id, name, getter, setter);
		Properties.Add(fa);
		return fa;
	}

	public abstract void Initialize(ExtensibleAnimator.AnimationClipState state);

	public virtual void BeforeUpdate()
	{
	}

	public abstract void Update(ExtensibleAnimator.AnimationClipState state, float dt);

	public abstract void Apply(ExtensibleAnimator.AnimationClipState state, float dt);

	public void SetState(ExtensibleAnimator.PrivateAnimatorState state)
	{
		this.state = state;
	}

	protected bool IsPrimary(ExtensibleAnimator.AnimationClipState state)
	{
		return layers[0].TargetState == state;
	}
}
