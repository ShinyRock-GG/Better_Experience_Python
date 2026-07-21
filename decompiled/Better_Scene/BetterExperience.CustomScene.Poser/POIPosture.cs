namespace BetterExperience.CustomScene.Poser;

public class POIPosture : Posture
{
	public string PoiId => base.Id.Split(new char[1] { '.' })[1];

	public string PostureId => base.Id.Split(new char[1] { '.' })[0];

	public override bool Is(Posture posture)
	{
		if (posture != null && posture.Id == base.Id)
		{
			return true;
		}
		return base.Poses.Posture.Is(posture);
	}
}
