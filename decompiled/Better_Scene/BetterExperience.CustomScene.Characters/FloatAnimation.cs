using System;

namespace BetterExperience.CustomScene.Characters;

internal class FloatAnimation : PropertyAnimation
{
	protected Func<float> getter;

	protected Action<float> setter;

	public float Value { get; }

	public FloatAnimation(string id, string name, Func<float> getter, Action<float> setter)
		: base(id, name)
	{
		base.PropType = AnimPropertyType.Float;
		this.getter = getter;
		this.setter = setter;
	}
}
