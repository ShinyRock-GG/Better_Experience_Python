using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class EffectorOverride
{
	public string Anchor { get; set; }

	public Vector3 Offset { get; set; }

	public Quaternion Angle { get; set; }

	public bool EnableOffset { get; set; }

	public bool EnableAngle { get; set; }
}
