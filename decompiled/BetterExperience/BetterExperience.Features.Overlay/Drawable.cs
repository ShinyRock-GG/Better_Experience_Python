using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public abstract class Drawable
{
	private Vector2 _position;

	private Vector2 _size;

	private Vector2? _preferredSize;

	private float _transparency = 1f;

	private bool _dirty = true;

	private bool _transient;

	private bool _visible = true;

	public bool EnableNative { get; set; }

	public bool OffscreenHint { get; set; }

	public VisualElement NativeComponent { get; protected set; }

	public bool Visible
	{
		get
		{
			return _visible;
		}
		set
		{
			_visible = value;
			Dirty = true;
			UpdateNativeComponentVisibility();
		}
	}

	public bool Transient
	{
		get
		{
			return _transient;
		}
		set
		{
			if (_transient != value)
			{
				_transient = value;
				Dirty = true;
				UpdateNativeComponentVisibility();
			}
		}
	}

	public Vector2 Position
	{
		get
		{
			return _position;
		}
		set
		{
			if (_position != value)
			{
				_position = value;
				Dirty = true;
				if (NativeComponent != null)
				{
					NativeComponent.style.left = new StyleLength(_position.x);
					NativeComponent.style.top = new StyleLength(_position.y);
				}
			}
		}
	}

	public Vector2 Size
	{
		get
		{
			if (PreferredSize.HasValue)
			{
				return PreferredSize.Value;
			}
			return _size;
		}
		set
		{
			if (_size != value)
			{
				_size = value;
				Dirty = true;
			}
		}
	}

	public Vector2? PreferredSize
	{
		get
		{
			if (Transient)
			{
				return Vector2.zero;
			}
			return _preferredSize;
		}
		set
		{
			if (_preferredSize != value)
			{
				_preferredSize = value;
				Dirty = true;
			}
		}
	}

	public float Transparency
	{
		get
		{
			return _transparency;
		}
		set
		{
			if (_transparency != value)
			{
				_transparency = value;
				Dirty = true;
				if (NativeComponent != null)
				{
					NativeComponent.style.opacity = new StyleFloat(value);
				}
			}
		}
	}

	public bool Dirty
	{
		get
		{
			return _dirty;
		}
		set
		{
			if (_dirty != value)
			{
				_dirty = value;
				if (this.OnDamage != null)
				{
					this.OnDamage(this);
				}
			}
		}
	}

	public event Action<Drawable> OnDamage;

	private void UpdateNativeComponentVisibility()
	{
		if (NativeComponent != null)
		{
			NativeComponent.visible = _visible && !_transient;
			if (NativeComponent.visible)
			{
				NativeComponent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
			}
			else
			{
				NativeComponent.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
			}
		}
	}

	public void Draw(DrawContext context)
	{
		if (!Visible || Transient || context.NativeCached)
		{
			return;
		}
		if (Dirty && !Transient)
		{
			Fit();
		}
		context.Begin();
		try
		{
			BeforeDraw(context);
			context.Transparency = Transparency;
			context.Translate(Position);
			Rect rect = context.CanvasRect;
			if (Size != default(Vector2))
			{
				rect.size = Size;
			}
			context.CanvasRect = rect;
			if (context.IsVisible() || !OffscreenHint)
			{
				OnDraw(context);
			}
		}
		finally
		{
			context.Complete();
		}
	}

	protected virtual void BeforeDraw(DrawContext context)
	{
	}

	protected abstract void OnDraw(DrawContext context);

	public virtual void Fit()
	{
		Dirty = false;
	}
}
