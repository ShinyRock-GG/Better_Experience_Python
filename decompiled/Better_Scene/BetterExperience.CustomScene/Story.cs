using BetterExperience.CustomScene.Packaging;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene;

public class Story
{
	public ScopeSupport Scope { get; } = new ScopeSupport();

	public ScopeSupport SceneScope { get; set; }

	public ScopeSupport SceneInterviewScope { get; set; }

	public Package MainPackage { get; }

	public VirtIO VFS { get; }

	public Observable SceneScopeCreated { get; } = new Observable();

	public Observable InterviewScopeCreated { get; } = new Observable();

	public Story(Package package, VirtIO virtIO)
	{
		MainPackage = package;
		VFS = virtIO;
	}
}
