using BetterExperience.CustomScene.Packaging;

namespace BetterExperience.CustomScene.Poser;

public class PointOfInterestDescriptor : Stored
{
	public string ParentPoi { get; set; }

	public string DisplayName { get; set; }

	public string LocalDisplayName { get; set; }

	public bool IsWaypoint { get; set; }
}
