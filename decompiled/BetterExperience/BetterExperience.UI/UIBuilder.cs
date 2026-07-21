using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public static class UIBuilder
{
	public class TableBuilder : IDisposable
	{
		private VisualElement ve;

		private int startIndex;

		private Action<VisualElement> style;

		public TableBuilder(VisualElement ve, Action<VisualElement> style)
		{
			this.ve = ve;
			startIndex = ve.childCount;
			this.style = style;
		}

		public void Dispose()
		{
			if (ve.childCount > startIndex)
			{
				VisualElement row = new VisualElement();
				row.style.flexDirection = FlexDirection.Row;
				while (ve.childCount > startIndex)
				{
					VisualElement e = ve[startIndex];
					ve.Remove(e);
					row.Add(e);
				}
				ve.Add(row);
				if (style != null)
				{
					row.Recursive(style);
				}
			}
		}
	}

	public static Label Label(this VisualElement ve, string text)
	{
		Label l = new Label(text);
		ve.Add(l);
		return l;
	}

	public static VisualElement HLayout(this VisualElement ve)
	{
		VisualElement l = new VisualElement();
		l.style.flexDirection = FlexDirection.Row;
		ve.Add(l);
		return l;
	}

	public static VisualElement VLayout(this VisualElement ve)
	{
		VisualElement l = new VisualElement();
		l.style.flexDirection = FlexDirection.Column;
		ve.Add(l);
		return l;
	}

	public static ScrollView Scroll(this VisualElement ve)
	{
		ScrollView s = new ScrollView();
		ve.Add(s);
		return s;
	}

	public static TextField TextBox(this VisualElement ve, string text = "")
	{
		TextField ts = new TextField();
		ts.value = text;
		ve.Add(ts);
		return ts;
	}

	public static Button Button(this VisualElement ve, string text = "")
	{
		Button b = new Button
		{
			text = text
		};
		ve.Add(b);
		return b;
	}

	public static Toggle Toggle(this VisualElement ve, string text = "", bool check = false)
	{
		Toggle t = new Toggle(text);
		t.value = check;
		ve.Add(t);
		return t;
	}

	public static Slider Slider(this VisualElement ve, float min = 0f, float max = 1f)
	{
		Slider s = new Slider(min, max);
		ve.Add(s);
		return s;
	}

	public static DropdownField Dropdown(this VisualElement ve, IEnumerable<string> model = null)
	{
		DropdownField df = new DropdownField();
		if (model != null)
		{
			List<string> tmp = new List<string>();
			tmp.AddRange(model);
			df.choices = tmp;
			df.SetValueWithoutNotify(tmp[0]);
		}
		ve.Add(df);
		return df;
	}

	public static T AddElement<T>(this VisualElement ve, T e) where T : VisualElement
	{
		ve.Add(e);
		return e;
	}

	public static void LayoutColSizes(this VisualElement ve, params int[] cols)
	{
		foreach (VisualElement row in ve.Children())
		{
			int cc = Math.Min(cols.Length, row.childCount);
			for (int i = 0; i < cc; i++)
			{
				row[i].style.width = cols[i];
			}
		}
	}

	public static VisualElement Show(this VisualElement ve)
	{
		ve.style.display = DisplayStyle.Flex;
		return ve;
	}

	public static VisualElement Hide(this VisualElement ve)
	{
		ve.style.display = DisplayStyle.None;
		return ve;
	}

	public static bool IsVisible(this VisualElement ve)
	{
		return ve.style.display != DisplayStyle.None;
	}

	public static void SetVisible(this VisualElement ve, bool value)
	{
		if (value)
		{
			ve.Show();
		}
		else
		{
			ve.Hide();
		}
	}

	public static void Recursive<T>(this VisualElement ve, Action<T> action) where T : VisualElement
	{
		if (ve is T a)
		{
			action(a);
		}
		foreach (VisualElement e in ve.Children())
		{
			e.Recursive(action);
		}
	}

	public static TableBuilder Row(this VisualElement ve, Action<VisualElement> style = null)
	{
		return new TableBuilder(ve, style);
	}

	public static T StyleAlign<T>(this T ve, Align align) where T : VisualElement
	{
		ve.style.alignSelf = align;
		return ve;
	}

	public static T StyleWidth<T>(this T ve, int width) where T : VisualElement
	{
		ve.style.width = width;
		return ve;
	}

	public static T StyleHeight<T>(this T ve, int height) where T : VisualElement
	{
		ve.style.height = height;
		return ve;
	}

	public static T StyleMargin<T>(this T ve, int margin) where T : VisualElement
	{
		ve.style.marginBottom = margin;
		ve.style.marginTop = margin;
		ve.style.marginLeft = margin;
		ve.style.marginRight = margin;
		return ve;
	}

	public static T StyleMargin<T>(this T ve, float top, float bottom, float left, float right) where T : VisualElement
	{
		ve.style.marginBottom = bottom;
		ve.style.marginTop = top;
		ve.style.marginLeft = left;
		ve.style.marginRight = right;
		return ve;
	}

	public static T StylePadding<T>(this T ve, int padding) where T : VisualElement
	{
		ve.style.paddingBottom = padding;
		ve.style.paddingTop = padding;
		ve.style.paddingLeft = padding;
		ve.style.paddingRight = padding;
		return ve;
	}

	public static T StyleBorder<T>(this T ve, int border) where T : VisualElement
	{
		ve.style.borderBottomWidth = border;
		ve.style.borderTopWidth = border;
		ve.style.borderLeftWidth = border;
		ve.style.borderRightWidth = border;
		return ve;
	}

	public static T StyleFlexGrow<T>(this T ve, float flexGrow = 1f) where T : VisualElement
	{
		ve.style.flexGrow = flexGrow;
		return ve;
	}

	public static VisualElement FindRoot(this VisualElement ve)
	{
		while (ve.parent != null && !(ve is PopupWindow))
		{
			ve = ve.parent;
		}
		return ve;
	}

	public static T WithTooltip<T>(this T ve, string text) where T : VisualElement
	{
		ve.tooltip = text;
		ve.AddManipulator(new TooltipSupport(null));
		return ve;
	}

	public static WindowDragManipulator EnableWindowDrag(this PopupWindow wnd)
	{
		WindowDragManipulator m = new WindowDragManipulator();
		wnd.AddManipulator(m);
		return m;
	}

	public static WindowDragManipulator EnablePersistedWindowDrag(this PopupWindow wnd, ConfigEntry<Vector2> e, ScopeSupport s)
	{
		WindowDragManipulator m = new WindowDragManipulator();
		wnd.AddManipulator(m);
		float x = wnd.style.left.value.value;
		float y = wnd.style.top.value.value;
		Vector2 old = new Vector2(x, y);
		m.PositionChanged.Add(delegate(Vector2Int newValue)
		{
			Vector2 vector = newValue - old;
			Logger.Global.Info("DV {0}", vector);
			e.Value = vector;
		}, s);
		wnd.style.left = x + e.Value.x;
		wnd.style.top = y + e.Value.y;
		return m;
	}
}
