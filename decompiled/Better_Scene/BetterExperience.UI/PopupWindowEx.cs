using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class PopupWindowEx : PopupWindow
{
	public PopupWindowEx()
	{
	}

	public PopupWindowEx(VisualElement child)
	{
		Add(child);
	}

	public void SetCenterScreen(int w, int h)
	{
		base.style.width = w;
		base.style.height = h;
		base.style.position = Position.Absolute;
		base.style.top = new Length(50f, LengthUnit.Percent);
		base.style.left = new Length(50f, LengthUnit.Percent);
		base.style.marginLeft = -w / 2;
		base.style.marginTop = -h / 2;
	}

	public virtual Action ShowModal(VisualElement root)
	{
		VisualElement backdrop = new VisualElement();
		backdrop.style.position = Position.Absolute;
		backdrop.style.width = new Length(100f, LengthUnit.Percent);
		backdrop.style.height = new Length(100f, LengthUnit.Percent);
		backdrop.style.backgroundColor = new Color(0.7f, 0.7f, 0.7f, 0.2f);
		backdrop.Add(this);
		root.Add(backdrop);
		return delegate
		{
			root.Remove(backdrop);
		};
	}

	public static Action ShowModal(VisualElement ve, int w, int h, VisualElement root)
	{
		PopupWindowEx pwe = new PopupWindowEx(ve);
		pwe.SetCenterScreen(w, h);
		return pwe.ShowModal(root);
	}
}
