using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public struct TransformDisposition(Vector3 position, Quaternion rotation)
{
	public Vector3 Position { get; set; } = position;

	public Quaternion Rotation { get; set; } = rotation;

	public TransformDisposition(Transform t)
		: this(t.position, t.rotation)
	{
	}

	public void Apply(Transform t)
	{
		t.position = Position;
		t.rotation = Rotation;
	}
}
