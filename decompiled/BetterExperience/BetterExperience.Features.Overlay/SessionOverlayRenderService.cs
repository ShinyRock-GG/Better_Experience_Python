using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts;
using Assets._ReusableScripts.UI.Drawing;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Assets.TValle.IU.Runtime.Drawing;
using Assets.TValle.IU.Runtime.Drawing.Abstracts;
using BepInEx.Configuration;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Windows;
using UnityEngine;

namespace BetterExperience.Features.Overlay;

internal class SessionOverlayRenderService : SessionService
{
	private List<Drawable> renderables;

	private List<Drawable> alwaysOnScreenRenderables;

	private List<TemporaryNotification> postLoad;

	private List<TemporaryNotification> activeNotifications = new List<TemporaryNotification>();

	private DrawableLabel padding = new DrawableLabel("");

	private GameOptionsWindow optionsWnd;

	private ModificadorDeBool m_canHideCursor;

	private bool loadingComplete;

	private HashSet<GenericUserPanelBase> visibleWindows = new HashSet<GenericUserPanelBase>();

	private HashSet<GenericUserPanelBase> initialWindows = new HashSet<GenericUserPanelBase>();

	private List<GenericFlotanteUserPanel> floatingWindows = new List<GenericFlotanteUserPanel>();

	private bool overlayHidden;

	private UIToolkitInitializer uiTK;

	private Camera lastMainCamera;

	private DispatcherService dispatcher;

	private IInputHandle hotkey;

	private OverlayService mainService;

	public ConfigEntry<KeyboardShortcut> HideOverlayHotkey { get; internal set; }

	public ISet<ScopeSupport> CursorHolders { get; internal set; }

	public bool HasVisibleWindows => visibleWindows.Count > 0;

	public SessionOverlayRenderService(List<Drawable> renderables, List<TemporaryNotification> notifications, List<Drawable> alwaysOnScreenRenderables)
	{
		this.renderables = renderables;
		postLoad = notifications;
		this.alwaysOnScreenRenderables = alwaysOnScreenRenderables;
		lastMainCamera = Camera.main;
	}

	public override void OnStart()
	{
		mainService = Lookup<OverlayService>();
		mainService.SessionService = this;
		StartUITK();
		dispatcher = Lookup<DispatcherService>();
		dispatcher.DrawGUI.Add(OnRender, base.Scope);
		dispatcher.DoUpdate.Add(OnUpdate, base.Scope);
		hotkey = dispatcher.Input.KeyboardEvent(HideOverlayHotkey, base.Scope);
		SMAGlobalPatches.OnLoaderScreenUpdate.Add(OnLoaderScreenUpdate, base.Scope);
		optionsWnd = Lookup<GameOptionsWindow>();
		m_canHideCursor = Singleton<ConfiguracionGeneralDeMouse>.instance.canHideCursorModificableAnd.ObtenerModificadorNotNull((UnityEngine.Object)(object)Lookup<Plugin>());
		initialWindows = SubscribeToAllLargePanels(base.Scope);
		base.Session.OnGuestReady += Session_OnGuestReady;
		FindFloatingWindows();
	}

	private void FindFloatingWindows()
	{
		floatingWindows.Clear();
		PanelBase[] array = UnityEngine.Object.FindObjectsOfType<PanelBase>();
		foreach (PanelBase panel in array)
		{
			GenericFlotanteUserPanel panelbase = panel.GetComponent<GenericFlotanteUserPanel>();
			if (panelbase != null)
			{
				floatingWindows.Add(panelbase);
			}
		}
	}

	private void StartUITK()
	{
		UITKManagedPanel panel = mainService.RequestPanel(1280, 720);
		foreach (Drawable x in renderables)
		{
			if (x.NativeComponent != null && x.EnableNative)
			{
				panel.GameView.Add(x.NativeComponent);
			}
		}
	}

	private void Session_OnGuestReady(GuestCharacter obj)
	{
		SubscribeToAllLargePanels(base.Session.Guest.Scope, initialWindows);
	}

	private HashSet<GenericUserPanelBase> SubscribeToAllLargePanels(ScopeSupport scope, HashSet<GenericUserPanelBase> except = null)
	{
		HashSet<GenericUserPanelBase> windows = new HashSet<GenericUserPanelBase>();
		PanelBase[] array = UnityEngine.Object.FindObjectsOfType<PanelBase>();
		foreach (PanelBase panel in array)
		{
			GenericUserPanelBase panelbase = panel.GetComponent<GenericUserPanelBase>();
			if ((except == null || !except.Contains(panelbase)) && panelbase is GenericUserPanelOnMainCanvas)
			{
				scope.EventHandler(delegate(Action<GenericUserPanelBase> h)
				{
					panelbase.showed += h;
				}, delegate(Action<GenericUserPanelBase> h)
				{
					panelbase.showed -= h;
				}, LargePanel_stateChanged);
				scope.EventHandler(delegate(Action<GenericUserPanelBase> h)
				{
					panelbase.hided += h;
				}, delegate(Action<GenericUserPanelBase> h)
				{
					panelbase.hided -= h;
				}, LargePanel_stateChanged);
				windows.Add(panelbase);
			}
		}
		return windows;
	}

