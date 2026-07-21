using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class VLayout<T> : ShiftingLayout<T>, IGuiContainer<T> where T : Drawable
{
	public VLayout()
		: base(Vector2.up)
	{
		base.NativeComponent.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Column);
	}
}
