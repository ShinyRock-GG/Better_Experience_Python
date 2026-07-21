using System.Collections.Generic;
using BetterExperience.Utils;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class TreeView<T> : VisualElement where T : class
{
	private Dictionary<T, TreeViewItem> items = new Dictionary<T, TreeViewItem>();

	private TreeViewItem rootItem = new TreeViewItem();

	public TreeViewModel<T> Model { get; }

	public TreeView(TreeViewModel<T> model)
	{
		Model = model;
		Add(rootItem);
		rootItem.Container.style.paddingLeft = 0f;
		model.TreeChanged += Model_TreeChanged;
		Model_TreeChanged();
	}

	private void Model_TreeChanged()
	{
		HashSet<T> expandedItems = new HashSet<T>();
		foreach (KeyValuePair<T, TreeViewItem> kv in items)
		{
			if (!kv.Value.Collapsed)
			{
				expandedItems.Add(kv.Key);
			}
		}
		items.Clear();
		T root = Model.Root;
		RefreshNode(null, root);
		foreach (KeyValuePair<T, TreeViewItem> kv2 in items)
		{
			if (expandedItems.Contains(kv2.Key))
			{
				kv2.Value.Collapsed = false;
			}
		}
	}

	private void RefreshNode(TreeViewItem parent, T item)
	{
		TreeViewItem viewItem = ((item != Model.Root) ? items.GetValueOrAdd(item, delegate
		{
			TreeViewItem treeViewItem = Model.CreateItem(item);
			parent.Container.Add(treeViewItem);
			treeViewItem.Collapsed = true;
			return treeViewItem;
		}) : rootItem);
		viewItem.Container.Clear();
		foreach (T c in Model.GetChildren(item))
		{
			if (c != null)
			{
				RefreshNode(viewItem, c);
			}
		}
		viewItem.SetToggleEnabled(viewItem.Container.childCount > 0 && item != Model.Root);
	}
}
