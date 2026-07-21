using BetterExperience.CustomScene.Packaging;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PointOfInterestData : Stored
{
	public string Name { get; set; }

	public float[] Position { get; set; }

	public float[] Rotation { get; set; }

	public PointOfInterestData()
	{
	}

	public PointOfInterestData(string name, Vector3 position, Quaternion rotation)
	{
		Name = name;
		Position = new float[3] { position.x, position.y, position.z };
		Rotation = new float[4] { rotation.x, rotation.y, rotation.z, rotation.w };
	}

	public void Apply(Transform t)
	{
		t.position = new Vector3(Position[0], Position[1], Position[2]);
		t.rotation = new Quaternion(Rotation[0], Rotation[1], Rotation[2], Rotation[3]);
	}
}
