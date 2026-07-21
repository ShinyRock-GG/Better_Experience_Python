using System;
using System.Collections;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.GameScopes;
using BetterExperience.PyStory.UI;
using BetterExperience.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory;

internal class ObjectInteractionService : SessionService
{
	private HandControllerV2 handController;

	private IInputHandle interactKey;

	private IInputHandle lmbKey;

	private IInputHandle rmbKey;

	private DialogueManager dialogueManager;

	private InteractiveUI ui;

	private InteractiveObject activeObject;

	private int layerMask;

	public ConfigEntry<KeyboardShortcut> UseHotkey { get; internal set; }

	public override void OnStart()
	{
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		base.OnStart();
		layerMask = LayerMask.GetMask("DialogueSys");
		CustomSceneFeature csf = Lookup<CustomSceneFeature>();
		DispatcherService dispatcher = Lookup<DispatcherService>();
		handController = base.Session.Player.GameObject.GetComponentInChildren<HandControllerV2>();
		interactKey = dispatcher.Input.KeyboardEvent(UseHotkey, base.Scope);
		lmbKey = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Mouse0, Array.Empty<KeyCode>()), base.Scope);
		rmbKey = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Mouse1, Array.Empty<KeyCode>()), base.Scope);
		dialogueManager = Lookup<DialogueManager>();
		ui = new InteractiveUI(UseHotkey);
		UIBuilder.Hide((VisualElement)ui);
		csf.EditorUiPanel.GameView.Add(ui);
		base.Scope.OnDispose += ui.RemoveFromHierarchy;
		dispatcher.StartCoroutine(CheckInteractionTarget(), base.Scope);
	}

	private IEnumerator CheckInteractionTarget()
	{
		while (base.Scope.Started)
		{
			CheckObject();
			yield return null;
		}
	}

	private void CheckObject()
	{
		Transform t = Camera.main.transform;
		if (!dialogueManager.IsActive && Physics.Raycast(new Ray(t.position, t.forward), out var hit, 1.5f, layerMask))
		{
			if (activeObject != null && !activeObject.enabled)
			{
				SetActiveObject(null);
			}
			InteractiveObject io = hit.collider.gameObject.GetComponent<InteractiveObject>();
			if (io != null && io.enabled)
			{
				SetActiveObject(io);
			}
			else
			{
				SetActiveObject(null);
			}
		}
		else
		{
			SetActiveObject(null);
		}
		if (activeObject != null)
		{
			ui.SetLabel(activeObject.label);
			UIBuilder.Show((VisualElement)ui);
			if (interactKey.Up || (lmbKey.Up && rmbKey.IsHold && (!((Behaviour)(object)handController).enabled || handController.weigth == 0f)))
			{
				activeObject.FireHandler();
			}
		}
		else
		{
			UIBuilder.Hide((VisualElement)ui);
		}
	}

	private void SetActiveObject(InteractiveObject obj)
	{
		if (activeObject != obj)
		{
			if (activeObject != null)
			{
				activeObject.FireOnLeave();
			}
			activeObject = obj;
			if (activeObject != null)
			{
				activeObject.FireOnEnter();
			}
		}
	}
}
