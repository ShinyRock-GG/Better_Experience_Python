using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace BetterExperience.CustomScene;

// SMA 23.1: the reused office/interview scenes ship the GLOBAL HDRP ColorAdjustments
// postExposure at ~+2 EV, washing the whole frame out. Two facts make a one-shot fix
// insufficient:
//   1. It's washed out FROM THE MOMENT THE SCENE LOADS -- before BE's Python runtime
//      even exists (GuestLoadGate defers Python until the model loads), so no Python
//      hook can reach the pre-model loading window.
//   2. The game RE-APPLIES the authored exposure on certain events -- confirmed by the
//      washout returning the instant the AIchat phone overlay opens (F9): that mod has
//      no post-processing code at all, so opening its ScreenSpaceOverlay canvas / freeing
//      the cursor makes the GAME re-blend its authored +EV back over any one-shot fix.
// So we ENFORCE rather than set-once: a persistent, DontDestroyOnLoad MonoBehaviour that
// re-clamps any egregious global postExposure every frame (cheap -- a cached float compare)
// and periodically re-scans for new/blended-in Volumes. Threshold-gated so intentional
// grading is untouched; config-toggleable.
public static class SceneWashoutFix
{
	private static bool _registered;

	public static void Register(ConfigFile config)
	{
		if (_registered)
		{
			return;
		}
		_registered = true;

		ConfigEntry<bool> enabled = config.Bind("Lighting", "FixSceneWashout", true,
			"Continuously neutralize the reused scene's global +EV post-exposure wash-out (also re-clamps when the game re-applies it, e.g. when an overlay UI opens).");
		ConfigEntry<float> threshold = config.Bind("Lighting", "WashoutThresholdEV", 0.5f,
			"Only clamp a ColorAdjustments whose postExposure exceeds this EV -- leaves intentional grading alone.");
		ConfigEntry<float> rescanSeconds = config.Bind("Lighting", "WashoutRescanSeconds", 2f,
			"How often to re-scan the scene for new/blended-in Volumes (the per-frame re-clamp of already-found ones is always on).");

		GameObject host = new GameObject("BE_WashoutEnforcer");
		UnityEngine.Object.DontDestroyOnLoad(host);
		host.hideFlags = HideFlags.HideAndDontSave;
		WashoutEnforcer enforcer = host.AddComponent<WashoutEnforcer>();
		enforcer.Init(enabled, threshold, rescanSeconds);
	}
}

// The persistent enforcer. Tracks every ColorAdjustments it has seen washed-out and
// re-clamps them each frame; a periodic full re-scan picks up Volumes that appear or
// blend in later. All work is on the Unity main thread (MonoBehaviour callbacks).
public sealed class WashoutEnforcer : MonoBehaviour
{
	private static readonly Logger logger = new Logger();

	private ConfigEntry<bool> _enabled;
	private ConfigEntry<float> _threshold;
	private ConfigEntry<float> _rescanSeconds;

	private readonly List<ColorAdjustments> _tracked = new List<ColorAdjustments>();
	private float _rescanTimer;
	private float _logCooldown;

	public void Init(ConfigEntry<bool> enabled, ConfigEntry<float> threshold, ConfigEntry<float> rescanSeconds)
	{
		_enabled = enabled;
		_threshold = threshold;
		_rescanSeconds = rescanSeconds;
		SceneManager.sceneLoaded += OnSceneLoaded;
		Rescan("register");
		logger.Info("[BE] WashoutEnforcer active (threshold={0} EV, rescan={1}s)", _threshold.Value, _rescanSeconds.Value);
	}

	private void OnDestroy()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// A new scene brings its own Volumes; drop stale refs and re-scan immediately
		// so the washout never shows even for the first frame.
		_tracked.Clear();
		Rescan("scene '" + scene.name + "'");
	}

	private void Update()
	{
		if (_enabled == null || !_enabled.Value)
		{
			return;
		}

		// Cheap per-frame re-clamp of everything we already know about -- this is what
		// beats the game re-applying the authored +EV when an overlay UI opens (F9).
		float threshold = _threshold.Value;
		bool reclamped = false;
		for (int i = 0; i < _tracked.Count; i++)
		{
			ColorAdjustments ca = _tracked[i];
			if (ca != null && ca.postExposure.value > threshold)
			{
				ca.postExposure.overrideState = true;
				ca.postExposure.value = 0f;
				reclamped = true;
			}
		}
		if (reclamped && _logCooldown <= 0f)
		{
			logger.Info("[BE] WashoutEnforcer: re-clamped global postExposure back to 0 (game re-applied it).");
			_logCooldown = 1f;
		}
		if (_logCooldown > 0f)
		{
			_logCooldown -= Time.unscaledDeltaTime;
		}

		// Periodic full re-scan for Volumes that appear or blend in after load.
		_rescanTimer += Time.unscaledDeltaTime;
		if (_rescanTimer >= Mathf.Max(0.25f, _rescanSeconds.Value))
		{
			_rescanTimer = 0f;
			Rescan(null);
		}
	}

	// Find all Volumes, track their ColorAdjustments, and clamp any egregious postExposure
	// right away. `reason` non-null => log the initial clamp (load / scene change); null
	// => quiet periodic maintenance scan.
	private void Rescan(string reason)
	{
		try
		{
			float threshold = _threshold.Value;
			Volume[] volumes = UnityEngine.Object.FindObjectsOfType<Volume>();
			foreach (Volume v in volumes)
			{
				if (v == null)
				{
					continue;
				}
				VolumeProfile profile = v.profile;   // per-Volume instance copy; never sharedProfile
				if (profile == null)
				{
					continue;
				}
				if (profile.TryGet<ColorAdjustments>(out var ca) && ca != null)
				{
					if (!_tracked.Contains(ca))
					{
						_tracked.Add(ca);
					}
					if (ca.postExposure.value > threshold)
					{
						float ev = ca.postExposure.value;
						ca.postExposure.overrideState = true;
						ca.postExposure.value = 0f;
						if (reason != null)
						{
							logger.Info("[BE] WashoutEnforcer: '{0}' postExposure {1:0.00} -> 0 ({2}).", v.name, ev, reason);
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error("[BE] WashoutEnforcer rescan failed: {0}", ex);
		}
	}
}
