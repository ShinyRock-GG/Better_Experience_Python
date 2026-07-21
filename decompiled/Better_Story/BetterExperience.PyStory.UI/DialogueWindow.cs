using System.Collections.Generic;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory.UI;

internal class DialogueWindow
{
	private class HighlightOnHover : Manipulator
	{
		protected override void RegisterCallbacksOnTarget()
		{
			base.target.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
			base.target.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
		}

		protected override void UnregisterCallbacksFromTarget()
		{
			base.target.UnregisterCallback<MouseEnterEvent>(OnMouseEnter);
			base.target.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
		}

		private void OnMouseEnter(MouseEnterEvent evt)
		{
			base.target.style.unityFontStyleAndWeight = FontStyle.Bold;
		}

		private void OnMouseLeave(MouseLeaveEvent evt)
		{
			base.target.style.unityFontStyleAndWeight = FontStyle.Normal;
		}
	}

	private Label continueBtn;

	private Label who;

	private Label what;

	private VisualElement answers;

	private IReadOnlyList<DialogueResponse> responses;

	private bool waitForResponse;

	public VisualElement Root { get; private set; }

	public Observable<string> OnRespond { get; } = new Observable<string>();

	public Observable OnContinue { get; } = new Observable();

	public Observable OnSkipPronounce { get; } = new Observable();

	public bool ShowingSubtitle => what.text != "" && responses.Count == 0;

	public DialogueWindow()
	{
		VisualElement layout = new VisualElement();
		layout.pickingMode = PickingMode.Position;
		VisualElement textbar = new VisualElement();
		layout.Add(textbar);
		textbar.style.position = Position.Absolute;
		textbar.style.bottom = 0f;
		textbar.style.width = new Length(100f, LengthUnit.Percent);
		textbar.style.paddingBottom = 30f;
		textbar.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
		ScrollView ansscroll = UIBuilder.Scroll(textbar);
		UIBuilder.StyleMargin<ScrollView>(ansscroll, 10);
		VisualElement data = UIBuilder.VLayout((VisualElement)ansscroll);
		layout.RegisterCallback<ClickEvent>(OnPanelClick);
		VisualElement toprow = UIBuilder.HLayout(data);
		who = UIBuilder.Label(toprow, "");
		continueBtn = UIBuilder.Label(toprow, "Mouse click to continue");
		continueBtn.style.display = DisplayStyle.None;
		continueBtn.style.color = Color.white;
		continueBtn.style.unityFontStyleAndWeight = FontStyle.Italic;
		continueBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
		continueBtn.style.flexGrow = 1f;
		what = UIBuilder.Label(data, "");
		who.style.color = Color.white;
		what.style.color = Color.white;
		answers = UIBuilder.VLayout(data);
		answers.style.paddingLeft = 10f;
		Root = layout;
		layout.style.position = Position.Absolute;
		layout.style.width = new Length(100f, LengthUnit.Percent);
		layout.style.height = new Length(100f, LengthUnit.Percent);
		who.style.unityFontStyleAndWeight = FontStyle.Bold;
	}

	internal void ReactSkipKey()
	{
		OnPanelClick(null);
	}

	internal void ClearSubtitle()
	{
		who.text = "";
		what.text = "";
	}

	private void OnPanelClick(ClickEvent evt)
	{
		if (UIBuilder.IsVisible((VisualElement)continueBtn))
		{
			UIBuilder.Hide((VisualElement)continueBtn);
			OnContinue.Invoke();
		}
		else
		{
			OnSkipPronounce.Invoke();
		}
	}

	internal void SetResponses(IReadOnlyList<DialogueResponse> responses)
	{
		this.responses = responses;
		answers.Clear();
		responses.ForEach(AddAnswer);
	}

	internal void SetRequestContinuation()
	{
		UIBuilder.SetVisible((VisualElement)continueBtn, true);
	}

	internal void SetSubtitle(string who, string what)
	{
		this.who.text = who;
		this.what.text = what;
		UIBuilder.SetVisible((VisualElement)continueBtn, false);
	}

	private void AddAnswer(DialogueResponse v)
	{
		Label lbl = UIBuilder.Label(answers, "");
		lbl.style.color = Color.white;
		lbl.text = answers.childCount + ". " + v.Label;
		lbl.RegisterCallback<ClickEvent>(delegate
		{
			OnResponseClick(v);
		});
		lbl.AddManipulator(new HighlightOnHover());
		waitForResponse = true;
	}

	private void OnResponseClick(DialogueResponse v)
	{
		waitForResponse = false;
		OnRespond.Invoke(v.Key);
	}

	public bool HasResponseAt(int index)
	{
		if (waitForResponse && responses.Count > index && index >= 0)
		{
			return true;
		}
		return false;
	}

	public void Respond(int index)
	{
		if (HasResponseAt(index))
		{
			OnResponseClick(responses[index]);
		}
	}
}
