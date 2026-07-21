using System.Collections.Generic;
using System.Linq;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Windows;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.Features.Overlay;

public class OverlayService : PluginService
{
	private enum UIState
	{
		MainMenu,
		Options,
		Game,
		Loading,
		UnkFullScreenWindow
	}

	private List<Drawable> drawables;

	private List<TemporaryNotification> notifications;

	private List<Drawable> alwaysOnScreenRenderables;

	private List<UITKManagedPanel> panels = new List<UITKManagedPanel>();

	private DispatcherService dispatcher;

	private SessionTracker sessionTracker;

	private List<Drawable> fitQueue = new List<Drawable>();

	private GameOptionsWindow gameOptionsWnd;

	public LoadingScreenFeature LoadingScreen { get; private set; }

	public VLayout<Drawable> TopRightPane { get; private set; }

	public VLayout<Drawable> BottomLeftPane { get; private set; }

	public VLayout<Drawable> NotificationArea { get; private set; }

	public ISet<ScopeSupport> CursorHolders { get; } = new HashSet<ScopeSupport>();

	internal SessionOverlayRenderService SessionService { get; set; }

	public OverlayService(List<Drawable> drawables, List<TemporaryNotification> notifications, List<Drawable> alwaysOnScreenRenderables)
	{
		this.drawables = drawables;
		this.notifications = notifications;
		TopRightPane = new VLayout<Drawable>();
		drawables.Add(new DockingContainer(Plugin.MonkeyMode ? (Vector2Int.up + Vector2Int.left) : (Vector2Int.up + Vector2Int.right), TopRightPane)
		{
			Position = (Plugin.MonkeyMode ? new Vector2(10f, 150f) : new Vector2(0f, 150f)),
			EnableNative = true
		});
		BottomLeftPane = new VLayout<Drawable>();
		BottomLeftPane.PrependMode = true;
		drawables.Add(new DockingContainer(Vector2Int.down + Vector2Int.left, BottomLeftPane)
		{
			EnableNative = true
		});
		BottomLeftPane.Add(new DrawableLabel("")
		{
			PreferredSize = new Vector2(1f, 200f)
		});
		DockingContainer notificationAreaAnchor = new DockingContainer(Vector2Int.up + Vector2Int.left)
		{
			Position = new Vector2(90f, Plugin.MonkeyMode ? 30 : 10),
			EnableNative = true
		};
		NotificationArea = notificationAreaAnchor.Add(new VLayout<Drawable>());
		drawables.Add(notificationAreaAnchor);
		this.alwaysOnScreenRenderables = alwaysOnScreenRenderables;
	}

	internal void AddToFitQueue(Drawable drawable, ScopeSupport scope)
	{
		fitQueue.Add(drawable);
		scope.OnDispose += delegate
		{
			fitQueue.Remove(drawable);
		};
	}

	public void AddDrawable(Drawable drawable, ScopeSupport scope = null)
	{
		drawables.Add(drawable);
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				drawables.Remove(drawable);
			};
		}
	}

	public void AddAlwaysDrawable(Drawable drawable, ScopeSupport scope = null)
	{
		alwaysOnScreenRenderables.Add(drawable);
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				drawables.Remove(drawable);
			};
		}
	}

	public void Notify(TemporaryNotification notification)
	{
		notifications.Add(notification);
	}

	public void InfoMessage(string text, float duration = 15f, float fadeOut = 3f)
	{
		notifications.Add(new TemporaryNotification
		{
			Drawable = new DrawableLabel(text),
			SpecificContainer = NotificationArea,
			Duration = duration,
			FadeOut = 3f
		});
	}

	public UITKManagedPanel RequestPanel(int width = 0, int height = 0)
	{
		UITKManagedPanel panel = panels.Where((UITKManagedPanel x) => x.width == width && x.height == height).FirstOrDefault();
		if (panel == null)
		{
			panel = new UITKManagedPanel(width, height);
			panels.Add(panel);
			CreatePanel(panel);
		}
		return panel;
	}

	private void CreatePanel(UITKManagedPanel panel)
	{
		UIToolkitInitializer uitki = new UIToolkitInitializer();
		UIDocument doc = uitki.InitializeUIToolkit();
		uitki.FixBattlehub();
		if (panel.width != 0 && panel.height != 0)
		{
			doc.panelSettings.referenceResolution = new Vector2Int(panel.width, panel.height);
			doc.panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			doc.panelSettings.match = 1f;
		}
		doc.rootVisualElement.Add(panel.MainMenu);
		doc.rootVisualElement.Add(panel.GameView);
		doc.rootVisualElement.Add(panel.OptionsMenu);
		doc.rootVisualElement.Add(panel.Loader);
		doc.rootVisualElement.Add(panel.AlwaysOnScreen);
		panel.panelSettings = doc.panelSettings;
	}

	public override void OnStart()
	{
		base.OnStart();
		dispatcher = Lookup<DispatcherService>();
		sessionTracker = Lookup<SessionTracker>();
		LoadingScreen = base.Scope.AddService(new LoadingScreenFeature());
		gameOptionsWnd = base.Scope.Parent.AddService(new GameOptionsWindow());
		dispatcher.DoUpdate.Add(OnUpdate, base.Scope);
		dispatcher.DrawGUI.Add(OnFitComponents, base.Scope);
	}

	private void OnFitComponents()
	{
		foreach (Drawable x in fitQueue)
		{
			x.Fit();
		}
	}

	private void OnUpdate()
	{
		UIState state = GetCurrentUIState();
		foreach (UITKManagedPanel p in panels)
		{
			SetVisible(p.MainMenu, state == UIState.MainMenu);
			SetVisible(p.OptionsMenu, state == UIState.Options);
			SetVisible(p.GameView, state == UIState.Game);
			SetVisible(p.Loader, state == UIState.Loading);
			if (p.panelSettings != null)
			{
				if (state == UIState.Loading && p.Loader.childCount > 0)
				{
					p.panelSettings.sortingOrder = 2f;
				}
				else
				{
					p.panelSettings.sortingOrder = 1f;
				}
			}
		}
	}

	private void SetVisible(VisualElement mainMenu, bool v)
	{
		if (v)
		{
			mainMenu.style.display = DisplayStyle.Flex;
		}
		else
		{
			mainMenu.style.display = DisplayStyle.None;
		}
	}

	private UIState GetCurrentUIState()
	{
		if (gameOptionsWnd.Visible)
		{
			return UIState.Options;
		}
		if (LoadingScreen.Visible)
		{
			return UIState.Loading;
		}
		if (sessionTracker.DesignerMode)
		{
			return UIState.Game;
		}
		if (SessionService == null)
		{
			return UIState.MainMenu;
		}
		if (SessionService.HasVisibleWindows)
		{
			return UIState.UnkFullScreenWindow;
		}
		return UIState.Game;
	}
}
