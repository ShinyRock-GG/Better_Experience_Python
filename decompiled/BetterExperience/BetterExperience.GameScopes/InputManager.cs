using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace BetterExperience.GameScopes;

public class InputManager
{
	private class KeyHandleStruct
	{
		public List<InputHandle> Simple { get; } = new List<InputHandle>();

		public List<InputHandle> WithModifiers { get; } = new List<InputHandle>();

		internal void Clear()
		{
			foreach (InputHandle k in Simple)
			{
				k.SetFlags(now: false, up: false, down: false);
			}
			foreach (InputHandle k2 in WithModifiers)
			{
				k2.SetFlags(now: false, up: false, down: false);
			}
		}
	}

	public class FocusTracker
	{
		private GameObject lastObject;

		private bool lastResult;

		private FocusController focusCtl;

		public bool IsTextField()
		{
			GameObject selected = ((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject : null);
			if (lastObject == selected)
			{
				if (focusCtl == null)
				{
					return lastResult;
				}
				return focusCtl.focusedElement is TextField;
			}
			focusCtl = null;
			lastObject = selected;
			if (lastObject != null)
			{
				if (lastObject.TryGetComponent<InputField>(out var _))
				{
					return lastResult = true;
				}
				if (lastObject.TryGetComponent<PanelEventHandler>(out var panel))
				{
					focusCtl = panel.panel.focusController;
					return lastResult = focusCtl.focusedElement is TextField;
				}
				return lastResult = false;
			}
			return lastResult = false;
		}
	}

	private Dictionary<KeyCode, KeyHandleStruct> keyboardListeners = new Dictionary<KeyCode, KeyHandleStruct>();

	public static FocusTracker focusTracker { get; } = new FocusTracker();

	public bool Enabled { get; set; } = true;

	public IInputHandle KeyboardEvent(KeyboardShortcut shortcut, ScopeSupport scope)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		InputHandle handle = new InputHandle();
		handle.InitKeyboard(shortcut);
		OnHandleCreate(handle);
		scope.OnDispose += delegate
		{
			RemoveHandle(handle);
		};
		return handle;
	}

	public IInputHandle KeyboardEvent(ConfigEntry<KeyboardShortcut> shortcut, ScopeSupport scope)
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		InputHandle handle = new InputHandle();
		handle.InitKeyboard(shortcut.Value);
		OnHandleCreate(handle);
		EventHandler eh = delegate
		{
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			OnHandleRemove(handle);
			handle.InitKeyboard(shortcut.Value);
			OnHandleCreate(handle);
		};
		shortcut.SettingChanged += eh;
		scope.OnDispose += delegate
		{
			shortcut.SettingChanged -= eh;
			RemoveHandle(handle);
		};
		return handle;
	}

	private void RemoveHandle(InputHandle handle)
	{
		OnHandleRemove(handle);
	}

	private void OnHandleCreate(InputHandle handle)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (handle.Keyboard)
		{
			Dictionary<KeyCode, KeyHandleStruct> dic = keyboardListeners;
			KeyboardShortcut shortcut = handle.Shortcut;
			KeyHandleStruct keyhandles = dic.GetValueOrAdd(shortcut.MainKey, () => new KeyHandleStruct());
			if (handle.Modifiers.Count == 0)
			{
				keyhandles.Simple.Add(handle);
			}
			else
			{
				keyhandles.WithModifiers.Add(handle);
			}
		}
	}

	private void OnHandleRemove(InputHandle handle)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		if (!handle.Keyboard)
		{
			return;
		}
		Dictionary<KeyCode, KeyHandleStruct> dictionary = keyboardListeners;
		KeyboardShortcut shortcut = handle.Shortcut;
		if (dictionary.TryGetValue(shortcut.MainKey, out var keyhandles))
		{
			if (handle.Modifiers.Count == 0)
			{
				keyhandles.Simple.Remove(handle);
			}
			else
			{
				keyhandles.WithModifiers.Remove(handle);
			}
		}
	}

	[Timed]
	public void OnUpdate()
	{
		if (Enabled)
		{
			ProcessKeyboardInput();
		}
	}

	private void ProcessKeyboardInput()
	{
		if (focusTracker.IsTextField())
		{
			foreach (KeyHandleStruct v in keyboardListeners.Values)
			{
				v.Clear();
			}
			return;
		}
		foreach (KeyValuePair<KeyCode, KeyHandleStruct> kv in keyboardListeners)
		{
			KeyCode k = kv.Key;
			bool now = Input.GetKey(k);
			bool up = Input.GetKeyUp(k);
			bool down = Input.GetKeyDown(k);
			bool handledWithModifiers = false;
			foreach (InputHandle handle in kv.Value.WithModifiers)
			{
				bool accept = true;
				foreach (KeyCode mk in handle.Modifiers)
				{
					if (!Input.GetKey(mk))
					{
						accept = false;
						break;
					}
				}
				if (accept)
				{
					handle.SetFlags(now, up, down);
					handledWithModifiers = true;
				}
				else
				{
					handle.SetFlags(now: false, up: false, down: false);
				}
			}
			foreach (InputHandle handle2 in kv.Value.Simple)
			{
				if (!handledWithModifiers)
				{
					handle2.SetFlags(now, up, down);
				}
				else
				{
					handle2.SetFlags(now: false, up: false, down: false);
				}
			}
		}
	}
}
