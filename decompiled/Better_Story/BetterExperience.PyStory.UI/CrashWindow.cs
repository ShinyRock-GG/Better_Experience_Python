using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory.UI;

internal class CrashWindow : PopupWindow
{
	private TextField textElement;

	private OverlayService overlayService;

	private ScopeSupport scope;

	public Observable OnRestart { get; } = new Observable();

	public CrashWindow(OverlayService overlayService, ScopeSupport scope)
	{
		this.overlayService = overlayService;
		ScopeSupport localScope = new ScopeSupport();
		scope.AddChild(localScope);
		this.scope = localScope;
		text = "Better_Script:" + Plugin.version + ". PyScript Failure";
		base.style.top = new Length(50f, LengthUnit.Percent);
		base.style.left = new Length(50f, LengthUnit.Percent);
		base.style.width = 1000f;
		base.style.height = 600f;
		base.style.marginLeft = -500f;
		base.style.marginTop = -300f;
		textElement = new TextField();
		textElement.style.flexGrow = 1f;
		textElement.multiline = true;
		textElement.isReadOnly = true;
		VisualElement layout = UIBuilder.VLayout((VisualElement)this);
		ScrollView scroll = UIBuilder.Scroll(layout);
		scroll.style.flexGrow = 1f;
		scroll.Add(textElement);
		VisualElement buttons = UIBuilder.HLayout(layout);
		UIBuilder.Button(buttons, "Close").clicked += Close_clicked;
		UIBuilder.Button(buttons, "Restart").clicked += OnRestart.Invoke;
	}

	public void SetError(string text)
	{
		text = text.Replace("\r", "");
		if (text.Length > 10000)
		{
			text = text.Substring(0, 10000);
		}
		textElement.value = text;
		Logger.Global.Error("error stack length " + text.Length);
	}

	private void Close_clicked()
	{
		SetWindowVisible(v: false);
	}

	internal void SetWindowVisible(bool v)
	{
		if (v)
		{
			UIBuilder.Show((VisualElement)this);
			overlayService.CursorHolders.Add(scope);
		}
		else
		{
			UIBuilder.Hide((VisualElement)this);
			overlayService.CursorHolders.Remove(scope);
		}
	}
}
