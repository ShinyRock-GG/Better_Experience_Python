using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;

namespace BetterExperience.CustomScene.Poser;

public class CurrentPlace
{
	public PointOfInterest POI { get; }

	public PoseOrientation Orientation { get; }

	public GoToScenaManager.GoTo NativeGoto { get; }

	public CurrentPlace(PointOfInterest point, PoseOrientation orientation, GoToScenaManager.GoTo gt)
	{
		POI = point;
		Orientation = orientation;
		NativeGoto = gt;
	}
}
