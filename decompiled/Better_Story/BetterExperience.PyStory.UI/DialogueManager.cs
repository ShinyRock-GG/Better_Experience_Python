using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory.UI;

internal class DialogueManager : StoryService
{
	private bool active = false;

	private DispatcherService dispatcher;

	private OverlayService overlayService;

	private DialogueWindow window;

	private bool skipPronounce = false;

	private IInputHandle[] numberKeys;

	private IInputHandle skipKey;

	public bool IsActive => active;

	public VisualElement RootVisualElement { get; internal set; }

	public Observable<string> OnRespond => window.OnRespond;

	public Observable OnPronounceComplete { get; } = new Observable();

	public Observable OnContinue => window.OnContinue;

	public bool ShowingSubtitle => window.ShowingSubtitle;

	public override void OnStart()
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		base.OnStart();
		dispatcher = Lookup<DispatcherService>();
		overlayService = Lookup<OverlayService>();
		window = new DialogueWindow();
		window.OnSkipPronounce.Add(delegate
		{
			skipPronounce = true;
		}, base.Scope);
		numberKeys = new IInputHandle[9]
		{
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha1, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha2, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha3, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha4, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha5, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha6, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha7, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha8, Array.Empty<KeyCode>()), base.Scope),
			dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Alpha9, Array.Empty<KeyCode>()), base.Scope)
		};
		skipKey = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.LeftControl, Array.Empty<KeyCode>()), base.Scope);
		dispatcher.DoUpdate.Add(ProcessInput, base.Scope);
	}

	private void ProcessInput()
	{
		if (!IsActive)
		{
			return;
		}
		if (skipKey.IsHold)
		{
			window.ReactSkipKey();
		}
		for (int i = 0; i < numberKeys.Length; i++)
		{
			if (numberKeys[i].Up && window.HasResponseAt(i))
			{
				window.Respond(i);
				break;
			}
		}
	}

	public void SetActive(bool value)
	{
		if (active != value)
		{
			active = value;
			if (active)
			{
				ActivateWindow();
			}
			else
			{
				DeactivateWindow();
			}
		}
	}

	private void ActivateWindow()
	{
		RootVisualElement.Add(window.Root);
		base.Session.Player.ActionsEnabled = false;
		overlayService.CursorHolders.Add(base.Scope);
	}

	private void DeactivateWindow()
	{
		RootVisualElement.Remove(window.Root);
		base.Session.Player.ActionsEnabled = true;
		overlayService.CursorHolders.Remove(base.Scope);
		ClearSubtitle();
	}

	public void SetSubtitle(string who, string what)
	{
		dispatcher.StartCoroutine(Pronounce(who, what), base.Scope);
	}

	private IEnumerator Pronounce(string who, string what)
	{
		skipPronounce = false;
		for (int i = 0; i < what.Length; i++)
		{
			if (skipPronounce)
			{
				i = what.Length - 1;
			}
			window.SetSubtitle(who, what.Substring(0, i + 1));
			yield return new WaitForSeconds(0.01f);
		}
		OnPronounceComplete.Invoke();
	}

	public void SetResponses(IReadOnlyList<DialogueResponse> responses)
	{
		window.SetResponses(responses);
	}

	internal void SetRequestContinuation()
	{
		window.SetRequestContinuation();
	}

	internal void ClearSubtitle()
	{
		window.ClearSubtitle();
	}
}
