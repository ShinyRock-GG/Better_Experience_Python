using BepInEx.Configuration;
using BetterExperience.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory;

internal class InteractiveUI : VisualElement
{
	private Label nameLabel;

	public InteractiveUI(ConfigEntry<KeyboardShortcut> useHotkey)
	{
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		base.pickingMode = PickingMode.Ignore;
		base.style.position = Position.Absolute;
		base.style.width = new Length(100f, LengthUnit.Percent);
		VisualElement layout = UIBuilder.VLayout((VisualElement)this);
		layout.style.width = new Length(100f, LengthUnit.Percent);
		nameLabel = UIBuilder.Label(layout, "");
		Style(nameLabel);
		Label hint = UIBuilder.Label(layout, "[" + ((object)useHotkey.Value/*cast due to constrained. prefix*/).ToString() + "] to interact");
		Style(hint);
		useHotkey.SettingChanged += delegate
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			hint.text = "[" + ((object)useHotkey.Value/*cast due to constrained. prefix*/).ToString() + "] to interact";
		};
	}

	private void Style(VisualElement nameLabel)
	{
		nameLabel.style.width = new Length(100f, LengthUnit.Percent);
		nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
		nameLabel.style.color = Color.yellow;
		nameLabel.style.fontSize = 20f;
	}

	internal void SetLabel(string name)
	{
		nameLabel.text = name;
	}
}
