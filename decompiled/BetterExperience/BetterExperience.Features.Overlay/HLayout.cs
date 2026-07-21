using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class HLayout<T> : ShiftingLayout<T>, IGuiContainer<T> where T : Drawable
{
	public HLayout()
		: base(Vector2.right)
	{
		base.NativeComponent.style.flexDirection = new StyleEnum<FlexDirection>(FlexDirection.Row);
	}
}
