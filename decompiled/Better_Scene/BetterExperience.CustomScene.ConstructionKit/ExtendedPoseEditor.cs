using Assets.Base.Bones.Gizmos.Runtime;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class ExtendedPoseEditor : SessionService
{
	private class ToolWindow : PopupWindow
	{
		private TabPanel tabs;

		public TabPanel Tabs => tabs;

		public ToolWindow()
		{
			tabs = new TabPanel();
			Add(tabs);
			text = "Scene tool";
			base.style.width = 350f;
			base.style.height = 350f;
			base.style.top = 150f;
			base.style.left = 10f;
			tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;
		}

		private void Tabs_SelectedIndexChanged()
		{
			InvalidateSize();
		}

		public void InvalidateSize()
		{
			if (tabs.SelectedIndex == -1)
			{
				base.style.width = 110f;
			}
			else
			{
				base.style.width = 350f;
			}
		}
	}

	private SkeletonEditorMode skeletonEditor;

	private InteractionManager interactionManager;

	private ToolWindow toolWindow;

	private IInputHandle toggleEditorKey;

	private ConfigEntry<Vector2> toolWindowOffset;

	public Observable<SkeletonEditorMode> OnStateChanged { get; } = new Observable<SkeletonEditorMode>();

	public ConfigEntry<KeyboardShortcut> StartCustomPoseHotkey { get; set; }

	public override void OnInit()
	{
		base.OnInit();
		ConfigFile cfg = base.Scope.Lookup<ConfigFile>();
		toolWindowOffset = cfg.Bind<Vector2>("Windows", "ToolWindowOffset", Vector2.zero, (ConfigDescription)null);
	}

	public override void OnStart()
	{
		base.OnStart();
		skeletonEditor = Singleton<SkeletonEditorMode>.instance;
		skeletonEditor.onModoChanged += Instance_onModoChanged;
		interactionManager = Lookup<InteractionManager>();
		OverlayService overlay = Lookup<OverlayService>();
		VisualElement uiRoot = Lookup<CustomSceneFeature>().EditorUiPanel.GameView;
		uiRoot.Add(toolWindow = new ToolWindow());
		UIBuilder.Hide((VisualElement)toolWindow);
		UIBuilder.EnableWindowDrag((PopupWindow)toolWindow);
		base.Scope.AddService(new BonePanelService());
		base.Scope.AddService(new PointOfInterestPanelService());
		base.Scope.AddService(new PosturePanelService());
		base.Scope.AddService(new ClipEditorPanelService());
		base.Scope.AddService(new AnimationsPanelService());
		base.Scope.AddService(new ExpressionEditorPanelService());
		base.Scope.OnDispose += delegate
		{
			uiRoot.Remove(toolWindow);
		};
		DispatcherService dispatcher = Lookup<DispatcherService>();
		toggleEditorKey = dispatcher.Input.KeyboardEvent(StartCustomPoseHotkey, base.Scope);
		dispatcher.DoUpdate.Add(delegate
		{
			if (toggleEditorKey.Up)
			{
				if (interactionManager.AnimationController.ChangingState)
				{
					overlay.InfoMessage("Please wait, puppet is not ready");
				}
				else if (interactionManager.HasActiveInteraction)
				{
					overlay.InfoMessage("Please wait, interaction is in progress");
				}
				else if (!interactionManager.IsEditorActive)
				{
					GizmosDeSkeleton skeleton = interactionManager.AnimationController.Skeleton;
					skeletonEditor.ActivarEnSkeleton(skeleton, false, false);
					overlay.InfoMessage("Starting editor");
				}
				else
				{
					skeletonEditor.DesactivarParaTodos(false);
					interactionManager.AnimationController.InterruptPose("Hotkey");
					overlay.InfoMessage("Exiting editor");
				}
			}
		}, base.Scope);
	}

	public void AddTab(string name, VisualElement component, ScopeSupport scope)
	{
		toolWindow.Tabs.AddTab(name, component);
		if (scope != null)
		{
			scope.OnDispose += delegate
			{
				toolWindow.Tabs.RemoveTab(component);
			};
		}
	}

	private void Instance_onModoChanged(SkeletonEditorMode obj)
	{
		bool active = skeletonEditor.activado && obj.skeletonActivos.Count > 0;
		if (active)
		{
			OnStateChanged.Invoke(skeletonEditor);
		}
		else
		{
			OnStateChanged.Invoke(null);
		}
		toolWindow.InvalidateSize();
		UIBuilder.SetVisible((VisualElement)toolWindow, active);
		interactionManager.AnimationController.IKTargeting.EffectorTracking = active;
	}
}
