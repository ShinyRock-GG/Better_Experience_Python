using BetterExperience.Utils;

namespace BetterExperience.CustomScene.Poser;

public class IKTargetingData
{
	public string Target { get; set; }

	public float[] LocalPosition { get; set; }

	public float[] LocalRotation { get; set; }

	public IKTargetingData()
	{
	}

	public IKTargetingData(DynamicIKTarget dynamicIK)
	{
		Target = dynamicIK.Target;
		LocalPosition = dynamicIK.LocalPosition.AsFloatArray();
		LocalRotation = dynamicIK.LocalRotation.AsFloatArray();
	}

	internal DynamicIKTarget Convert()
	{
		return new DynamicIKTarget
		{
			Target = Target,
			LocalPosition = LocalPosition.AsVector3(),
			LocalRotation = LocalRotation.AsQuaternion()
		};
	}
}
