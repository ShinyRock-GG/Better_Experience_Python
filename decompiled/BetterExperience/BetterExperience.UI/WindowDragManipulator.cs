using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

public class WindowDragManipulator : Manipulator
{
	private Logger logger = Logger.Create<WindowDragManipulator>();

	private bool dragging;

	public Observable<Vector2Int> PositionChanged { get; } = new Observable<Vector2Int>();

	protected override void RegisterCallbacksOnTarget()
	{
		base.target.RegisterCallback<MouseDownEvent>(OnMouseDown);
		base.target.RegisterCallback<MouseUpEvent>(OnMouseUp);
		base.target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
		base.target.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
	}

	protected override void UnregisterCallbacksFromTarget()
	{
		base.target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
		base.target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
		base.target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
		base.target.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
	}

	private void OnMouseDown(MouseDownEvent evt)
	{
		if (evt.button == 0 && evt.localMousePosition.y < 20f)
		{
			dragging = true;
		}
	}

	private void OnMouseUp(MouseUpEvent evt)
	{
		if (evt.button == 0 && dragging)
		{
			dragging = false;
		}
	}

	private void OnMouseMove(MouseMoveEvent evt)
	{
		if (dragging)
		{
			StyleLength styleLength = (base.target.style.left = base.target.resolvedStyle.left + evt.mouseDelta.x);
			StyleLength x = styleLength;
			styleLength = (base.target.style.top = base.target.resolvedStyle.top + evt.mouseDelta.y);
			StyleLength y = styleLength;
			Vector2Int p = new Vector2Int((int)x.value.value, (int)y.value.value);
			PositionChanged.Invoke(p);
		}
	}

	private void OnMouseLeave(MouseLeaveEvent evt)
	{
		if (dragging)
		{
			dragging = false;
		}
	}
}
