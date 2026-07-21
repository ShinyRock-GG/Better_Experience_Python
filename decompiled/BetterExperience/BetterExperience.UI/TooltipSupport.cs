using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class TooltipSupport : Manipulator
{
	private VisualElement element;

	private VisualElement root;

	public TooltipSupport(VisualElement root)
	{
		this.root = root;
	}

	protected override void RegisterCallbacksOnTarget()
	{
		base.target.RegisterCallback<MouseEnterEvent>(MouseIn);
		base.target.RegisterCallback<MouseOutEvent>(MouseOut);
	}

	protected override void UnregisterCallbacksFromTarget()
	{
		base.target.UnregisterCallback<MouseEnterEvent>(MouseIn);
		base.target.UnregisterCallback<MouseOutEvent>(MouseOut);
		if (element != null)
		{
			element.parent.Remove(element);
		}
	}

	private void MouseIn(MouseEnterEvent e)
	{
		if (element == null)
		{
			element = new VisualElement();
			element.style.backgroundColor = Color.gray;
			element.style.position = Position.Absolute;
			element.style.left = base.target.worldBound.center.x;
			element.style.top = base.target.worldBound.yMin;
			Label label = new Label(base.target.tooltip);
			label.style.color = Color.white;
			element.Add(label);
			if (root == null)
			{
				root = base.target.FindRoot();
			}
			root.parent.Add(element);
		}
		element.style.visibility = Visibility.Visible;
		element.BringToFront();
	}

	private void MouseOut(MouseOutEvent e)
	{
		element.style.visibility = Visibility.Hidden;
	}
}
