using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableLabel : Drawable
{
	private Label label = new Label();

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
				label.text = _text;
			}
		}
	}

	public DrawableLabel(string text)
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
			context.DrawText(Text);
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
				base.Size = GUI.skin.label.CalcSize(new GUIContent(Text));
			}
			else
			{
				base.Size = GUI.skin.label.CalcSize(new GUIContent(""));
			}
			base.Dirty = false;
		}
	}
}
