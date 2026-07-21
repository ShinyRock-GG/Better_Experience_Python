using System;
using Assets;
using Assets._ReusableScripts.Globales.Mapas;
using BepInEx.Configuration;
using BetterExperience.Wrappers.Characters;
using UnityEngine;

namespace BetterExperience.Features.SceneCameras;

internal class CameraRig : IDisposable
{
	private const float ACCEPTABLE_PIVOT_RAYCAST_DISTANCE = 2f;

	private const float ROTATION_SMOOTHNESS = 5000000f;

	private const float TRANSLATION_SMOOTHNESS = 10f;

	private const float MINIMAL_LINECAST_DISTANCE = 0f;

	private const float PIVOT_LINECAST_PRECISSION = 0f;

	private Logger logger = new Logger("Camera Rig");

	private PlayerCharacter player;

	private Camera mainCamera;

	private GameObject shadowGo;

	private Camera shadowCamera;

	private int cameraRaycastLayerMask;

	private CameraSettings currentSettings;

	private Transform rigTransform;

	public Transform RootTransform { get; }

	public Transform ConnectorTransform { get; }

	public Transform PivotTransform { get; }

	public ConfigEntry<bool> UseCameraLinecast { get; set; }

	public ConfigEntry<bool> UseRagdollPivot { get; set; }

	private Transform CameraTarget { get; set; }

	public ConfigEntry<bool> UseDetachedRig { get; internal set; }

	private Transform NewTransform(string name, Transform parent)
	{
		GameObject go = new GameObject(name);
		go.transform.parent = parent;
		return go.transform;
	}

	public CameraRig(PlayerCharacter player)
	{
		this.player = player;
		mainCamera = Camera.main;
		rigTransform = NewTransform("Shadow Rig", player.Character.animatorRootMotionTransform);
		RootTransform = NewTransform("Shadow Root", rigTransform);
		ConnectorTransform = NewTransform("Shadow Root Connector", RootTransform);
		shadowGo = UnityEngine.Object.Instantiate(Camera.main.gameObject, ConnectorTransform);
		shadowCamera = shadowGo.GetComponent<Camera>();
		shadowCamera.enabled = false;
		shadowCamera.name = "Shadow Camera";
		shadowGo.SetActive(value: false);
		ConfiguracionGlobal.Layers layers = MapaSingleton<ConfiguracionGlobal>.instance.layers;
		cameraRaycastLayerMask = layers.skins.ToLayerMask() | layers.convexSkins.ToLayerMask() | layers.ragdoll.ToLayerMask();
		Component[] componentsInChildren = shadowGo.GetComponentsInChildren<Component>();
		foreach (Component c in componentsInChildren)
		{
			if (c is Rigidbody)
			{
				UnityEngine.Object.Destroy(c);
			}
			else if (c is SphereCollider && c.name == "Collider")
			{
				UnityEngine.Object.Destroy(c);
			}
		}
		PivotTransform = NewTransform("Shadow Pivot", ConnectorTransform);
		CameraTarget = NewTransform("Camera Target", ConnectorTransform);
		PivotTransform.gameObject.SetActive(value: false);
	}

	public void SetShadowCameraEnabled(bool state)
	{
		if (shadowCamera.enabled != state)
		{
			shadowCamera.enabled = state;
			mainCamera.enabled = !state;
			shadowGo.SetActive(state);
			if (state)
			{
				shadowCamera.transform.position = mainCamera.transform.position;
				shadowCamera.transform.rotation = mainCamera.transform.rotation;
				CameraTarget.position = shadowCamera.transform.position;
				CameraTarget.rotation = shadowCamera.transform.rotation;
			}
		}
	}

	internal void Translate(Vector3 translate)
	{
		CameraTarget.transform.Translate(translate);
	}

	public void UpdateDistanceFactor(float dv)
	{
		float currentDistance = Vector3.Distance(PivotTransform.position, CameraTarget.position);
		float newDistance = currentDistance + dv;
		newDistance = Mathf.Clamp(newDistance, 0.1f, 2f);
		Vector3 delta = new Vector3(0f, 0f, 0f - newDistance);
		Vector3 newPosition = PivotTransform.position + CameraTarget.rotation * delta;
		CameraTarget.position = newPosition;
	}

	public CameraSettings GetSettings()
	{
		CameraSettings settings = new CameraSettings();
		Transform t = shadowCamera.transform;
		settings.Position = new float[3]
		{
			t.localPosition.x,
			t.localPosition.y,
			t.localPosition.z
		};
		settings.Rotation = new float[4]
		{
			t.localRotation.x,
			t.localRotation.y,
			t.localRotation.z,
			t.localRotation.w
		};
		if (currentSettings != null)
		{
			settings.Name = currentSettings.Name;
		}
		return settings;
	}

	public void SetSettings(CameraSettings settings, bool pivotEnabled)
	{
		if (settings != null)
		{
			Transform t = shadowCamera.transform;
			t.localPosition = new Vector3(settings.Position[0], settings.Position[1], settings.Position[2]);
			t.localRotation = new Quaternion(settings.Rotation[0], settings.Rotation[1], settings.Rotation[2], settings.Rotation[3]);
			CameraTarget.localPosition = t.localPosition;
			CameraTarget.localRotation = t.localRotation;
		}
		PivotTransform.gameObject.SetActive(pivotEnabled);
		if (pivotEnabled)
		{
			UpdatePivot();
		}
		else
		{
			SetRootMotionEnabled(state: true);
		}
		currentSettings = settings;
	}

