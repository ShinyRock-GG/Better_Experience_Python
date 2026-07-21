using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using BetterExperience.Wrappers.Windows;
using UnityEngine.UIElements;

namespace BetterExperience.Features.PluginOptions;

public class PluginOptionsService : PluginService
{
	public enum SettingsType
	{
		general,
		player,
		guest,
		scene,
		custom
	}

	private class SettingsPanel : PopupWindow
	{
		private TabPanel tabs = new TabPanel();

		public SettingsView common;

		public SettingsView player;

		public SettingsView guest;

		public SettingsView scene;

		private Dictionary<string, SettingsView> genericViews = new Dictionary<string, SettingsView>();

		public SettingsPanel()
		{
			common = new SettingsView();
			player = new SettingsView();
			guest = new SettingsView();
			scene = new SettingsView();
			text = "BetterExperience settings";
			base.style.position = Position.Absolute;
			tabs.CanUnToggle = false;
			Add(tabs);
			tabs.AddTab("Common", common);
			tabs.AddTab("Player", player);
			tabs.AddTab("Guest", guest);
			tabs.AddTab("Scene", scene);
		}

		public void SetModalInterface(MainModalWindow wnd)
		{
			common.SetModal(wnd);
			player.SetModal(wnd);
			guest.SetModal(wnd);
			scene.SetModal(wnd);
			genericViews.Values.ForEach(delegate(SettingsView x)
			{
				x.SetModal(wnd);
			});
		}

		internal void RefreshAll()
		{
			common.Refresh();
			player.Refresh();
			guest.Refresh();
			scene.Refresh();
			genericViews.Values.ForEach(delegate(SettingsView x)
			{
				x.Refresh();
			});
		}

		internal void AddTab(string title, VisualElement component, ScopeSupport scope)
		{
			tabs.AddTab(title, component);
			if (scope != null)
			{
				scope.OnDispose += delegate
				{
					tabs.RemoveTab(component);
				};
			}
		}

		public SettingsView GetSettings(string genericName, ScopeSupport scopeSupport)
		{
			if (!genericViews.TryGetValue(genericName, out var settings))
			{
				settings = new SettingsView();
				AddTab(genericName, settings, scopeSupport);
			}
			return settings;
		}
	}

	public class SettingsView : VisualElement
	{
		private class ToggleItem
		{
			public string group { get; set; }

			public string text { get; set; }

			public Action handler { get; set; }

			public Action<Button> bind { get; set; }

			public Action<Button> unbind { get; set; } = delegate
			{
			};
		}

		private MultiColumnListView table;

		private List<ToggleItem> items = new List<ToggleItem>();

		private Dictionary<Button, Action> clickHandlers = new Dictionary<Button, Action>();

		private MainModalWindow modal;

		public SettingsView()
		{
			base.style.flexDirection = FlexDirection.Column;
			this.Label("Interactive settings editor");
			this.Label("  * Most settings require game/session/interview restart to take effect");
			base.style.width = new Length(100f, LengthUnit.Percent);
			table = new MultiColumnListView();
			table.fixedItemHeight = 25f;
			table.columns.Add(new Column
			{
				title = "Flag",
				makeCell = () => new Button
				{
					text = "Toggle"
				},
				bindCell = delegate(VisualElement e, int i)
				{
					items[i].bind(e as Button);
				},
				width = 100f
			});
			table.columns.Add(new Column
			{
				title = "Description",
				makeCell = () => new Label(),
				bindCell = delegate(VisualElement e, int i)
				{
					(e as Label).text = items[i].text;
				},
				width = 600f
			});
			table.itemsSource = items;
			Add(table);
		}

		public void SetModal(MainModalWindow modalWindow)
		{
			modal = modalWindow;
		}

		internal void AddFlag(string name, Func<bool> getter, Action<bool> setter, string group, ScopeSupport scopeSupport)
		{
			ToggleItem item = CreateToggleItem(name, group, scopeSupport);
			item.handler = delegate
			{
				bool switchto = !getter();
				modal.MessageBoxYesNo("Toggle '" + name + "' to " + (switchto ? "Yes" : "No")).OnResult += delegate(bool yes)
				{
					if (yes)
					{
						setter(switchto);
					}
					int num = table.itemsSource.IndexOf(item);
					if (num >= 0)
					{
						table.RefreshItem(num);
					}
				};
			};
			item.bind = delegate(Button btn)
			{
				btn.Show();
				btn.text = (getter() ? "Yes" : "No");
				RegClick(btn, item.handler);
			};
			items.Add(item);
			table.RefreshItems();
		}

