using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public interface IBoneDisposition
{
	Vector3 HipOffset { get; set; }

	int Count { get; }

	Vector3 RootOffset { get; set; }

	Quaternion RootRotation { get; set; }

	[Timed]
	Quaternion GetRotation(int index);

	void SetRotation(int index, Quaternion value);
}
