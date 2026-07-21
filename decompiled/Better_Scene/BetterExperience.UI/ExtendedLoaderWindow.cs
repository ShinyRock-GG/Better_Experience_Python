using BetterExperience.Features.Overlay;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class ExtendedLoaderWindow
{
	private UITKManagedPanel uIDoc;

	private PopupWindow wnd;

	private VisualElement layout;

	private Label sceneName;

	private Label operationName;

	private Label operationProgress;

	private VisualElement root;

	private const int width = 800;

	private const int height = 100;

	public ExtendedLoaderWindow(UITKManagedPanel uIDoc)
	{
		this.uIDoc = uIDoc;
		wnd = new PopupWindow();
		wnd.text = "Loading scene";
		wnd.style.top = new Length(50f, LengthUnit.Percent);
		wnd.style.left = new Length(50f, LengthUnit.Percent);
		wnd.style.width = 800f;
		wnd.style.height = 100f;
		wnd.style.marginLeft = -400f;
		wnd.style.marginTop = -50f;
		wnd.style.position = Position.Absolute;
		UIBuilder.SetVisible((VisualElement)wnd, true);
		layout = UIBuilder.VLayout((VisualElement)wnd);
		sceneName = UIBuilder.Label(layout, "");
		VisualElement row = UIBuilder.HLayout(layout);
		operationName = UIBuilder.Label(row, "");
		operationProgress = UIBuilder.Label(row, "");
		root = uIDoc.Loader;
	}

	public void SetVisible(bool value)
	{
		if (value)
		{
			if (wnd.parent == null)
			{
				root.Add(wnd);
			}
			uIDoc.panelSettings.sortingOrder = 100f;
			wnd.BringToFront();
		}
		else
		{
			if (wnd.parent != null)
			{
				root.Remove(wnd);
			}
			uIDoc.panelSettings.sortingOrder = -1f;
		}
	}

	internal void SetTitle(string v)
	{
		sceneName.text = v;
		SetOperation("Initializing...");
	}

	internal void SetOperation(string v)
	{
		operationName.text = v;
		operationProgress.text = "";
	}

	internal void SetProgress(float progress)
	{
		operationProgress.text = Mathf.RoundToInt(progress * 100f) + "%";
	}
}
