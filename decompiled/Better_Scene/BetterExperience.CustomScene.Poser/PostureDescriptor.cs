using BetterExperience.CustomScene.Packaging;

namespace BetterExperience.CustomScene.Poser;

public class PostureDescriptor : Stored
{
	public string DisplayName { get; set; }

	public string CancelDisplayName { get; set; }

	public string ParentPosture { get; set; }

	public PoseOrientation Orientation { get; set; } = PoseOrientation.UNIVERSAL;
}
