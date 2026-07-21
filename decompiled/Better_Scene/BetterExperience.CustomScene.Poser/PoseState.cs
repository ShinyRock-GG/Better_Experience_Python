namespace BetterExperience.CustomScene.Poser;

public struct PoseState
{
	public SpartialData hip;

	public SpartialData leftHand;

	public SpartialData rightHand;

	public SpartialData leftFoot;

	public SpartialData rightFoot;

	public SpartialData head;

	public new string ToString()
	{
		string[] obj = new string[13]
		{
			"{\nhip: ", null, null, null, null, null, null, null, null, null,
			null, null, null
		};
		SpartialData spartialData = hip;
		obj[1] = spartialData.ToString();
		obj[2] = "\nhead: ";
		spartialData = head;
		obj[3] = spartialData.ToString();
		obj[4] = "\nlHand: ";
		spartialData = leftHand;
		obj[5] = spartialData.ToString();
		obj[6] = "\nrHand: ";
		spartialData = rightHand;
		obj[7] = spartialData.ToString();
		obj[8] = "\nlFoot: ";
		spartialData = leftFoot;
		obj[9] = spartialData.ToString();
		obj[10] = "\nrFoot: ";
		spartialData = rightFoot;
		obj[11] = spartialData.ToString();
		obj[12] = "}";
		return string.Concat(obj);
	}
}
