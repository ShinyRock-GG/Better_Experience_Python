using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class TabPanel : VisualElement
{
	private class TabEntry
	{
		public Button TabBtn { get; set; }

		public VisualElement Content { get; set; }

		public bool Selected { get; set; }
	}

	private VisualElement buttons;

	private VisualElement contentPanel;

	private List<TabEntry> tabs = new List<TabEntry>();

	public int selectedIndex = -1;

	public bool CanUnToggle { get; set; } = true;

	public int SelectedIndex => selectedIndex;

	public Color TabBtnSelectedBackground { get; set; } = new Color(0.5f, 0.5f, 0.5f);

	public Color TabBtnNonSelectedBackground { get; set; } = new Color(0.7f, 0.7f, 0.7f);

	public event Action SelectedIndexChanged = delegate
	{
	};

	public TabPanel()
	{
		buttons = new VisualElement();
		Add(buttons);
		contentPanel = new VisualElement();
		Add(contentPanel);
		base.style.flexDirection = FlexDirection.Row;
		buttons.style.width = 100f;
		buttons.style.height = new Length(100f, LengthUnit.Percent);
		contentPanel.style.flexGrow = 1f;
		contentPanel.style.height = new Length(100f, LengthUnit.Percent);
	}

	public void AddTab(string text, VisualElement content)
	{
		TabEntry e = new TabEntry
		{
			TabBtn = new Button
			{
				text = text
			},
			Content = content
		};
		tabs.Add(e);
		e.TabBtn.style.whiteSpace = WhiteSpace.Normal;
		e.TabBtn.style.backgroundColor = TabBtnNonSelectedBackground;
		buttons.Add(e.TabBtn);
		e.TabBtn.clicked += delegate
		{
			ActivateTab(e);
		};
		if (tabs.Count == 1 && !CanUnToggle)
		{
			ActivateTab(e);
		}
	}

	public bool RemoveTab(VisualElement component)
	{
		for (int i = 0; i < tabs.Count; i++)
		{
			if (tabs[i].Content == component)
			{
				RemoveTab(tabs[i]);
				return true;
			}
		}
		return false;
	}

	private void RemoveTab(TabEntry tabEntry)
	{
		tabs.Remove(tabEntry);
		buttons.Remove(tabEntry.TabBtn);
		if (contentPanel.IndexOf(tabEntry.Content) != -1)
		{
			contentPanel.Remove(tabEntry.Content);
			if (tabs.Count > 0)
			{
				ActivateTab(tabs[0]);
			}
		}
	}

	private void ActivateTab(TabEntry e)
	{
		foreach (TabEntry te in tabs)
		{
			if (e == te)
			{
				if (!te.Selected)
				{
					te.Selected = true;
				}
				else if (CanUnToggle)
				{
					te.Selected = false;
				}
			}
			else
			{
				te.Selected = false;
			}
			if (!te.Selected)
			{
				te.TabBtn.SetEnabled(value: true);
				te.TabBtn.style.backgroundColor = TabBtnNonSelectedBackground;
				continue;
			}
			te.TabBtn.style.backgroundColor = TabBtnSelectedBackground;
			if (CanUnToggle)
			{
				te.TabBtn.SetEnabled(value: true);
			}
			else
			{
				te.TabBtn.SetEnabled(value: false);
			}
		}
		contentPanel.Clear();
		if (e.Selected)
		{
			contentPanel.Add(e.Content);
			selectedIndex = tabs.IndexOf(e);
		}
		else
		{
			selectedIndex = -1;
		}
		this.SelectedIndexChanged();
	}
}
