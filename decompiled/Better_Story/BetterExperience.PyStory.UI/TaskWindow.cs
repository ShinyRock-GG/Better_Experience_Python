using BetterExperience.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory.UI;

internal class TaskWindow : VisualElement
{
	private Label taskPanel;

	public TaskWindow()
	{
		base.pickingMode = PickingMode.Ignore;
		base.style.width = new Length(100f, LengthUnit.Percent);
		base.style.height = new Length(100f, LengthUnit.Percent);
		base.style.position = Position.Absolute;
		taskPanel = UIBuilder.Label((VisualElement)this, "");
		taskPanel.pickingMode = PickingMode.Ignore;
		taskPanel.style.position = Position.Absolute;
		taskPanel.style.width = new Length(20f, LengthUnit.Percent);
		taskPanel.style.height = new Length(80f, LengthUnit.Percent);
		taskPanel.style.left = new Length(80f, LengthUnit.Percent);
		taskPanel.style.top = new Length(20f, LengthUnit.Percent);
		taskPanel.style.fontSize = 15f;
		taskPanel.style.color = Color.white;
	}
}
