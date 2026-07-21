using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using UnityEngine;

namespace BetterExperience.Features.SceneCameras;

internal class SceneCameraService : SessionService
{
	private CameraMode _cameraMode;

	private PoseCameraRegistry registry;

	private CameraSettings currentCameraSettings;

	private List<CameraSettings> availableCameras = new List<CameraSettings>();

	private DrawableLabel uiOnR;

	private DrawableLabel uiOnInsert;

	private DrawableLabel uiOnX;

	private DrawableLabel uiOnDelete;

	private DrawableLabel uiCameraList;

	private CameraRig cameraRig;

	private DrawableLabel cameraModeLabel;

	public IInputHandle Forward { get; set; }

	public IInputHandle Backward { get; set; }

	public IInputHandle Left { get; set; }

	public IInputHandle Right { get; set; }

	public IInputHandle Up { get; set; }

	public IInputHandle Down { get; set; }

	public IInputHandle EditKey { get; set; }

	public IInputHandle SwitchKey { get; set; }

	public IInputHandle SaveKey { get; set; }

	public IInputHandle DeleteKey { get; set; }

	public IInputHandle AltKey { get; set; }

	public bool SceneCamMode { get; set; } = true;

	private CameraMode CameraMode
	{
		get
		{
			return _cameraMode;
		}
		set
		{
			if (value == CameraMode.DIRECTOR || value == CameraMode.CUSTOM)
			{
				base.Session.Player.LocomotionEnabled = false;
				base.Session.Player.ActionsEnabled = value == CameraMode.CUSTOM;
				base.Session.Player.CameraLookEnabled = false;
				cameraRig.SetShadowCameraEnabled(state: true);
				cameraRig.SetSettings(cameraRig.GetSettings(), value == CameraMode.CUSTOM);
			}
			else
			{
				base.Session.Player.LocomotionEnabled = true;
				base.Session.Player.ActionsEnabled = true;
				base.Session.Player.CameraLookEnabled = true;
				cameraRig.SetShadowCameraEnabled(state: false);
				cameraRig.SetSettings(null, pivotEnabled: false);
				currentCameraSettings = null;
			}
			_cameraMode = value;
		}
	}

	private bool CanSave
	{
		get
		{
			if (CameraMode == CameraMode.DIRECTOR && base.Session.Guest != null)
			{
				return base.Session.Guest.GetCurrentPoseStr() != "";
			}
			return false;
		}
	}

	private bool CanUpdate
	{
		get
		{
			if (CanSave && currentCameraSettings != null)
			{
				return currentCameraSettings.Name != null;
			}
			return false;
		}
	}

	private bool CanDelete
	{
		get
		{
			if (CameraMode == CameraMode.CUSTOM)
			{
				return currentCameraSettings.Name != null;
			}
			return false;
		}
	}

	public ConfigEntry<bool> UpdatePivotWithAlt { get; set; }

	public ConfigEntry<bool> EnableRagdollRaycast { get; set; }

	public ConfigEntry<bool> EnablePivotLinecast { get; set; }

	public ConfigEntry<bool> EnableDetachableRig { get; set; }

	public ConfigEntry<bool> EnableQuickCam { get; internal set; }

	public IInputHandle ShiftKey { get; internal set; }

	public override void OnStart()
	{
		registry = new PoseCameraRegistry(Lookup<PersistenceService>());
		Plugin.DoUpdate.Add(HandleInput, base.Scope);
		base.Session.OnGuestReady += delegate(GuestCharacter guest)
		{
			guest.PoseChanged += RefreshCameraList;
			RefreshCameraList();
		};
		base.Session.OnGuestLeft += delegate
		{
			RefreshCameraList();
			if (CameraMode != CameraMode.POV)
			{
				CameraMode = CameraMode.POV;
			}
		};
		cameraRig = new CameraRig(base.Session.Player);
		cameraRig.UseCameraLinecast = EnablePivotLinecast;
		cameraRig.UseRagdollPivot = EnableRagdollRaycast;
		cameraRig.UseDetachedRig = EnableDetachableRig;
		Lookup<OverlayService>().TopRightPane.Add(CreateGUI(), base.Scope);
		base.Session.Player.OnIkUpdated.Add(cameraRig.SyncRootTransform, base.Scope);
		base.Scope.AddChild(cameraRig);
	}

