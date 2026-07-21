using System;
using System.Collections;
using Assets._ReusableScripts.Genetica.NPCs;
using Assets._ReusableScripts.Scenes;
using Assets._ReusableScripts.Tiempo;
using Assets.Productos.Juegos.Reception.Scripts.Genetica.Eventos;
using Assets.Productos.Juegos.Reception.Scripts.TimepoEventosDeJuego;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoTraining : SessionService
{
	private bool _active;

	private bool activeOnce;

	private DispatcherService disp;

	private OverlayService overlay;

	private bool active
	{
		get
		{
			return _active;
		}
		set
		{
			_active = value;
			if (_active)
			{
				if (activeOnce)
				{
					overlay.InfoMessage("Autotrain once started");
				}
				else
				{
					overlay.InfoMessage("Autotrain started");
				}
			}
			else
			{
				overlay.InfoMessage("Autotrain stopped");
			}
		}
	}

	public override void OnStart()
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		base.OnStart();
		disp = Lookup<DispatcherService>();
		overlay = Lookup<OverlayService>();
		IInputHandle toggle = disp.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Home, Array.Empty<KeyCode>()), base.Scope);
		IInputHandle toggleOnce = disp.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.End, Array.Empty<KeyCode>()), base.Scope);
		disp.DoUpdate.Add(delegate
		{
			if (toggle.Up || toggleOnce.Up)
			{
				activeOnce = toggleOnce.Up;
				active = !active;
				if (!active)
				{
					activeOnce = false;
				}
			}
		});
		disp.StartCoroutine(ControlRoutine(), base.Scope);
	}

	private IEnumerator ControlRoutine()
	{
		// HorariosNormalesDeEntrevistas is [Obsolete] in SMA 23.1; auto-training coroutine disabled
		while (true)
		{
			yield return new WaitForSeconds(1.5f);
			if (!active) continue;
			if (SceneManager.GetSceneByName("EntrevistaHeroina").IsValid())
			{
				logger.Info("Interview found");
				if (base.Session.Guest != null)
				{
					logger.Info("Sending guest home");
					base.Session.TerminateInterview();
				}
			}
			else if (SceneManager.GetSceneByName("EntrevistaVacia").IsValid())
			{
				if (CanStartInterview())
				{
					logger.Info("Starting interview");
					StartInterview();
					if (activeOnce) { activeOnce = false; active = false; }
				}
			}
		}
	}

	private void StartInterview()
	{
		// SceneLoader.Pedido pointer casts and PiscinasDeEventosDeEntrevista are not compatible with SMA 23.1
		// Stubbed: auto-training feature disabled
	}

	private bool CanStartInterview()
	{
		// PiscinasDeEventosDeEntrevista is [Obsolete] in SMA 23.1
		return false;
	}
}
