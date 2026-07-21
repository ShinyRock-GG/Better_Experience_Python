using System;
using BetterExperience.GameScopes;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class ComponentPopUp : PopupWindowEx
{
	private VisualElement container;

	public Observable OnClose { get; } = new Observable();

	public Observable OnSave { get; } = new Observable();

	public ComponentPopUp()
	{
		container = UIBuilder.AddElement<ScrollView>((VisualElement)this, new ScrollView());
		UIBuilder.StyleFlexGrow<VisualElement>(container, 1f);
		VisualElement row = UIBuilder.HLayout((VisualElement)this);
		UIBuilder.Button(row, "Save").clicked += delegate
		{
			OnSave.Invoke();
			OnClose.Invoke();
		};
		UIBuilder.Button(row, "Close").clicked += OnClose.Invoke;
	}

	public ComponentPopUp(VisualElement component)
		: this()
	{
		SetComponent(component);
	}

	public void SetComponent(VisualElement ve)
	{
		UIBuilder.StyleFlexGrow<VisualElement>(ve, 1f);
		container.Add(ve);
	}

	public override Action ShowModal(VisualElement root)
	{
		Action cb = base.ShowModal(root);
		OnClose.Add(cb);
		return cb;
	}
}
