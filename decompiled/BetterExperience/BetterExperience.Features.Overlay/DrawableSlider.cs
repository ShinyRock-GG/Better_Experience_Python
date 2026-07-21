using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableSlider : Drawable
{
	private Slider nativeSlider = new Slider();

	public float Value
	{
		get
		{
			return nativeSlider.value;
		}
		set
		{
			if (nativeSlider.value != value)
			{
				nativeSlider.value = value;
				base.Dirty = true;
				this.OnValueChange();
			}
		}
	}

	public float MinValue
	{
		get
		{
			return nativeSlider.lowValue;
		}
		set
		{
			if (nativeSlider.lowValue != value)
			{
				nativeSlider.lowValue = value;
				base.Dirty = true;
			}
		}
	}

	public float MaxValue
	{
		get
		{
			return nativeSlider.highValue;
		}
		set
		{
			if (nativeSlider.highValue != value)
			{
				nativeSlider.highValue = value;
				base.Dirty = true;
			}
		}
	}

	public event Action OnValueChange = delegate
	{
	};

	public DrawableSlider()
		: this(0.5f, 0f, 1f)
	{
	}

	public DrawableSlider(float value, float min, float max)
	{
		Value = value;
		MinValue = min;
		MaxValue = max;
		base.NativeComponent = nativeSlider;
		nativeSlider.style.marginTop = new StyleLength(0f);
		nativeSlider.style.marginBottom = new StyleLength(0f);
		nativeSlider.style.marginRight = new StyleLength(1f);
		nativeSlider.style.marginLeft = new StyleLength(1f);
		nativeSlider.style.paddingBottom = new StyleLength(0f);
		nativeSlider.style.paddingTop = new StyleLength(0f);
		nativeSlider.style.paddingLeft = new StyleLength(0f);
		nativeSlider.style.paddingRight = new StyleLength(0f);
		nativeSlider.RegisterCallback<ChangeEvent<float>>(OnNativeChange);
	}

	private void OnNativeChange(ChangeEvent<float> evt)
	{
		this.OnValueChange();
	}

	protected override void OnDraw(DrawContext context)
	{
		if (!context.Native && context.IsVisible())
		{
			float newValue = GUI.HorizontalSlider(context.CanvasRect, Value, MinValue, MaxValue);
			if (newValue != Value)
			{
				Value = newValue;
			}
		}
		else
		{
			nativeSlider.style.width = new StyleLength(base.Size.x);
			nativeSlider.style.height = new StyleLength(base.Size.y);
		}
	}

	public override void Fit()
	{
		if (base.Dirty)
		{
			base.Size = GUI.skin.horizontalSlider.CalcSize(new GUIContent(Value.ToString()));
			base.Dirty = false;
		}
	}
}
