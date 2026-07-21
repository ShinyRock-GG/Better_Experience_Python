using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Windows;
using UnityEngine;

namespace BetterExperience.Features.PluginOptions;

internal class GeneralSettings
{
	private GameSession session;

	private Observable RefreshAll = new Observable();

	private GridLayout mainGrid;

	private GridLayout currentGrid;

	private string currentGroup;

	internal PluginOptionsService.SettingsView target;

	public Drawable Component { get; private set; }

	public GeneralSettings()
	{
		Component = CreateGUI();
	}

	public void SetSession(GameSession session)
	{
		this.session = session;
	}

	private Drawable CreateGUI()
	{
		VLayout<Drawable> root = new VLayout<Drawable>();
		root.Label("Interactive settings editor");
		root.Label("  * Most settings require game/session/interview restart to take effect");
		mainGrid = root.Grid(1f, 1f);
		mainGrid.Position = new Vector2(10f, 0f);
		return root;
	}

	internal void Refresh()
	{
		RefreshAll.Invoke();
	}

	private GridLayout LocateGrid(string group, ScopeSupport scope)
	{
		if ("Features" == group)
		{
			currentGroup = null;
			return mainGrid;
		}
		if (group == currentGroup)
		{
			return currentGrid;
		}
		if (currentGroup != null)
		{
			mainGrid.Label("");
			mainGrid.Label(group);
			mainGrid.NewLine();
		}
		mainGrid.Label("", scope);
		currentGrid = mainGrid.Grid();
		mainGrid.NewLine();
		currentGroup = group;
		return currentGrid;
	}

	private DrawableButton LabelButton(string name, string group, ScopeSupport scopeSupport)
	{
		GridLayout grid = LocateGrid(group, scopeSupport);
		DrawableButton btn = grid.Button(null, scopeSupport);
		if (group != "Features")
		{
			int idx = name.IndexOf(":");
			if (idx != -1)
			{
				name = name.Substring(idx + 1);
			}
		}
		grid.Label(name, scopeSupport);
		grid.NewLine();
		return btn;
	}

	public void AddFlag(string name, Func<bool> getter, Action<bool> setter, string group, ScopeSupport scopeSupport)
	{
		DrawableButton btn = LabelButton(name, group, scopeSupport);
		Action refresh = delegate
		{
			btn.Text = (getter() ? "Yes" : "No");
		};
		refresh();
		btn.OnClick += delegate
		{
			bool switchto = !getter();
			session.Modal.MessageBoxYesNo("Toggle '" + name + "' to " + (switchto ? "Yes" : "No")).OnResult += delegate(bool yes)
			{
				if (yes)
				{
					setter(switchto);
				}
				refresh();
			};
		};
		RefreshAll.Add(refresh, scopeSupport);
		if (target != null)
		{
			target.AddFlag(name, getter, setter, group, scopeSupport);
		}
	}

	public void AddHotkey(string name, Func<KeyboardShortcut> getter, Action<KeyboardShortcut> setter, string group, ScopeSupport scopeSupport)
	{
		DrawableButton btn = LabelButton(name, group, scopeSupport);
		Action refresh = delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			btn.Text = ((object)getter()/*cast due to constrained. prefix*/).ToString();
		};
		refresh();
		btn.OnClick += delegate
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			GrabShortcut(getter()).OnResult += delegate(KeyboardShortcut? result)
			{
				//IL_0019: Unknown result type (might be due to invalid IL or missing references)
				//IL_001e: Unknown result type (might be due to invalid IL or missing references)
				if (result.HasValue)
				{
					KeyboardShortcut switchto = result.Value;
					session.Modal.MessageBoxYesNo("Change '" + name + "' hotkey to " + ((object)switchto).ToString()).OnResult += delegate(bool yes)
					{
						//IL_000f: Unknown result type (might be due to invalid IL or missing references)
						if (yes)
						{
							setter(switchto);
						}
						refresh();
					};
				}
			};
		};
		RefreshAll.Add(refresh, scopeSupport);
		if (target != null)
		{
			target.AddHotKey(name, getter, setter, group, scopeSupport);
		}
	}

	public void AddValue(string name, Func<float> getter, Action<float> setter, string group, ScopeSupport scopeSupport)
	{
		DrawableButton btn = LabelButton(name, group, scopeSupport);
		Action refresh = delegate
		{
			btn.Text = getter().ToString();
		};
		refresh();
		btn.OnClick += delegate
		{
			GrabValue(getter()).OnResult += delegate(float? result)
			{
				if (result.HasValue)
				{
					float switchto = result.Value;
					session.Modal.MessageBoxYesNo("Change '" + name + "' value to " + switchto).OnResult += delegate(bool yes)
					{
						if (yes)
						{
							setter(switchto);
						}
						refresh();
					};
				}
			};
		};
		RefreshAll.Add(refresh, scopeSupport);
		if (target != null)
		{
			target.AddValue(name, getter, setter, group, scopeSupport);
		}
	}

	private unsafe MayBeResult<KeyboardShortcut?> GrabShortcut(KeyboardShortcut shortcut)
	{
		MayBeResult<KeyboardShortcut?> promise = new MayBeResult<KeyboardShortcut?>();
		session.Modal.RequestInput("Type new shortcut", ((object)(*(KeyboardShortcut*)(&shortcut))/*cast due to constrained. prefix*/).ToString()).OnResult += delegate(string result)
		{
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0043: Unknown result type (might be due to invalid IL or missing references)
			//IL_0048: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			KeyboardShortcut? result2 = null;
			if (result != null)
			{
				try
				{
					result2 = KeyboardShortcut.Deserialize(result);
				}
				catch (Exception ex)
				{
					session.Modal.MessageError(ex.Message);
				}
				if (result2.HasValue && ((object)result2.Value/*cast due to constrained. prefix*/).Equals((object)KeyboardShortcut.Empty))
				{
					session.Modal.MessageError("Unable to parse shortcut. Examples:\nP or P+LeftAlt/RightAlt/LeftControl/LeftShift");
					result2 = null;
				}
			}
			promise.SetResult(result2);
		};
		return promise;
	}

	private MayBeResult<float?> GrabValue(float value)
	{
		MayBeResult<float?> promise = new MayBeResult<float?>();
		session.Modal.RequestInput("Type new value", value.ToString()).OnResult += delegate(string result)
		{
			float? result2 = null;
			if (result != null)
			{
				try
				{
					result2 = float.Parse(result);
				}
				catch (Exception ex)
				{
					session.Modal.MessageError(ex.Message);
				}
			}
			promise.SetResult(result2);
		};
		return promise;
	}
}
