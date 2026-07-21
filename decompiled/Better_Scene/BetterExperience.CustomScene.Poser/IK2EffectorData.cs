using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class IK2EffectorData
{
	public string anchor { get; set; }

	public bool enableAngle { get; set; }

	public bool enableOffset { get; set; }

	public float[] angle { get; set; }

	public float[] offset { get; set; }

	public IK2EffectorData()
	{
	}

	public IK2EffectorData(EffectorOverride data)
	{
		anchor = data.Anchor;
		enableAngle = data.EnableAngle;
		enableOffset = data.EnableOffset;
		angle = data.Angle.AsFloatArray();
		offset = data.Offset.AsFloatArray();
	}

	public EffectorOverride AsEffectorOverride()
	{
		EffectorOverride e = new EffectorOverride();
		e.Anchor = anchor;
		e.EnableAngle = enableAngle;
		e.EnableOffset = enableOffset;
		e.Angle = ((angle != null) ? angle.AsQuaternion() : Quaternion.identity);
		e.Offset = ((offset != null) ? offset.AsVector3() : Vector3.zero);
		return e;
	}
}
