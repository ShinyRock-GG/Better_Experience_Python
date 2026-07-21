using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class UITKManagedPanel
{
	public int width { get; }

	public int height { get; }

	public VisualElement MainMenu { get; } = new VisualElement();

	public VisualElement OptionsMenu { get; } = new VisualElement();

	public VisualElement GameView { get; } = new VisualElement();

	public VisualElement Loader { get; } = new VisualElement();

	public VisualElement AlwaysOnScreen { get; } = new VisualElement();

	public PanelSettings panelSettings { get; set; }

	public UITKManagedPanel(int width, int height)
	{
		this.width = width;
		this.height = height;
		FillParent(MainMenu);
		FillParent(OptionsMenu);
		FillParent(GameView);
		FillParent(Loader);
		FillParent(AlwaysOnScreen);
	}

	private void FillParent(VisualElement ve)
	{
		ve.style.position = Position.Absolute;
		ve.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
		ve.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
		ve.pickingMode = PickingMode.Ignore;
		ve.style.display = DisplayStyle.None;
	}
}