	private void HandleInput()
	{
		Vector3 hdirection = Vector3.zero;
		Vector3 vdirection = Vector3.zero;
		if (Up.IsHold)
		{
			vdirection += Vector3.up;
		}
		if (Down.IsHold)
		{
			vdirection += Vector3.down;
		}
		if (Forward.IsHold)
		{
			hdirection += Vector3.forward;
		}
		if (Backward.IsHold)
		{
			hdirection += Vector3.back;
		}
		if (Left.IsHold)
		{
			hdirection += Vector3.left;
		}
		if (Right.IsHold)
		{
			hdirection += Vector3.right;
		}
		if (ShiftKey.IsHold)
		{
			hdirection *= 4f;
		}
		if (CameraMode == CameraMode.DIRECTOR)
		{
			if (hdirection != Vector3.zero)
			{
				Vector3 translate = hdirection * Time.deltaTime;
				cameraRig.Translate(translate);
			}
			if (vdirection != Vector3.zero)
			{
				Vector3 translate2 = vdirection * Time.deltaTime;
				cameraRig.Translate(translate2);
			}
			if (CanSave && SaveKey.Up)
			{
				SaveCamera();
			}
			if (CanUpdate && DeleteKey.Up)
			{
				UpdateCamera();
			}
		}
		else if (CameraMode == CameraMode.CUSTOM)
		{
			if (hdirection != Vector3.zero)
			{
				MovePlayer(hdirection);
			}
			if (vdirection != Vector3.zero)
			{
				cameraRig.UpdateDistanceFactor(vdirection.y * Time.deltaTime);
				cameraRig.PivotRotate2D(0f, 0f);
			}
			if (CanDelete && DeleteKey.Up)
			{
				DeleteCamera();
			}
		}
		if (CameraMode == CameraMode.CUSTOM && UpdatePivotWithAlt.Value)
		{
			if (Input.GetMouseButton(1) && AltKey.IsHold)
			{
				cameraRig.PivotTransform.gameObject.SetActive(value: false);
			}
			else
			{
				cameraRig.PivotTransform.gameObject.SetActive(value: true);
			}
		}
		if (CameraMode != CameraMode.POV && Input.GetMouseButton(1))
		{
			float dx = Input.GetAxis("Mouse X");
			float dy = Input.GetAxis("Mouse Y");
			if (CameraMode == CameraMode.DIRECTOR)
			{
				cameraRig.Rotate2D(dx, 0f - dy);
			}
			else if (AltKey.IsHold)
			{
				cameraRig.Rotate2D(dx, 0f - dy);
				if (UpdatePivotWithAlt.Value)
				{
					cameraRig.UpdatePivot();
				}
			}
			else
			{
				cameraRig.PivotRotate2D(dx, 0f - dy);
			}
		}
		if (EditKey.Up)
		{
			ToggleDirectorButton();
		}
		if (SwitchKey.Up)
		{
			ToggleSwitchButton();
		}
		UpdateGUI();
	}

	private void MovePlayer(Vector3 direction)
	{
		direction *= Time.deltaTime * 0.1f;
		if (direction != Vector3.up && direction != Vector3.down)
		{
			base.Session.Player.Move(direction);
		}
		else
		{
			base.Session.Player.AddScale(direction);
		}
	}

	private void RefreshCameraList()
	{
		if (base.Session.Guest != null)
		{
			string pose = base.Session.Guest.GetCurrentPoseStr();
			availableCameras = registry.GetCamerasForPose(pose);
			if (availableCameras == null)
			{
				availableCameras = new List<CameraSettings>();
			}
		}
		else
		{
			availableCameras = new List<CameraSettings>();
		}
		if (currentCameraSettings != null && !availableCameras.Contains(currentCameraSettings) && CameraMode == CameraMode.CUSTOM)
		{
			CameraMode = CameraMode.POV;
		}
	}

	private void SaveCamera()
	{
		CameraSettings settings = cameraRig.GetSettings();
		string pose = base.Session.Guest.GetCurrentPoseStr();
		base.Session.Modal.MessageBoxYesNo("Save New Camera?").OnResult += delegate(bool accept)
		{
			if (accept)
			{
				if (settings.Name != null)
				{
					settings.Name = null;
				}
				registry.Save(pose, settings);
				RefreshCameraList();
				SetCustomCamera(settings);
			}
		};
	}

	private void UpdateCamera()
	{
		CameraSettings settings = cameraRig.GetSettings();
		string pose = base.Session.Guest.GetCurrentPoseStr();
		base.Session.Modal.MessageBoxYesNo("Update camera?").OnResult += delegate(bool accept)
		{
			if (accept)
			{
				registry.Save(pose, settings);
				RefreshCameraList();
				SetCustomCamera(settings);
			}
		};
	}

	private void DeleteCamera()
	{
		CameraSettings settings = cameraRig.GetSettings();
		string pose = base.Session.Guest.GetCurrentPoseStr();
		base.Session.Modal.MessageBoxYesNo("Delete camera?").OnResult += delegate(bool accept)
		{
			if (accept)
			{
				CameraSettings nextCamera = GetNextCamera(settings);
				registry.Delete(pose, settings);
				RefreshCameraList();
				SetCustomCamera(nextCamera);
			}
		};
	}

