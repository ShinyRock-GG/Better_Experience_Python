using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace BetterExperience.GameScopes;

internal class InputHandle : IInputHandle
{
	private float holdSince;

	public bool Keyboard { get; private set; }

	public KeyboardShortcut Shortcut { get; private set; }

	public List<KeyCode> Modifiers { get; private set; } = new List<KeyCode>();

	public bool Up { get; private set; }

	public bool Down { get; private set; }

	public bool IsHold { get; private set; }

	public float Duration
	{
		get
		{
			if (!IsHold && !Up)
			{
				return 0f;
			}
			return Time.time - holdSince;
		}
	}

	public void InitKeyboard(KeyboardShortcut shortcut)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		Modifiers.Clear();
		Shortcut = shortcut;
		Keyboard = true;
		foreach (KeyCode mod in shortcut.Modifiers)
		{
			Modifiers.Add(mod);
		}
	}

	internal void SetFlags(bool now, bool up, bool down)
	{
		if (down)
		{
			holdSince = Time.time;
		}
		else if (holdSince > 0f && !(up || now))
		{
			holdSince = 0f;
		}
		IsHold = now;
		Up = up;
		Down = down;
	}

	public override string ToString()
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (Keyboard)
		{
			return ((object)Shortcut/*cast due to constrained. prefix*/).ToString();
		}
		return base.ToString();
	}
}
