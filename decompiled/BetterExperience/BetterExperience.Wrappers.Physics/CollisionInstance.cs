using Assets._ReusableScripts.CuchiCuchi.Skins;

namespace BetterExperience.Wrappers.Physics;

internal class CollisionInstance
{
	public HitSkin.Colision Instance { get; private set; }

	public CollisionInstance(HitSkin.Colision instance)
	{
		Instance = instance;
	}
}
