using System;
using System.Collections.Generic;

namespace BetterExperience.UI;

public abstract class TreeViewModel<T>
{
	public T Root { get; protected set; }

	public event Action TreeChanged = delegate
	{
	};

	public abstract List<T> GetChildren(T parent);

	public abstract TreeViewItem CreateItem(T item);

	protected virtual void ReloadTree()
	{
		this.TreeChanged();
	}
}
