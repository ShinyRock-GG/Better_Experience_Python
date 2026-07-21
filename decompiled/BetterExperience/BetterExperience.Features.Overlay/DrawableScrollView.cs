using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class DrawableScrollView : DrawableContainer<Drawable>, IGuiContainer<Drawable>
{
	private Vector2 scrollPos;

	private float scrolly;

	public bool Autoscroll { get; set; } = true;

	protected override void OnDraw(DrawContext context)
	{
		Rect canvasRect = new Rect(Vector2.zero, base.Children[0].Size);
		float scrollableY = canvasRect.height - context.CanvasRect.height;
		if (Autoscroll && scrolly >= 1f)
		{
			scrollPos.y = scrollableY * scrolly;
		}
		scrollPos = GUI.BeginScrollView(context.CanvasRect, scrollPos, canvasRect);
		context.VisibleRect = new Rect(scrollPos, base.Size);
		if (scrollableY <= 0f)
		{
			scrolly = 1f;
		}
		else
		{
			scrolly = scrollPos.y / scrollableY;
		}
		context.CanvasRect = canvasRect;
		base.OnDraw(context);
		GUI.EndScrollView();
	}
}
