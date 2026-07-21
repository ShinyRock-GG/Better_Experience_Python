using BetterExperience.Utils;

namespace BetterExperience.CustomScene.Poser;

public struct SpartialData
{
	public Direction forward;

	public Direction4 hip;

	public override string ToString()
	{
		string[] obj = new string[6]
		{
			"f: ",
			forward.ToString(),
			" hip-m: ",
			hip.main.ToString(),
			" hip-xyz: ",
			null
		};
		BitMask<Direction> xyz = hip.xyz;
		obj[5] = xyz.ToString();
		return string.Concat(obj);
	}
}
