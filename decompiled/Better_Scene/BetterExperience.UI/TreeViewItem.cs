using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class TreeViewItem : VisualElement
{
	private Button toggleBtn;

	public VisualElement Container { get; } = new VisualElement();

	public VisualElement Item { get; } = new VisualElement();

	public bool Collapsed
	{
		get
		{
			return !UIBuilder.IsVisible(Container);
		}
		set
		{
			UIBuilder.SetVisible(Container, !value);
			if (value)
			{
				toggleBtn.text = "+";
			}
			else
			{
				toggleBtn.text = "-";
			}
		}
	}

	public TreeViewItem()
	{
		UIBuilder.StylePadding<TreeViewItem>(UIBuilder.StyleMargin<TreeViewItem>(this, 0), 0);
		VisualElement row = UIBuilder.HLayout((VisualElement)this);
		VisualElement dummy = new VisualElement();
		row.Add(dummy);
		UIBuilder.StylePadding<VisualElement>(UIBuilder.StyleMargin<VisualElement>(UIBuilder.StyleHeight<VisualElement>(UIBuilder.StyleWidth<VisualElement>(dummy, 13), 13), 0), 0);
		toggleBtn = UIBuilder.Button(dummy, "");
		UIBuilder.StylePadding<Button>(UIBuilder.StyleMargin<Button>(UIBuilder.StyleHeight<Button>(UIBuilder.StyleWidth<Button>(toggleBtn, 13), 13), 0), 0);
		toggleBtn.clicked += ToggleBtn_clicked;
		row.Add(Item);
		Add(Container);
		Container.style.paddingLeft = 10f;
		toggleBtn.text = "-";
	}

	public void SetToggleEnabled(bool value)
	{
		if (!value && !UIBuilder.IsVisible(Container))
		{
			ToggleBtn_clicked();
		}
		UIBuilder.SetVisible((VisualElement)toggleBtn, value);
	}

	private void ToggleBtn_clicked()
	{
		Collapsed = !Collapsed;
	}
}
