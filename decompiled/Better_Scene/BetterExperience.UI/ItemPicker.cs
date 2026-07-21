using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class ItemPicker : VisualElement
{
	private CheckListView<string> table = new CheckListView<string>((string x) => x);

	public IReadOnlyList<string> Selection => table.Selection;

	public bool Multiselect { get; private set; } = true;

	public bool CustomInput { get; private set; } = true;

	public ItemPicker(IEnumerable<string> items, bool multiselect = true, bool custominput = true)
	{
		ItemPicker itemPicker = this;
		Multiselect = multiselect;
		CustomInput = custominput;
		base.style.flexDirection = FlexDirection.Column;
		List<string> model = new List<string>(items);
		model.Sort();
		table.SetElements(model);
		table.SingleSelection = !multiselect;
		VisualElement row = UIBuilder.HLayout((VisualElement)this);
		TextField newTextBox = UIBuilder.StyleFlexGrow<TextField>(UIBuilder.TextBox(row, ""), 1f);
		if (custominput)
		{
			UIBuilder.Button(row, "Insert").clicked += delegate
			{
				string text = newTextBox.text;
				newTextBox.value = "";
				if (text != null)
				{
					text = text.Trim();
					if (text.Length != 0 && !model.Contains(text))
					{
						model.Add(text);
						model.Sort();
						IReadOnlyList<string> selection = itemPicker.table.Selection;
						itemPicker.table.SetElements(model);
						itemPicker.table.SetSelection(selection);
					}
				}
			};
		}
		UIBuilder.StyleFlexGrow<CheckListView<string>>(table, 1f);
		Add(table);
		newTextBox.RegisterValueChangedCallback(OnTextChanged);
	}

	private void OnTextChanged(ChangeEvent<string> evt)
	{
		if (evt.newValue == null || evt.newValue.Length == 0)
		{
			table.Filter(null);
			return;
		}
		string v = evt.newValue.ToLower();
		table.Filter((string x) => x.ToLower().Contains(v));
	}

	public void SetSelection(IReadOnlyList<string> items)
	{
		table.SetSelection(items);
	}

	internal static void PickOne(VisualElement GameView, List<string> list, Action<int> p)
	{
		ItemPicker picker = new ItemPicker(list, multiselect: false, custominput: false);
		ComponentPopUp popup = new ComponentPopUp(picker);
		popup.SetCenterScreen(400, 500);
		popup.OnSave.Add(delegate
		{
			if (picker.Selection.Count != 0)
			{
				string item = picker.Selection[0];
				p(list.IndexOf(item));
			}
		});
		popup.ShowModal(GameView);
	}
}
