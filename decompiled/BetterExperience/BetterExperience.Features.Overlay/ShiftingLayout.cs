using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class ShiftingLayout<T> : DrawableContainer<T> where T : Drawable
{
	private Vector2 offset = Vector2.zero;

	private int index;

	private Vector2 direction;

	public float Spacing { get; set; }

	public float[] ElementSizing { get; private set; } = new float[0];

	public float[] PreferredSizing { get; set; }

	public ShiftingLayout(Vector2 direction)
	{
		this.direction = direction;
	}

	protected override void OnDraw(DrawContext context)
	{
		index = 0;
		offset = Vector2.zero;
		base.OnDraw(context);
	}

	protected override bool BeforeChildDraw(DrawContext context, Drawable child)
	{
		context.Translate(offset);
		if (base.BeforeChildDraw(context, child))
		{
			if (PreferredSizing != null && PreferredSizing.Length > index)
			{
				offset += PreferredSizing[index] * direction + Spacing * direction;
			}
			else
			{
				offset += (child.Position + child.Size) * direction + Spacing * direction;
			}
			index++;
			return true;
		}
		index++;
		return false;
	}

	public void UpdateNativeComponent()
	{
		if (base.NativeComponent == null || PreferredSizing == null)
		{
			return;
		}
		int i = 0;
		foreach (VisualElement c in base.NativeComponent.Children())
		{
			if (i < PreferredSizing.Length)
			{
				c.style.width = PreferredSizing[i++];
			}
		}
	}

	public override void Fit()
	{
		base.Fit();
		Vector2 size = base.Size;
		size -= size * direction;
		if (ElementSizing.Length != base.Children.Count)
		{
			ElementSizing = new float[base.Children.Count];
		}
		int i = 0;
		foreach (T row in base.Children)
		{
			if (!row.Transient)
			{
				if (PreferredSizing != null && PreferredSizing.Length > i)
				{
					size += PreferredSizing[i] * direction + Spacing * direction;
				}
				else
				{
					size += (row.Position + row.Size) * direction + Spacing * direction;
				}
				ElementSizing[i++] = ((row.Position + row.Size) * direction).magnitude;
			}
		}
		base.Size = size;
		base.Dirty = false;
	}
}
