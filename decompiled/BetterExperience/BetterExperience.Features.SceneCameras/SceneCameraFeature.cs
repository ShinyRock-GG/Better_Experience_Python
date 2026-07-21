using System;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features.SceneCameras;

internal class SceneCameraFeature : PluginFeature
{
	private ConfigEntry<bool> configEnableSceneCamera;

	private ConfigEntry<KeyboardShortcut> configKeyForward;

	private ConfigEntry<KeyboardShortcut> configKeyLeft;

	private ConfigEntry<KeyboardShortcut> configKeyBackward;

	private ConfigEntry<KeyboardShortcut> configKeyRight;

	private ConfigEntry<KeyboardShortcut> configKeyUp;

	private ConfigEntry<KeyboardShortcut> configKeyDown;

	private ConfigEntry<KeyboardShortcut> configKeyEditCam;

	private ConfigEntry<KeyboardShortcut> configKeySwitchCam;

	private ConfigEntry<KeyboardShortcut> configKeySaveCam;

	private ConfigEntry<KeyboardShortcut> configKeyDelCam;

	private ConfigEntry<bool> updatePivotWithAlt;

	private ConfigEntry<bool> enableRagdollRaycastPivot;

	private ConfigEntry<bool> enablePivotLinecast;

	private ConfigEntry<bool> enableRigDetachment;

	private ConfigEntry<bool> enableQuickCam;

	private ConfigEntry<bool> enableLegacyMode;

	private bool sceneCamMode => enableLegacyMode.Value;

	public override bool Enabled => configEnableSceneCamera.Value;

