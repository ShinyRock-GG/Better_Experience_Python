using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class CheckBox : Toggle
{
	private Label labelComponent = new Label();

	public string Label
	{
		get
		{
			return labelComponent.text;
		}
		set
		{
			labelComponent.text = value;
		}
	}

	public CheckBox(string text, bool check = false)
	{
		labelComponent = new Label(text);
		Add(labelComponent);
		UIBuilder.StyleAlign<Label>(labelComponent, Align.Center);
		value = check;
	}
}