	private void ToggleDirectorButton()
	{
		if (CameraMode == CameraMode.POV)
		{
			CameraMode = CameraMode.DIRECTOR;
		}
		else if (CameraMode == CameraMode.CUSTOM)
		{
			CameraMode = CameraMode.DIRECTOR;
		}
		else
		{
			CameraMode = CameraMode.POV;
		}
	}

	private void ToggleSwitchButton()
	{
		if (CameraMode == CameraMode.DIRECTOR)
		{
			CameraSettings settings = cameraRig.GetSettings();
			SetCustomCamera(settings);
		}
		else if (CameraMode == CameraMode.POV || CameraMode == CameraMode.CUSTOM)
		{
			CameraSettings next = GetNextCamera(currentCameraSettings);
			if (next != null)
			{
				SetCustomCamera(next);
			}
			else if (EnableQuickCam.Value && currentCameraSettings == null)
			{
				CameraMode = CameraMode.DIRECTOR;
				CameraSettings settings2 = cameraRig.GetSettings();
				SetCustomCamera(settings2);
			}
			else
			{
				SetCustomCamera(next);
			}
		}
	}

	private void SetCustomCamera(CameraSettings settings)
	{
		if (settings != null)
		{
			CameraMode = CameraMode.CUSTOM;
			cameraRig.SetSettings(settings, pivotEnabled: true);
			cameraRig.UpdatePivot();
			currentCameraSettings = settings;
		}
		else
		{
			CameraMode = CameraMode.POV;
			currentCameraSettings = null;
		}
	}

	private CameraSettings GetNextCamera(CameraSettings current)
	{
		if (base.Session.Guest == null)
		{
			return null;
		}
		return registry.NextCameraForPose(base.Session.Guest.GetCurrentPoseStr(), current);
	}

	private Drawable CreateGUI()
	{
		GridLayout panel = new GridLayout();
		panel.Add(new DrawableLabel("Quick Cam:"));
		uiCameraList = panel.Add(new DrawableLabel("1/10"), newline: true);
		uiOnR = panel.Add(new DrawableLabel("[" + EditKey.ToString() + "] edit camera"));
		uiOnInsert = panel.Add(new DrawableLabel("[" + SaveKey.ToString() + "] save camera"), newline: true);
		uiOnX = panel.Add(new DrawableLabel("[" + SwitchKey.ToString() + "] next camera"));
		uiOnDelete = panel.Add(new DrawableLabel("[" + DeleteKey.ToString() + "] update camera"), newline: true);
		GridLayout panel2 = new GridLayout();
		panel2.Add(new DrawableLabel("Camera: "));
		cameraModeLabel = panel2.Add(new DrawableLabel(""));
		panel2.Add(new DrawableLabel("  "));
		if (!SceneCamMode)
		{
			return panel2;
		}
		return panel;
	}

	private void UpdateGUI()
	{
		if (CameraMode == CameraMode.DIRECTOR)
		{
			uiOnR.Text = "[" + EditKey?.ToString() + "] reset camera";
		}
		else if (CameraMode == CameraMode.POV)
		{
			uiOnR.Text = "[" + EditKey?.ToString() + "] create camera";
		}
		else
		{
			uiOnR.Text = "[" + EditKey?.ToString() + "] edit camera";
		}
		if (CameraMode != CameraMode.DIRECTOR && availableCameras.Count > 0)
		{
			if (currentCameraSettings != null)
			{
				uiOnX.Text = "[" + SwitchKey?.ToString() + "] next camera";
			}
			else
			{
				uiOnX.Text = "[" + SwitchKey?.ToString() + "] scene camera";
			}
		}
		else if (CameraMode == CameraMode.DIRECTOR)
		{
			uiOnX.Text = "[" + SwitchKey?.ToString() + "] tune pose";
		}
		uiOnInsert.Text = (CanSave ? ("[" + SaveKey?.ToString() + "] to save") : "");
		if (CameraMode == CameraMode.DIRECTOR)
		{
			uiOnDelete.Text = (CanUpdate ? ("[" + DeleteKey?.ToString() + "] to update") : "");
		}
		else if (CameraMode == CameraMode.CUSTOM)
		{
			uiOnDelete.Text = (CanDelete ? ("[" + DeleteKey?.ToString() + "] to delete") : "");
		}
		else
		{
			uiOnDelete.Text = "";
		}
		uiCameraList.Text = "Preset: " + (availableCameras.IndexOf(currentCameraSettings) + 1) + "/" + availableCameras.Count;
		if (CameraMode == CameraMode.POV)
		{
			cameraModeLabel.Text = "POV";
		}
		else if (CameraMode == CameraMode.CUSTOM)
		{
			cameraModeLabel.Text = "Orbit";
		}
		else
		{
			cameraModeLabel.Text = "Free";
		}
	}
}
