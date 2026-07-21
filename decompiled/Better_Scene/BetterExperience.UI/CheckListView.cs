using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class CheckListView<T> : VisualElement
{
	public class CheckListRow<T> : VisualElement
	{
		private Toggle toggle;

		public T Item { get; private set; }

		public bool Checked
		{
			get
			{
				return toggle.value;
			}
			set
			{
				toggle.value = value;
			}
		}

		public event Action<bool> ValueChanged = delegate
		{
		};

		public CheckListRow(string text, T model)
		{
			base.style.flexDirection = FlexDirection.Row;
			base.style.flexWrap = Wrap.NoWrap;
			toggle = new Toggle();
			Add(toggle);
			toggle.Add(new Label(text));
			Item = model;
			toggle.RegisterValueChangedCallback(OnValueChanged);
		}

		private void OnValueChanged(ChangeEvent<bool> evt)
		{
			this.ValueChanged(evt.newValue);
		}

		internal void SetCheckedSilently(bool value)
		{
			toggle.SetValueWithoutNotify(value);
		}
	}

	private CheckListRow<T>[] rows = new CheckListRow<T>[0];

	private Func<T, string> mapper;

	public IReadOnlyList<T> Selection { get; private set; } = new T[0];

	public bool SingleSelection { get; set; } = true;

	public IComparer<T> Sorter { get; set; }

	public IReadOnlyList<CheckListRow<T>> Items => rows;

	public event Action SelectionChanged = delegate
	{
	};

	public CheckListView(Func<T, string> mapper)
	{
		base.style.flexDirection = FlexDirection.Column;
		this.mapper = mapper;
	}

	public void SetElements(IReadOnlyList<T> elements)
	{
		while (base.childCount > 0)
		{
			RemoveAt(0);
		}
		rows = new CheckListRow<T>[elements.Count];
		for (int i = 0; i < elements.Count; i++)
		{
			T e = elements[i];
			CheckListRow<T> t = new CheckListRow<T>(mapper(e), e);
			t.ValueChanged += delegate(bool value)
			{
				OnValueChanged(t, value);
			};
			rows[i] = t;
		}
		if (Sorter != null)
		{
			Comparer<CheckListRow<T>> sorter = Comparer<CheckListRow<T>>.Create((CheckListRow<T> a, CheckListRow<T> b) => Sorter.Compare(a.Item, b.Item));
			Array.Sort(rows, sorter);
		}
		CheckListRow<T>[] array = rows;
		foreach (CheckListRow<T> r in array)
		{
			Add(r);
		}
	}

	private void OnValueChanged(CheckListRow<T> sender, bool value)
	{
		List<T> checks = new List<T>();
		CheckListRow<T>[] array = rows;
		foreach (CheckListRow<T> r in array)
		{
			if (SingleSelection && r != sender)
			{
				r.SetCheckedSilently(value: false);
			}
			if (r.Checked)
			{
				checks.Add(r.Item);
			}
		}
		Selection = checks;
		this.SelectionChanged();
	}

	public void SetSelection(IReadOnlyList<T> selection)
	{
		List<T> checks = new List<T>();
		CheckListRow<T>[] array = rows;
		foreach (CheckListRow<T> r in array)
		{
			bool check = selection.Contains(r.Item);
			r.SetCheckedSilently(check);
			if (r.Checked)
			{
				checks.Add(r.Item);
			}
		}
		Selection = checks;
		this.SelectionChanged();
	}

	public void Filter(Predicate<T> filter)
	{
		CheckListRow<T>[] array = rows;
		foreach (CheckListRow<T> r in array)
		{
			UIBuilder.SetVisible((VisualElement)r, filter?.Invoke(r.Item) ?? true);
		}
	}
}
