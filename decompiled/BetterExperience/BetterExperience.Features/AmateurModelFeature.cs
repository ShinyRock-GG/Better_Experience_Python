using Assets._ReusableScripts.CuchiCuchi.AI.ReactoresDeEstimulos;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ai.Reactores.ParaReactores.OjosExpresion;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets.TValle.BeachGirl.Sexual;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using UnityEngine;

namespace BetterExperience.Features;

internal class AmateurModelFeature : PluginFeature
{
	private class AmateurModelService : SessionService
	{
		private LookAtControllerV2 controller;

		private OjosExpresionController eyesController;

		private float time;

		private Camera lookAtCam;

		private GuestHeadController.LookAt lookAtTarget;

		public float Duration { get; private set; } = 20f;

		public int Priority { get; private set; } = 2147483646;

		public bool EnableDontCloseEyes { get; set; } = true;

		public bool Suppress { get; set; }

		public override void OnStart()
		{
			base.OnStart();
			controller = base.Session.Guest.Impl.GetComponentInChildren<LookAtControllerV2>();
			eyesController = base.Session.Guest.Impl.GetComponentInChildren<OjosExpresionController>();
			if (EnableDontCloseEyes)
			{
				OverrideEyesClosedAnimation();
			}
			lookAtTarget = base.Session.Guest.HeadController.CreateLookAt(base.Scope);
			lookAtTarget.Priority = 2147483547;
			lookAtTarget.Enabled = true;
			ForceLookIntoCamera();
			Lookup<DispatcherService>().DoUpdate.Add(Update, base.Scope);
		}

		private void OverrideEyesClosedAnimation()
		{
			ParaReactor reactor = base.Session.Guest.Impl.GetComponentInChildren<ParaReactorCerrarOjosAlAhogarse>();
			if (reactor != null)
			{
				reactor.enabled = false;
			}
			reactor = base.Session.Guest.Impl.GetComponentInChildren<ParaReactorCerrarOjosAlCoito>();
			if (reactor != null)
			{
				reactor.enabled = false;
			}
		}

		private void Update()
		{
			if (!Suppress)
			{
				time += Time.deltaTime;
				if (time > 5f || lookAtCam != Camera.main)
				{
					ForceLookIntoCamera();
					time = 0f;
				}
				if (EnableDontCloseEyes)
				{
					WidenEyes();
				}
			}
		}

		private void WidenEyes()
		{
			float value = 0f;
			OjosExpresionController.Orden state = eyesController.currentStado[OjosExpresionController.Tipo.achiquitar];
			if (state != null)
			{
				value = state.target;
				state.target = 0f;
			}
			state = eyesController.currentStado[OjosExpresionController.Tipo.agrandar];
			if (state != null)
			{
				state.target = Mathf.Max(state.target, value);
			}
		}

		private void ForceLookIntoCamera()
		{
			lookAtCam = Camera.main;
			lookAtTarget.Transform = lookAtCam.transform;
			lookAtTarget.HeadWeight = HeadWeight();
		}

		private float HeadWeight()
		{
			if (base.Session.Player.Character.peneDeCharacter.isPenetrating && base.Session.Player.Character.peneDeCharacter.TryGetPenetratingHole() is IBocaHole)
			{
				return 0f;
			}
			return 1f;
		}
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<bool> enableDontCloseEyes;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableAmateurModel", true, "Enable Amateur Model: She has irresistable urge to look at the camera");
		enableDontCloseEyes = config.Bind<bool>("AmateurModel", "EnableDontCloseEyes", false, "Amateur Model: Enable dont-close-eyes: prevents some long animations with eyes closed");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.guest);
		Lookup<PluginOptionsService>().Expose(enableDontCloseEyes, base.Scope, PluginOptionsService.SettingsType.guest);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new AmateurModelService());
	}
}
