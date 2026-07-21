namespace BetterExperience.CustomScene.Poser;

public class IK2Data
{
	public IK2EffectorData footLeft { get; set; }

	public IK2EffectorData footRight { get; set; }

	public IK2EffectorData handLeft { get; set; }

	public IK2EffectorData handRight { get; set; }

	public IK2EffectorData playerRoot { get; set; }

	public IK2EffectorData shoulderLeft { get; set; }

	public IK2EffectorData shoulderRight { get; set; }

	public IK2Data()
	{
	}

	public IK2Data(EffectorData effectorData)
	{
		if (effectorData.FootLeft != null)
		{
			footLeft = new IK2EffectorData(effectorData.FootLeft);
		}
		if (effectorData.FootRight != null)
		{
			footRight = new IK2EffectorData(effectorData.FootRight);
		}
		if (effectorData.HandLeft != null)
		{
			handLeft = new IK2EffectorData(effectorData.HandLeft);
		}
		if (effectorData.HandRight != null)
		{
			handRight = new IK2EffectorData(effectorData.HandRight);
		}
		if (effectorData.PlayerRoot != null)
		{
			playerRoot = new IK2EffectorData(effectorData.PlayerRoot);
		}
		if (effectorData.ShoulderLeft != null)
		{
			shoulderLeft = new IK2EffectorData(effectorData.ShoulderLeft);
		}
		if (effectorData.ShoulderRight != null)
		{
			shoulderRight = new IK2EffectorData(effectorData.ShoulderRight);
		}
	}

	internal EffectorData AsEffectorData()
	{
		EffectorData ed = new EffectorData();
		ed.FootLeft = Convert(footLeft);
		ed.FootRight = Convert(footRight);
		ed.HandLeft = Convert(handLeft);
		ed.HandRight = Convert(handRight);
		ed.PlayerRoot = Convert(playerRoot);
		ed.ShoulderLeft = Convert(shoulderLeft);
		ed.ShoulderRight = Convert(shoulderRight);
		return ed;
	}

	private EffectorOverride Convert(IK2EffectorData data)
	{
		return data?.AsEffectorOverride();
	}
}
