using System;
using System.Collections.Generic;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class DrawableTabPane : DrawableContainer<DrawableTab>
{
	private class DrawableTabButton : DrawableContainer<Drawable>
	{
		private DrawableLabel label = new DrawableLabel("");

		private DrawableButton button = new DrawableButton();

		public bool Selected
		{
			get
			{
				return label.Visible;
			}
			set
			{
				label.Visible = value;
				button.Visible = !value;
			}
		}

		public string Text
		{
			get
			{
				return label.Text;
			}
			set
			{
				label.Text = value;
				button.Text = value;
			}
		}

		public event Action<DrawableTabButton> OnClick = delegate
		{
		};

		public DrawableTabButton()
		{
			Add(label).Visible = false;
			Add(button).Visible = false;
			Selected = false;
			button.OnClick += delegate
			{
				this.OnClick(this);
			};
		}
	}

	private VLayout<Drawable> root = new VLayout<Drawable>();

	private HLayout<DrawableTabButton> tabHeader = new HLayout<DrawableTabButton>();

	private List<Tuple<DrawableTabButton, DrawableTab>> tabs = new List<Tuple<DrawableTabButton, DrawableTab>>();

	private DockingContainer contentPane = new DockingContainer(Vector2Int.up + Vector2Int.left);

	public bool DynamicSize { get; set; } = true;

	public DrawableTabPane()
	{
		contentPane.PreferredSize = null;
		root.Add(tabHeader);
		root.Add(contentPane);
	}

	public DrawableTab AddTab(string title, Drawable drawable, ScopeSupport scope)
	{
		DrawableTab tab = new DrawableTab();
		tab.Title = title;
		tab.Add(drawable);
		return Add(tab, scope);
	}

	public override K Add<K>(K child, ScopeSupport scope = null)
	{
		DrawableTabButton btn = tabHeader.Add(new DrawableTabButton
		{
			Text = child.Title
		});
		btn.OnClick += OnTabSelected;
		btn.Selected = base.Children.Count == 0;
		contentPane.Add(child).Visible = base.Children.Count == 0;
		tabs.Add(new Tuple<DrawableTabButton, DrawableTab>(btn, child));
		return base.Add(child, scope);
	}

	public override void Remove(DrawableTab child)
	{
		base.Remove(child);
		int tabIdx = tabs.FindIndex((Tuple<DrawableTabButton, DrawableTab> x) => x.Item2 == child);
		if (tabIdx > -1)
		{
			Tuple<DrawableTabButton, DrawableTab> tab = tabs[tabIdx];
			tabs.RemoveAt(tabIdx);
			contentPane.Remove(tab.Item2);
			tabHeader.Remove(tab.Item1);
			tab.Item1.OnClick -= OnTabSelected;
		}
	}

	private void OnTabSelected(DrawableTabButton obj)
	{
		foreach (Tuple<DrawableTabButton, DrawableTab> tab in tabs)
		{
			if (tab.Item1 == obj)
			{
				tab.Item1.Selected = true;
				tab.Item2.Visible = true;
				tab.Item2.Transient = false;
			}
			else
			{
				tab.Item1.Selected = false;
				tab.Item2.Visible = false;
				tab.Item2.Transient = true;
			}
		}
	}

	public override void Fit()
	{
		if (!DynamicSize && base.PreferredSize.HasValue)
		{
			tabHeader.Fit();
			Vector2 hdrSize = tabHeader.Size;
			foreach (Tuple<DrawableTabButton, DrawableTab> tab in tabs)
			{
				tab.Item2.PreferredSize = base.PreferredSize - new Vector2(15f, hdrSize.y + 25f);
			}
			root.Fit();
		}
		root.Fit();
		if (DynamicSize)
		{
			base.PreferredSize = root.Size;
		}
		base.Dirty = false;
	}

	protected override void OnDraw(DrawContext context)
	{
		root.Draw(context);
	}
}
