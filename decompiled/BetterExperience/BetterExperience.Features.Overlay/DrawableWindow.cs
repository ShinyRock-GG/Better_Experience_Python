using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class DrawableWindow : DrawableContainer<Drawable>, IGuiContainer<Drawable>
{
	private static int window_id_counter = 99999;

	private int windowId = window_id_counter++;

	public bool Modal { get; set; }

	public string Text { get; set; } = "Window";

	public bool CanDrag { get; set; } = true;

	public DrawableWindow(int width, int height)
	{
		base.PreferredSize = new Vector2(width, height);
		base.Padding = new Rect(10f, 20f, 10f, 10f);
	}

	protected override void OnDraw(DrawContext context)
	{
		context.DrawCursor = true;
		Rect newRect = GUI.Window(windowId, context.CanvasRect, Render, Text);
		float dx = newRect.x - context.CanvasRect.x;
		float dy = newRect.y - context.CanvasRect.y;
		base.Position = new Vector2(base.Position.x + dx, base.Position.y + dy);
	}

	private void Render(int id)
	{
		DrawContext ctx = new DrawContext(new Rect(0f, 0f, base.Size.x, base.Size.y));
		ctx.CanvasRect = new Rect(0f, 0f, base.Size.x, base.Size.y);
		ctx.Begin();
		base.OnDraw(ctx);
		ctx.Complete();
		if (CanDrag)
		{
			GUI.DragWindow(new Rect(0f, 0f, Screen.width, 20f));
		}
	}

	public K Add<K>(K value) where K : Drawable
	{
		return base.Add(value);
	}
}
