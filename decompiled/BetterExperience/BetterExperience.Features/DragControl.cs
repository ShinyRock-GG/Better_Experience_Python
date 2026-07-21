using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using UnityEngine;
using Assets.TValle.BeachGirl;

namespace BetterExperience.Features;

internal class DragControl : PluginFeature
{
	private class DragControlService : SessionService
	{
		private class DragContext
		{
			public Vector3 motionAxis;

			public float thrust;
		}

		private DragContext context;

		private PelvisMovementController controller;

		private HandControllerV2 handController;

		private bool CanDrag => !handController.handEstaSiendoUsada;

		public override void OnStart()
		{
			base.OnStart();
			Lookup<DispatcherService>().DoUpdate.Add(HandleInput, base.Scope);
			controller = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			handController = base.Session.Player.GameObject.GetComponentInChildren<HandControllerV2>();
		}

		private Vector3 GetMouseMotion()
		{
			return new Vector3(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
		}

		private void ApplyThrust()
		{
			float dt = context.thrust * Time.deltaTime;
			context.thrust -= dt;
			Penis p = base.Session.Player.Character.pene;
			if (p.isPenetrating)
			{
				BoneStretchedChain hole = p.TryGetPenetratingHole();
				if (hole != null && hole.penetraciones.currentHits.count < 4 && dt < 0f)
				{
					dt = 0f;
				}
				dt = Mathf.Sign(dt) * Mathf.Min(7f, Mathf.Abs(dt));
			}
			controller.AddProfundidadDelta(dt * 0.05f);
		}

		private void HandleInput()
		{
			if (Input.GetMouseButton(0) && CanDrag)
			{
				if (context != null)
				{
					Vector3 dragDirection = GetMouseMotion();
					Vector3 axisMotion = Vector3.Scale(dragDirection, context.motionAxis);
					Vector3 absoluteAxismotion = Vector3.Scale(axisMotion, context.motionAxis);
					if (axisMotion.sqrMagnitude >= (dragDirection - absoluteAxismotion).sqrMagnitude)
					{
						float force = axisMotion.x + axisMotion.y + axisMotion.z;
						force /= Time.deltaTime;
						force *= 0.01f;
						context.thrust += force;
					}
					ApplyThrust();
					return;
				}
				context = new DragContext();
				Vector3 pointA = ((IPene)base.Session.Player.Character.pene).parteBase.position;
				Vector3 pointB = ((IPene)base.Session.Player.Character.pene).partePunta.position;
				Vector3 planeA = base.Session.MainCamera.WorldToScreen(pointA);
				Vector3 planeB = base.Session.MainCamera.WorldToScreen(pointB);
				Vector3 peneVec = (planeA - planeB).normalized;
				if (Mathf.Abs(peneVec.x) > Mathf.Abs(peneVec.y))
				{
					if (peneVec.x > 0f)
					{
						context.motionAxis = Vector3.left;
					}
					else
					{
						context.motionAxis = Vector3.right;
					}
				}
				else
				{
					context.motionAxis = Vector3.up;
				}
			}
			else
			{
				context = null;
			}
		}
	}

	private ConfigEntry<bool> enableFeature;

	public override bool Enabled => enableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		enableFeature = config.Bind<bool>("Features", "EnableDragControl", false, "Enable DragControl: Move pelvis with LMB drag");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().InterviewServices.Add(() => new DragControlService());
	}
}
