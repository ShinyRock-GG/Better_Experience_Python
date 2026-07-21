using System;
using System.Collections.Generic;
using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class DrawableContainer<T> : Drawable where T : Drawable
{
	private List<T> _children = new List<T>();

	private Rect _padding;

	public IReadOnlyList<T> Children { get; }

	public bool PrependMode { get; set; }

	public Rect Padding
	{
		get
		{
			return _padding;
		}
		set
		{
			if (_padding != value)
			{
				_padding = value;
				base.Dirty = true;
			}
		}
	}

	public Vector2 ClientSize
	{
		get
		{
			Vector2 r = base.Size;
			r.x -= Padding.x + Padding.width;
			r.y -= Padding.y + Padding.height;
			return r;
		}
	}

	protected DrawableContainer()
	{
		Children = _children.AsReadOnly();
		base.NativeComponent = new VisualElement();
		base.NativeComponent.pickingMode = PickingMode.Ignore;
	}

	protected override void OnDraw(DrawContext context)
	{
		context.Translate(Padding.x, Padding.y);
		context.Shrink(Padding.width, Padding.height);
		foreach (T child in Children)
		{
			if (child.Transient)
			{
				continue;
			}
			context.Begin();
			try
			{
				if (BeforeChildDraw(context, child))
				{
					child.Draw(context);
				}
			}
			finally
			{
				context.Complete();
			}
		}
	}

	protected virtual bool BeforeChildDraw(DrawContext context, Drawable child)
	{
		return true;
	}

	public override void Fit()
	{
		Vector2 size = default(Vector2);
		foreach (T row in Children)
		{
			if (!row.Transient)
			{
				row.Fit();
				size.x = Math.Max(size.x, row.Position.x + row.Size.x);
				size.y = Math.Max(size.y, row.Position.y + row.Size.y);
			}
		}
		base.Size = size;
		base.Fit();
	}

	public virtual K Add<K>(K child, ScopeSupport scope = null) where K : T
	{
		if (_children.Contains((T)child))
		{
			return child;
		}
		if (!PrependMode)
		{
			_children.Add((T)child);
		}
		else
		{
			_children.Insert(0, (T)child);
		}
		if (child.NativeComponent != null && base.NativeComponent != null)
		{
			base.NativeComponent.Add(child.NativeComponent);
		}
		else
		{
			new Logger().Error("Child without native component {0} {1}", GetType().Name, child.GetType().Name);
		}
		child.OnDamage += Child_OnDamage;
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				Remove((T)child);
			};
		}
		base.Dirty = true;
		return child;
	}

	private void Child_OnDamage(Drawable obj)
	{
		base.Dirty = true;
	}

	public virtual void Remove(T child)
	{
		if (!_children.Remove(child))
		{
			return;
		}
		child.OnDamage -= Child_OnDamage;
		base.Dirty = true;
		if (child.NativeComponent != null && base.NativeComponent != null)
		{
			if (base.NativeComponent.Contains(child.NativeComponent))
			{
				base.NativeComponent.Remove(child.NativeComponent);
				return;
			}
			new Logger().Error("Class {0} has {1} that is not child", GetType().Name, child.GetType().Name);
		}
	}
}
