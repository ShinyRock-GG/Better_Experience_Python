using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

public abstract class AbstractEditorPlugin : VisualElement
{
	public ScopeSupport Scope { get; } = new ScopeSupport();

	public abstract string ToolName { get; }

	public VisualElement GameView { get; private set; }

	public ClipEditorPanelService Service { get; private set; }

	public InteractionManager InteractionManager { get; private set; }

	public GameSession Session { get; private set; }

	public PoseAnimationClip Clip => Service.Model.Clip;

	public AbstractEditorPlugin()
	{
		Scope.OnStart += InitializeServices;
		Scope.Name = "EditorPlugin." + GetType().Name;
	}

	public virtual void InitializeServices()
	{
		GameView = Scope.Lookup<CustomSceneFeature>().EditorUiPanel.GameView;
		Service = Scope.Lookup<ClipEditorPanelService>();
		Service.RegisterTool(ToolName, this, Scope);
		InteractionManager = Scope.Lookup<InteractionManager>();
		Service.OnClipOpened.Add(OnClipOpened, Scope);
		Service.OnClipClosed.Add(OnClipClosed, Scope);
		Service.OnClipSaving.Add(OnClipSaving, Scope);
		Session = Scope.Lookup<SessionTracker>().Current;
	}

	protected virtual void OnClipSaving()
	{
	}

	protected virtual void OnClipOpened()
	{
		Service.Model.SelectedFrameChanged += OnSelectedFrameChanged;
	}

	protected virtual void OnSelectedFrameChanged()
	{
	}

	protected virtual void OnClipClosed()
	{
	}
}