	private void SetRootMotionEnabled(bool state)
	{
		if (!UseDetachedRig.Value)
		{
			state = true;
		}
		if (state)
		{
			ConnectorTransform.parent = RootTransform.parent;
		}
		else
		{
			ConnectorTransform.parent = null;
		}
	}

	public void SetPivot(Vector3 anchor, bool rootMotion)
	{
		PivotTransform.position = anchor;
		SetRootMotionEnabled(rootMotion);
	}

	public void Rotate2D(float dx, float dy)
	{
		Rotate2DInternal(dx, dy);
	}

	public void PivotRotate2D(float dx, float dy)
	{
		RotateAround2D(PivotTransform.position, dx, dy);
	}

	private void RotateAround2D(Vector3 anchor, float dx, float dy)
	{
		Transform transform = CameraTarget;
		float distance = Vector3.Distance(PivotTransform.position, CameraTarget.position);
		Rotate2DInternal(dx, dy);
		Vector3 negDistance = new Vector3(0f, 0f, 0f - distance);
		Vector3 position = transform.rotation * negDistance + anchor;
		CameraTarget.position = position;
	}

	internal void Rotate2DInternal(float dx, float dy)
	{
		Vector3 angles = CurrentEuler();
		angles.y += dx;
		angles.x += dy;
		angles.x = Mathf.Clamp(angles.x, -89f, 89f);
		CameraTarget.localRotation = Quaternion.Slerp(CameraTarget.localRotation, Quaternion.Euler(angles), 5000000f * Time.deltaTime);
	}

	private Vector3 CurrentEuler()
	{
		Vector3 euler = CameraTarget.localEulerAngles;
		NormalizeEuler(ref euler);
		return euler;
	}

	public void NormalizeEuler(ref Vector3 rEuler)
	{
		if (rEuler.x < -180f)
		{
			rEuler.x += 360f;
		}
		else if (rEuler.x > 180f)
		{
			rEuler.x -= 360f;
		}
		if (rEuler.y < -180f)
		{
			rEuler.y += 360f;
		}
		else if (rEuler.y > 180f)
		{
			rEuler.y -= 360f;
		}
	}

	public bool RagdollRaycast(out Vector3 point)
	{
		if (Physics.Raycast(new Ray(CameraTarget.position, CameraTarget.forward), out var hit, 2f, cameraRaycastLayerMask))
		{
			point = hit.point;
			return true;
		}
		point = Vector3.zero;
		return false;
	}

	private (Vector3, bool) ComputeRotationPivot()
	{
		if (UseRagdollPivot.Value && RagdollRaycast(out var hitpoint))
		{
			return (hitpoint, false);
		}
		Plane verticalPlane = new Plane(RootTransform.right, RootTransform.position);
		Plane horizontalPlane = new Plane(RootTransform.up, player.Character.pene.lookAtTarget.position);
		Ray ray = new Ray(CameraTarget.position, CameraTarget.forward);
		if (verticalPlane.Raycast(ray, out var hit) && hit <= 2f)
		{
			return (ray.GetPoint(hit), true);
		}
		if (horizontalPlane.Raycast(ray, out hit) && hit <= 2f)
		{
			return (ray.GetPoint(hit), true);
		}
		return (RootTransform.position, true);
	}

	public void Dispose()
	{
		if (rigTransform != null)
		{
			if (ConnectorTransform.parent == null)
			{
				UnityEngine.Object.Destroy(ConnectorTransform.gameObject);
			}
			UnityEngine.Object.Destroy(rigTransform.gameObject);
			rigTransform = null;
		}
	}

	internal void UpdatePivot()
	{
		var (pivot, rootmotion) = ComputeRotationPivot();
		SetPivot(pivot, rootmotion);
	}

	internal void SyncRootTransform()
	{
		Transform head = player.Character.bones.head.transform;
		if (RootTransform.position != head.position)
		{
			RootTransform.position = head.position;
			if (PivotTransform.gameObject.activeSelf)
			{
				PivotRotate2D(0f, 0f);
			}
		}
		if (CameraTarget.localRotation != shadowCamera.transform.localRotation)
		{
			shadowCamera.transform.localRotation = CameraTarget.localRotation;
		}
		if (!(CameraTarget.localPosition != shadowCamera.transform.localPosition))
		{
			return;
		}
		if (PivotTransform.gameObject.activeSelf)
		{
			float distance = Vector3.Distance(PivotTransform.position, CameraTarget.position);
			if (UseCameraLinecast.Value && Physics.Linecast(PivotTransform.position, CameraTarget.position, out var hit, cameraRaycastLayerMask) && hit.distance > 0f)
			{
				distance = hit.distance;
			}
			Vector3 negDistance = new Vector3(0f, 0f, 0f - distance);
			Vector3 position = CameraTarget.rotation * negDistance + PivotTransform.position;
			if (!ExtendedMonoBehaviour.AlmostEqual(shadowCamera.transform.position, position, 0f))
			{
				shadowCamera.transform.position = Vector3.Lerp(shadowCamera.transform.position, position, Time.deltaTime * 10f);
			}
		}
		else
		{
			shadowCamera.transform.position = Vector3.Lerp(shadowCamera.transform.position, CameraTarget.position, Time.deltaTime * 10f);
		}
	}
}
