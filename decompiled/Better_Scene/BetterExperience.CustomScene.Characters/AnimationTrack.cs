using UnityEngine;

namespace BetterExperience.CustomScene.Characters;

public class AnimationTrack
{
	public int[] Indices { get; set; }

	public AnimPropertyType PropType { get; set; }

	public Vector3[] Vectors { get; set; }

	public Quaternion[] Quaternions { get; set; }

	public float[] Floats { get; set; }
}
