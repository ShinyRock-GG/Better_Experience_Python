using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class DrawContext
{
	public const int FONT_SIZE = 13;

	private List<Action> rollbackList = new List<Action>();

	private Stack<List<Action>> rollbackStack = new Stack<List<Action>>();

	private Rect rect = new Rect(0f, 0f, Screen.width, Screen.height);

	private Rect? visibleRect;

	public bool DrawCursor { get; set; }

	public Rect CanvasRect
	{
		get
		{
			return rect;
		}
		set
		{
			Rect backup = rect;
			rect = value;
			rollbackList.Add(delegate
			{
				rect = backup;
			});
		}
	}

	public Rect? VisibleRect
	{
		get
		{
			if (!visibleRect.HasValue)
			{
				return rect;
			}
			return visibleRect;
		}
		set
		{
			Rect? backup = visibleRect;
			visibleRect = value;
			rollbackList.Add(delegate
			{
				visibleRect = backup;
			});
		}
	}

	public Color Color
	{
		get
		{
			return GUI.color;
		}
		set
		{
			Color backup = GUI.color;
			value.a *= backup.a;
			GUI.color = value;
			rollbackList.Add(delegate
			{
				GUI.color = backup;
			});
		}
	}

	public float Transparency
	{
		get
		{
			return GUI.color.a;
		}
		set
		{
			Color c = Color;
			c.a = value;
			Color = c;
		}
	}

	public bool Native { get; set; }

	public bool NativeCached { get; internal set; }

	public DrawContext(Rect rect)
	{
		this.rect = rect;
		GUI.skin.label.padding.top = 0;
		GUI.skin.label.padding.bottom = 0;
		GUI.skin.label.padding.left = 2;
		GUI.skin.label.padding.right = 2;
		GUI.skin.label.fontSize = 13;
	}

	public void Begin()
	{
		rollbackStack.Push(rollbackList);
		rollbackList = new List<Action>();
	}

	public void Complete()
	{
		rollbackList.Reverse();
		rollbackList.ForEach(delegate(Action x)
		{
			x();
		});
		rollbackList = rollbackStack.Pop();
	}

	public void DrawText(string text)
	{
		if (!Native && IsVisible())
		{
			Rect r = rect;
			r.width += r.x;
			r.height += r.y;
			GUI.Label(r, text);
		}
	}

	public bool IsVisible()
	{
		return VisibleRect.Value.Overlaps(CanvasRect);
	}

	internal void Translate(float x, float y)
	{
		Rect r = CanvasRect;
		r.x += x;
		r.y += y;
		r.width -= x;
		r.height -= y;
		CanvasRect = r;
	}

	internal void Translate(Vector2 dir)
	{
		Translate(dir.x, dir.y);
	}

	public void Shrink(float w, float h)
	{
		Rect r = CanvasRect;
		r.width -= w;
		r.height -= h;
		CanvasRect = r;
	}
}