	public override void Configure(ConfigFile config)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		configEnableSceneCamera = config.Bind<bool>("Features", "SceneCamera", true, "Scene Camera: Enable feature");
		enableLegacyMode = config.Bind<bool>("SceneCamera", "LegacyMode", false, "SceneCamera: legacy mode");
		configKeyForward = config.Bind<KeyboardShortcut>("SceneCamera", "ForwardKey", new KeyboardShortcut(KeyCode.W, Array.Empty<KeyCode>()), "SceneCamera: Forward");
		configKeyLeft = config.Bind<KeyboardShortcut>("SceneCamera", "LeftKey", new KeyboardShortcut(KeyCode.A, Array.Empty<KeyCode>()), "SceneCamera: Left");
		configKeyBackward = config.Bind<KeyboardShortcut>("SceneCamera", "BackwardKey", new KeyboardShortcut(KeyCode.S, Array.Empty<KeyCode>()), "SceneCamera: Backward");
		configKeyRight = config.Bind<KeyboardShortcut>("SceneCamera", "RightKey", new KeyboardShortcut(KeyCode.D, Array.Empty<KeyCode>()), "SceneCamera: Right");
		configKeyUp = config.Bind<KeyboardShortcut>("SceneCamera", "UpKey", new KeyboardShortcut(KeyCode.Q, Array.Empty<KeyCode>()), "SceneCamera: Up");
		configKeyDown = config.Bind<KeyboardShortcut>("SceneCamera", "DownKey", new KeyboardShortcut(KeyCode.E, Array.Empty<KeyCode>()), "SceneCamera: Down");
		configKeyEditCam = config.Bind<KeyboardShortcut>("SceneCamera", "EditCamKey", new KeyboardShortcut(KeyCode.R, Array.Empty<KeyCode>()), "SceneCamera: Edit Camera");
		configKeySwitchCam = config.Bind<KeyboardShortcut>("SceneCamera", "SwitchCamKey", new KeyboardShortcut(KeyCode.X, Array.Empty<KeyCode>()), "SceneCamera: Switch Camera");
		configKeySaveCam = config.Bind<KeyboardShortcut>("SceneCamera", "SaveCamKey", new KeyboardShortcut(KeyCode.Insert, Array.Empty<KeyCode>()), "SceneCamera: Save Camera");
		configKeyDelCam = config.Bind<KeyboardShortcut>("SceneCamera", "DelCamKey", new KeyboardShortcut(KeyCode.Delete, Array.Empty<KeyCode>()), "SceneCamera: Delete Camera");
		updatePivotWithAlt = config.Bind<bool>("SceneCamera", "UpdatePivotWithAlt", true, "SceneCamera: Update pivot with alt rotation");
		enableRagdollRaycastPivot = config.Bind<bool>("SceneCamera", "EnableRagdollRaycastPivot", true, "SceneCamera: Enable ragdoll raycast");
		enablePivotLinecast = config.Bind<bool>("SceneCamera", "EnablePivotLinecast", false, "SceneCamera: Enable camera obstacle avoidance");
		enableRigDetachment = config.Bind<bool>("SceneCamera", "EnableRigDetachment", true, "SceneCamera: Disconnect camera from player when pivot is character body");
		enableQuickCam = config.Bind<bool>("SceneCamera", "EnableQuickCamera", true, "SceneCamera: Enable Quick pivot-camera");
	}

	public override void OnInit()
	{
		PluginOptionsService options = Lookup<PluginOptionsService>();
		options.Expose(configEnableSceneCamera, base.Scope);
		options.Expose(enableRagdollRaycastPivot, base.Scope);
		options.Expose(enablePivotLinecast, base.Scope);
		if (sceneCamMode)
		{
			options.Expose(enableQuickCam, base.Scope);
		}
		options.Expose(configKeyForward, base.Scope);
		options.Expose(configKeyBackward, base.Scope);
		options.Expose(configKeyLeft, base.Scope);
		options.Expose(configKeyRight, base.Scope);
		options.Expose(configKeyUp, base.Scope);
		options.Expose(configKeyDown, base.Scope);
		if (sceneCamMode)
		{
			options.Expose(configKeyEditCam, base.Scope);
			options.Expose(configKeySwitchCam, base.Scope);
			options.Expose(configKeySaveCam, base.Scope);
			options.Expose(configKeyDelCam, base.Scope);
		}
	}

	public override void OnStart()
	{
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		InputManager input = Lookup<DispatcherService>().Input;
		IInputHandle forward = input.KeyboardEvent(configKeyForward, base.Scope);
		IInputHandle left = input.KeyboardEvent(configKeyLeft, base.Scope);
		IInputHandle right = input.KeyboardEvent(configKeyRight, base.Scope);
		IInputHandle backward = input.KeyboardEvent(configKeyBackward, base.Scope);
		IInputHandle up = input.KeyboardEvent(configKeyUp, base.Scope);
		IInputHandle down = input.KeyboardEvent(configKeyDown, base.Scope);
		IInputHandle edit = input.KeyboardEvent(configKeyEditCam, base.Scope);
		IInputHandle switc = input.KeyboardEvent(configKeySwitchCam, base.Scope);
		IInputHandle save = input.KeyboardEvent(configKeySaveCam, base.Scope);
		IInputHandle del = input.KeyboardEvent(configKeyDelCam, base.Scope);
		IInputHandle alt = input.KeyboardEvent(new KeyboardShortcut(KeyCode.LeftAlt, Array.Empty<KeyCode>()), base.Scope);
		IInputHandle shift = input.KeyboardEvent(new KeyboardShortcut(KeyCode.LeftShift, Array.Empty<KeyCode>()), base.Scope);
		Lookup<SessionTracker>().SessionServices.Add(() => new SceneCameraService
		{
			Forward = forward,
			Left = left,
			Backward = backward,
			Right = right,
			Up = up,
			Down = down,
			EditKey = edit,
			SwitchKey = switc,
			SaveKey = save,
			DeleteKey = del,
			AltKey = alt,
			ShiftKey = shift,
			UpdatePivotWithAlt = updatePivotWithAlt,
			EnableRagdollRaycast = enableRagdollRaycastPivot,
			EnablePivotLinecast = enablePivotLinecast,
			EnableDetachableRig = enableRigDetachment,
			EnableQuickCam = enableQuickCam,
			SceneCamMode = sceneCamMode
		});
	}
}
