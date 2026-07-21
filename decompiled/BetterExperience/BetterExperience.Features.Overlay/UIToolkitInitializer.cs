using BetterExperience.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class UIToolkitInitializer
{
	private UIDocument uiDocument;

	private bool visibility = true;

	public UIDocument InitializeUIToolkit()
	{
		AssetBundle asset = AssetBundle.LoadFromMemory(BetterExperience.Properties.Resources.uitoolkit);
		GameObject uie = asset.LoadAsset<GameObject>("UIElements");
		GameObject instance = Object.Instantiate(uie);
		instance.hideFlags |= HideFlags.HideAndDontSave;
		Object.DontDestroyOnLoad(instance);
		UIDocument doc = instance.GetComponent<UIDocument>();
		doc.panelSettings.sortingOrder = -100f;
		doc.rootVisualElement.pickingMode = PickingMode.Ignore;
		asset.Unload(unloadAllLoadedObjects: false);
		uiDocument = doc;
		return doc;
	}

	public void FixBattlehub()
	{
		PanelRaycaster[] array = Object.FindObjectsOfType<PanelRaycaster>();
		foreach (PanelRaycaster uiToolkitRaycaster in array)
		{
			Canvas c = uiToolkitRaycaster.gameObject.AddComponent<Canvas>();
			if (c != null)
			{
				c.enabled = false;
			}
		}
	}

	internal void SetVisible(bool value)
	{
		if (visibility != value)
		{
			visibility = value;
			if (value)
			{
				uiDocument.rootVisualElement.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
			}
			else
			{
				uiDocument.rootVisualElement.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
			}
		}
	}

	internal void Dispose()
	{
		if (uiDocument != null)
		{
			Object.Destroy(uiDocument.gameObject);
		}
	}
}
