namespace BetterExperience.CustomScene.Poser;

public class EffectorData
{
	public EffectorOverride HandLeft { get; set; }

	public EffectorOverride HandRight { get; set; }

	public EffectorOverride FootLeft { get; set; }

	public EffectorOverride FootRight { get; set; }

	public EffectorOverride ShoulderLeft { get; set; }

	public EffectorOverride ShoulderRight { get; set; }

	public EffectorOverride PlayerRoot { get; set; }

	public EffectorData()
	{
	}

	public EffectorData(EffectorData effectorData)
	{
		HandLeft = effectorData.HandLeft;
		HandRight = effectorData.HandRight;
		FootLeft = effectorData.FootLeft;
		FootRight = effectorData.FootRight;
		PlayerRoot = effectorData.PlayerRoot;
		ShoulderLeft = effectorData.ShoulderLeft;
		ShoulderRight = effectorData.ShoulderRight;
	}
}