	private void LargePanel_stateChanged(GenericUserPanelBase obj)
	{
		if (obj.isShowing)
		{
			visibleWindows.Add(obj);
		}
		else
		{
			visibleWindows.Remove(obj);
		}
	}

	public override void OnStop()
	{
		activeNotifications.ForEach(RemoveNotification);
		if (uiTK != null)
		{
			uiTK.Dispose();
		}
		if (mainService != null)
		{
			mainService.SessionService = null;
		}
		base.OnStop();
	}

	private void OnUpdate()
	{
		dispatcher.Input.Enabled = !optionsWnd.Visible && !base.Session.Modal.Visible;
		if (optionsWnd.Visible)
		{
			return;
		}
		if (!lastMainCamera.isActiveAndEnabled)
		{
			RebindCamera();
		}
		if (loadingComplete && postLoad.Count > 0)
		{
			DrainNotifications();
		}
		activeNotifications.RemoveIf(delegate(TemporaryNotification n)
		{
			n._timer += Time.deltaTime;
			if (n.Duration <= n._timer)
			{
				RemoveNotification(n);
				return true;
			}
			if (n.Duration - n._timer < n.FadeOut)
			{
				n.Drawable.Transparency = (n.Duration - n._timer) / n.FadeOut;
			}
			else if (n.FadeIn > 0f && n._timer < n.FadeIn)
			{
				n.Drawable.Transparency = Mathf.Min(1f, n._timer / n.FadeIn);
			}
			return false;
		});
		if (hotkey.Down)
		{
			overlayHidden = !overlayHidden;
		}
		if (uiTK != null)
		{
			uiTK.SetVisible(!overlayHidden);
		}
	}

	private void RebindCamera()
	{
		lastMainCamera = Camera.main;
		foreach (GenericFlotanteUserPanel wnd in floatingWindows)
		{
			if (wnd.isBinded)
			{
				Canvas c = wnd.GetComponentInChildren<Canvas>();
				if (c != null)
				{
					c.worldCamera = lastMainCamera;
				}
			}
		}
	}

	private void RemoveNotification(TemporaryNotification n)
	{
		if (n.SpecificContainer == null)
		{
			renderables.Remove(n.Drawable);
		}
		else
		{
			n.SpecificContainer.Remove(n.Drawable);
		}
	}

	private void OnLoaderScreenUpdate(bool obj)
	{
		if (!obj)
		{
			loadingComplete = true;
		}
	}

	private void DrainNotifications()
	{
		activeNotifications.AddRange(postLoad);
		postLoad.Clear();
		activeNotifications.ForEach(delegate(TemporaryNotification n)
		{
			if (n.SpecificContainer == null)
			{
				renderables.Add(n.Drawable);
			}
			else
			{
				n.SpecificContainer.Add(n.Drawable);
			}
		});
	}

	private void OnRender()
	{
		if (!base.Session.Modal.Visible)
		{
			DrawContext dc = new DrawContext(new Rect(0f, 0f, 1280f, 720f));
			DrawContext dcScreen = new DrawContext(new Rect(0f, 0f, Screen.width, Screen.height));
			RenderList(dc, dcScreen, alwaysOnScreenRenderables);
			if (!optionsWnd.Visible && visibleWindows.Count == 0 && !overlayHidden)
			{
				RenderList(dc, dcScreen, renderables);
			}
			bool drawCursor = dc.DrawCursor || dcScreen.DrawCursor;
			m_canHideCursor.valor.valor = !drawCursor && CursorHolders.Count == 0;
		}
	}

	private void RenderList(DrawContext rc, DrawContext screen, List<Drawable> list)
	{
		foreach (Drawable r in list)
		{
			rc.Native = r.EnableNative && r.NativeComponent != null;
			rc.NativeCached = rc.Native && !r.Dirty;
			if (rc.NativeCached)
			{
				continue;
			}
			rc.Begin();
			try
			{
				if (rc.Native)
				{
					r.Draw(rc);
				}
				else
				{
					r.Draw(screen);
				}
			}
			finally
			{
				rc.Complete();
			}
		}
	}
}