		internal void AddHotKey(string name, Func<KeyboardShortcut> getter, Action<KeyboardShortcut> setter, string group, ScopeSupport scopeSupport)
		{
			ToggleItem item = CreateToggleItem(name, group, scopeSupport);
			item.handler = delegate
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				GrabShortcut(getter()).OnResult += delegate(KeyboardShortcut? result)
				{
					//IL_0019: Unknown result type (might be due to invalid IL or missing references)
					//IL_001e: Unknown result type (might be due to invalid IL or missing references)
					if (result.HasValue)
					{
						KeyboardShortcut switchto = result.Value;
						modal.MessageBoxYesNo("Change '" + name + "' hotkey to " + ((object)switchto).ToString()).OnResult += delegate(bool yes)
						{
							//IL_000f: Unknown result type (might be due to invalid IL or missing references)
							if (yes)
							{
								setter(switchto);
							}
							int num = table.itemsSource.IndexOf(item);
							if (num >= 0)
							{
								table.RefreshItem(num);
							}
						};
					}
				};
			};
			item.bind = delegate(Button btn)
			{
				//IL_000e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0013: Unknown result type (might be due to invalid IL or missing references)
				btn.Show();
				btn.text = ((object)getter()/*cast due to constrained. prefix*/).ToString();
				RegClick(btn, item.handler);
			};
			items.Add(item);
			table.RefreshItems();
		}

		internal void AddAction(string name, Action action, string group, ScopeSupport scopeSupport)
		{
			ToggleItem item = CreateToggleItem(name, group, scopeSupport);
			item.handler = action;
			item.bind = delegate(Button btn)
			{
				btn.text = "";
				RegClick(btn, item.handler);
			};
			items.Add(item);
			table.RefreshItems();
		}

		internal void AddValue(string name, Func<float> getter, Action<float> setter, string group, ScopeSupport scopeSupport)
		{
			ToggleItem item = CreateToggleItem(name, group, scopeSupport);
			item.handler = delegate
			{
				GrabValue(getter()).OnResult += delegate(float? result)
				{
					if (result.HasValue)
					{
						float switchto = result.Value;
						modal.MessageBoxYesNo("Change '" + name + "' value to " + switchto).OnResult += delegate(bool yes)
						{
							if (yes)
							{
								setter(switchto);
							}
							int num = table.itemsSource.IndexOf(item);
							if (num >= 0)
							{
								table.RefreshItem(num);
							}
						};
					}
				};
			};
			item.bind = delegate(Button btn)
			{
				btn.text = getter().ToString();
				RegClick(btn, item.handler);
			};
			items.Add(item);
			table.RefreshItems();
		}

		private ToggleItem CreateToggleItem(string name, string group, ScopeSupport scopeSupport)
		{
			if ((items.Count == 0 || items.Last().group != group) && (items.Count == 0 || (group != "Features" && items.Last().group != "Features")))
			{
				items.Add(new ToggleItem
				{
					group = group,
					text = group,
					bind = delegate(Button btn)
					{
						btn.Hide();
					},
					unbind = delegate(Button btn)
					{
						btn.Show();
					}
				});
			}
			if (group != "Features")
			{
				int idx = name.IndexOf(":");
				if (idx != -1)
				{
					name = name.Substring(idx + 1);
				}
			}
			ToggleItem item = new ToggleItem();
			item.text = ((group != "Features") ? "     " : "") + name;
			item.group = group;
			item.unbind = delegate(Button btn)
			{
				btn.clicked -= item.handler;
			};
			scopeSupport.OnDispose += delegate
			{
				items.Remove(item);
				table.RefreshItems();
			};
			return item;
		}

		private unsafe MayBeResult<KeyboardShortcut?> GrabShortcut(KeyboardShortcut shortcut)
		{
			MayBeResult<KeyboardShortcut?> promise = new MayBeResult<KeyboardShortcut?>();
			modal.RequestInput("Type new shortcut", ((object)(*(KeyboardShortcut*)(&shortcut))/*cast due to constrained. prefix*/).ToString()).OnResult += delegate(string result)
			{
				//IL_000c: Unknown result type (might be due to invalid IL or missing references)
				//IL_003e: Unknown result type (might be due to invalid IL or missing references)
				//IL_0043: Unknown result type (might be due to invalid IL or missing references)
				//IL_0046: Unknown result type (might be due to invalid IL or missing references)
				KeyboardShortcut? result2 = null;
				if (result != null)
				{
					try
					{
						result2 = KeyboardShortcut.Deserialize(result);
					}
					catch (Exception ex)
					{
						modal.MessageError(ex.Message);
					}
					if (result2.HasValue && ((object)result2.Value/*cast due to constrained. prefix*/).Equals((object)KeyboardShortcut.Empty))
					{
						modal.MessageError("Unable to parse shortcut. Examples:\nP or P+LeftAlt/RightAlt/LeftControl/LeftShift");
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
			modal.RequestInput("Type new value", value.ToString()).OnResult += delegate(string result)
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
						modal.MessageError(ex.Message);
					}
				}
				promise.SetResult(result2);
			};
			return promise;
		}

