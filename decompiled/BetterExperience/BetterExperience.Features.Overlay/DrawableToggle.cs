using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableToggle : Drawable
{
	private Toggle label = new Toggle();

	private string _text;

	private bool _value;

	public string Text
	{
		get
		{
			return _text;
		}
		set
		{
			if (_text != value)
			{
				_text = value;
				base.Dirty = true;
				label.text = _text;
			}
		}
	}

	public bool Value
	{
		get
		{
			return _value;
		}
		set
		{
			if (_value != value)
			{
				_value = value;
				this.OnValueChanged();
			}
		}
	}

	public event Action OnValueChanged = delegate
	{
	};

	public DrawableToggle(string text)
	{
		Text = text;
		label.style.color = new StyleColor(Color.white);
		label.style.fontSize = new StyleLength(13f);
		label.style.marginTop = new StyleLength(0f);
		label.style.marginBottom = new StyleLength(0f);
		label.style.marginRight = new StyleLength(0f);
		label.style.marginLeft = new StyleLength(0f);
		label.style.paddingBottom = new StyleLength(0f);
		label.style.paddingTop = new StyleLength(0f);
		label.style.paddingLeft = new StyleLength(0f);
		label.style.paddingRight = new StyleLength(0f);
		base.NativeComponent = label;
		base.NativeComponent.pickingMode = PickingMode.Ignore;
	}

	protected override void OnDraw(DrawContext context)
	{
		if (Text != null)
		{
			if (!context.Native && context.IsVisible())
			{
				Value = GUI.Toggle(context.CanvasRect, Value, Text);
				return;
			}
			label.style.width = new StyleLength(base.Size.x);
			label.style.height = new StyleLength(base.Size.y);
		}
	}

	public override string ToString()
	{
		return "<Label> " + Text;
	}

	public override void Fit()
	{
		if (base.Dirty)
		{
			if (Text != null)
			{
				base.Size = GUI.skin.toggle.CalcSize(new GUIContent(Text));
			}
			else
			{
				base.Size = GUI.skin.toggle.CalcSize(new GUIContent(""));
			}
			base.Dirty = false;
		}
	}
}
