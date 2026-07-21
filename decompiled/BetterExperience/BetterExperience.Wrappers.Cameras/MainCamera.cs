using UnityEngine;

namespace BetterExperience.Wrappers.Cameras;

public class MainCamera
{
	private Camera cachedCamera;

	private bool cachedIndoorFlag;

	public Vector3 Forward => Camera.main.transform.forward;

	public bool IsPov => IsIndoorCam();

	public void Translate(Vector3 translate)
	{
		Camera.main.transform.Translate(translate);
	}

	private bool IsIndoorCam()
	{
		if (cachedCamera != Camera.main)
		{
			cachedCamera = Camera.main;
			cachedIndoorFlag = Camera.main.name.Contains("Indoor");
		}
		return cachedIndoorFlag;
	}

	public Vector3 WorldToScreen(Vector3 vector)
	{
		return Camera.main.WorldToScreenPoint(vector);
	}

	public void Reset()
	{
		Camera.main.transform.localPosition = Vector3.zero;
	}
}