		private void RegClick(Button b, Action a)
		{
			if (clickHandlers.TryGetValue(b, out var p))
			{
				b.clicked -= p;
			}
			b.clicked += a;
			clickHandlers[b] = a;
		}

		internal void Refresh()
		{
			table.RefreshItems();
		}
	}

	private Button newSettingsButton = new Button();

	private SettingsPanel newSettingsPanel = new SettingsPanel();

	private Observable onRefresh = new Observable();

	public override void OnInit()
	{
		newSettingsButton.text = "BetterExperience";
		newSettingsButton.style.position = Position.Absolute;
		newSettingsButton.style.left = 16f;
		newSettingsButton.style.top = 600f;
		newSettingsButton.style.width = 265f;
		newSettingsPanel.SetModalInterface(Lookup<MainModalWindow>());
		newSettingsPanel.style.top = 50f;
		newSettingsPanel.style.left = 300f;
		newSettingsPanel.style.width = 930f;
		newSettingsPanel.style.height = 620f;
		newSettingsPanel.Hide();
		newSettingsButton.clicked += delegate
		{
			newSettingsPanel.SetVisible(!newSettingsPanel.IsVisible());
			if (newSettingsPanel.IsVisible())
			{
				onRefresh.Invoke();
			}
		};
		newSettingsPanel.AddManipulator(new WindowDragManipulator());
		onRefresh.Add(newSettingsPanel.RefreshAll, base.Scope);
	}

	public void AddOptions(string title, VisualElement component, ScopeSupport scope, Action refreshCallback = null)
	{
		newSettingsPanel.AddTab(title, component, scope);
		if (refreshCallback != null)
		{
			onRefresh.Add(refreshCallback, base.Scope);
		}
	}

	private SettingsView GetSettings(SettingsType settingsType, string customLabel = "unnamed", ScopeSupport customscope = null)
	{
		return settingsType switch
		{
			SettingsType.custom => newSettingsPanel.GetSettings(customLabel, customscope), 
			SettingsType.player => newSettingsPanel.player, 
			SettingsType.guest => newSettingsPanel.guest, 
			SettingsType.scene => newSettingsPanel.scene, 
			_ => newSettingsPanel.common, 
		};
	}

	public PluginOptionsService Expose(ConfigEntry<bool> configEntry, ScopeSupport scope, SettingsType type = SettingsType.general, string customLabel = null)
	{
		GetSettings(type, customLabel, scope).AddFlag(((ConfigEntryBase)configEntry).Description.Description, () => configEntry.Value, delegate(bool v)
		{
			configEntry.Value = v;
		}, ((ConfigEntryBase)configEntry).Definition.Section, scope);
		return this;
	}

	public PluginOptionsService Expose(ConfigEntry<KeyboardShortcut> configEntry, ScopeSupport scope, SettingsType type = SettingsType.general, string customLabel = null)
	{
		GetSettings(type, customLabel, scope).AddHotKey(((ConfigEntryBase)configEntry).Description.Description, () => configEntry.Value, delegate(KeyboardShortcut v)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			configEntry.Value = v;
		}, ((ConfigEntryBase)configEntry).Definition.Section, scope);
		return this;
	}

	public PluginOptionsService Expose(ConfigEntry<float> configEntry, ScopeSupport scope, SettingsType type = SettingsType.general, string customLabel = null)
	{
		GetSettings(type, customLabel, scope).AddValue(((ConfigEntryBase)configEntry).Description.Description, () => configEntry.Value, delegate(float v)
		{
			configEntry.Value = v;
		}, ((ConfigEntryBase)configEntry).Definition.Section, scope);
		return this;
	}

	public PluginOptionsService Expose(ConfigEntry<int> configEntry, ScopeSupport scope, SettingsType type = SettingsType.general, string customLabel = null)
	{
		GetSettings(type, customLabel, scope).AddValue(((ConfigEntryBase)configEntry).Description.Description, () => configEntry.Value, delegate(float v)
		{
			configEntry.Value = (int)v;
		}, ((ConfigEntryBase)configEntry).Definition.Section, scope);
		return this;
	}

	public PluginOptionsService ExposeAction(Action action, string name, string section, ScopeSupport scope, SettingsType type = SettingsType.general, string customLabel = null)
	{
		GetSettings(type, customLabel, scope).AddAction(name, action, section, scope);
		return this;
	}

	public override void OnStart()
	{
		UITKManagedPanel panel = Lookup<OverlayService>().RequestPanel(1280, 720);
		panel.OptionsMenu.Add(newSettingsButton);
		panel.OptionsMenu.Add(newSettingsPanel);
	}
}
