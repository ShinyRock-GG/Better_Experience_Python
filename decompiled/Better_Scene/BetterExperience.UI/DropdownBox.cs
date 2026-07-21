using System.Collections.Generic;
using BetterExperience.GameScopes;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class DropdownBox : VisualElement
{
	private Button mainBtn;

	private List<string> items = new List<string>();

	private GenericDropdownMenu menu;

	public int SelectedIndex { get; protected set; }

	public string SelectedItem
	{
		get
		{
			if (SelectedIndex == -1)
			{
				return null;
			}
			return items[SelectedIndex];
		}
	}

	public IReadOnlyList<string> Items
	{
		get
		{
			return items;
		}
		set
		{
			items = new List<string>(value);
			SetSelectedIndex(SelectedIndex);
		}
	}

	public Observable<int> SelectedIndexChanged { get; } = new Observable<int>();

	public DropdownBox(IEnumerable<string> model = null)
	{
		mainBtn = UIBuilder.Button((VisualElement)this, "");
		if (model != null)
		{
			items = new List<string>(model);
		}
		else
		{
			items = new List<string>();
		}
		SetSelectedIndex(0);
		mainBtn.clicked += MainBtn_clicked;
	}

	private void MainBtn_clicked()
	{
		ShowDropdown(v: true);
	}

	private void ShowDropdown(bool v)
	{
		if (!v || items.Count <= 0)
		{
			return;
		}
		menu = new GenericDropdownMenu();
		for (int i = 0; i < items.Count; i++)
		{
			int c = i;
			menu.AddItem(items[i], i == SelectedIndex, delegate
			{
				SetSelectedIndex(c);
			});
		}
		menu.DropDown(mainBtn.worldBound, mainBtn);
	}

	public void SetSelectedIndex(int index)
	{
		int p = SelectedIndex;
		if (index >= 0 && index < items.Count)
		{
			SelectedIndex = index;
			mainBtn.text = items[index];
		}
		else
		{
			SelectedIndex = -1;
			mainBtn.text = "";
		}
		if (p != SelectedIndex)
		{
			SelectedIndexChanged.Invoke(SelectedIndex);
		}
	}

	internal void SetValueWithoutNotify(string ks)
	{
		SelectedIndex = Items.IndexOf(ks);
		if (SelectedIndex >= 0)
		{
			mainBtn.text = items[SelectedIndex];
		}
		else
		{
			mainBtn.text = "";
		}
	}
}
