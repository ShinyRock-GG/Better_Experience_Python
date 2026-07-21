using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableButton : Drawable
{
	private Button nativeButton = new Button();

	private string _text;

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
				nativeButton.text = _text;
			}
		}
	}

	public event Action OnClick = delegate
	{
	};

	public DrawableButton()
		: this("")
	{
	}

	public DrawableButton(string text)
	{
		Text = text;
		nativeButton.text = text;
		base.NativeComponent = nativeButton;
		nativeButton.style.marginTop = new StyleLength(0f);
		nativeButton.style.marginBottom = new StyleLength(0f);
		nativeButton.style.marginRight = new StyleLength(1f);
		nativeButton.style.marginLeft = new StyleLength(1f);
		nativeButton.style.paddingBottom = new StyleLength(0f);
		nativeButton.style.paddingTop = new StyleLength(0f);
		nativeButton.style.paddingLeft = new StyleLength(0f);
		nativeButton.style.paddingRight = new StyleLength(0f);
		nativeButton.RegisterCallback<ClickEvent>(OnNativeClick);
	}

	private void OnNativeClick(ClickEvent evt)
	{
		this.OnClick();
	}

	protected override void OnDraw(DrawContext context)
	{
		if (!context.Native && context.IsVisible())
		{
			if (GUI.Button(context.CanvasRect, Text))
			{
				this.OnClick();
			}
		}
		else
		{
			nativeButton.style.width = new StyleLength(base.Size.x);
			nativeButton.style.height = new StyleLength(base.Size.y);
		}
	}

	public override void Fit()
	{
		if (base.Dirty)
		{
			if (Text != null)
			{
				base.Size = GUI.skin.button.CalcSize(new GUIContent(Text));
			}
			else
			{
				base.Size = GUI.skin.button.CalcSize(new GUIContent(""));
			}
			base.Dirty = false;
		}
	}
}
