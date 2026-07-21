using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets.Base.RootMotion.BeachGirl.Runtime.Controllers.Interacciones.MaleInteractions;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Physics;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class GripController : SessionService, IGripController
{
	private GripSolver solver;

	private const float GRIP_TARGET_ANGLE = -90f;

	private bool hasBindingPose;

	private bool fixedBindingPose;

	private bool bindingPoseResetRequired;

	public PhysicalPuppet Puppet { get; private set; }

	public GripPose CurrentPose { get; private set; }

	public bool TransitionComplete { get; private set; } = true;

	public bool PoserEnabled { get; set; } = true;

	public bool HasBindingPose => hasBindingPose;

	public GripController()
	{
		logger = new Logger();
	}

	public void SetPose(GripPose pose, bool force = false)
	{
		if (!(CurrentPose != pose || force))
		{
			return;
		}
		CurrentPose = pose;
		switch (pose)
		{
		case GripPose.Idle:
		{
			for (int i = 0; i < solver.FingerCount; i++)
			{
				solver.RequestPose(i, new float[3], 0f, checkCollision: false);
			}
			break;
		}
		case GripPose.FingersDown:
		{
			float[] thumbpose = new float[3] { -11.25f, -15f, -0f };
			float[] forwardpose4 = new float[3] { -22.5f, -30f, -60f };
			solver.RequestPose(0, forwardpose4, -5f, checkCollision: true);
			solver.RequestPose(1, forwardpose4, 0f, checkCollision: true);
			solver.RequestPose(2, forwardpose4, 5f, checkCollision: true);
			solver.RequestPose(3, forwardpose4, 10f, checkCollision: true);
			solver.RequestPose(4, thumbpose, 0f, checkCollision: true);
			break;
		}
		case GripPose.FingersUp:
		{
			float[] forwardpose3 = new float[3] { 0f, 0f, 90f };
			solver.RequestPose(0, forwardpose3, -5f, checkCollision: false);
			solver.RequestPose(1, forwardpose3, 0f, checkCollision: false);
			solver.RequestPose(2, forwardpose3, 5f, checkCollision: false);
			solver.RequestPose(3, forwardpose3, 10f, checkCollision: false);
			solver.RequestPose(4, forwardpose3, 0f, checkCollision: false);
			break;
		}
		case GripPose.PreGrab:
		{
			float[] forwardpose2 = new float[3] { -90f, -90f, -90f };
			float[] pose3 = new float[3] { -90f, -90f, 0f };
			float[] thumb2 = new float[3];
			solver.RequestPose(0, pose3, -5f, checkCollision: false);
			solver.RequestPose(1, forwardpose2, 0f, checkCollision: false);
			solver.RequestPose(2, forwardpose2, 5f, checkCollision: false);
			solver.RequestPose(3, forwardpose2, 10f, checkCollision: false);
			solver.RequestPose(4, thumb2, 0f, checkCollision: false);
			break;
		}
		case GripPose.Grab:
		{
			float[] forwardpose = new float[3] { -90f, -90f, -90f };
			float[] pose2 = new float[3] { -90f, -90f, -90f };
			float[] thumb = new float[3];
			solver.RequestPose(0, pose2, -5f, checkCollision: false);
			solver.RequestPose(1, forwardpose, 0f, checkCollision: false);
			solver.RequestPose(2, forwardpose, 5f, checkCollision: false);
			solver.RequestPose(3, forwardpose, 10f, checkCollision: false);
			solver.RequestPose(4, thumb, 0f, checkCollision: false);
			break;
		}
		}
		TransitionComplete = false;
	}

	public override void OnStart()
	{
		base.OnStart();
		Puppet = new PhysicalPuppet(base.Session.Player.GameObject);
		solver = new GripSolver(Puppet);
		IIKUpdater ik = base.Session.Player.GameObject.GetComponentInChildren<IIKUpdater>();
		ik.onPhysicsIKUpdated += ApplyPose;
		ik.onAllIKsUpdated += Ik_onAllIKsUpdated;
		base.Scope.OnDispose += delegate
		{
			ik.onAllIKsUpdated -= Ik_onAllIKsUpdated;
		};
		base.Scope.OnDispose += delegate
		{
			ik.onPhysicsIKUpdated -= ApplyPose;
		};
		InitBindingPose();
	}

	private void InitBindingPose()
	{
		InteraccionesBasicasDeMale interactions = base.Session.Player.GameObject.GetComponentInChildren<InteraccionesBasicasDeMale>();
		if (!(interactions == null))
		{
			InteraccionDeCharacter hand = interactions.Obtener(InteraccionesBasicasDeMale.InteraccionSegundariaName.massage_R);
			if (hand != null && solver.SetBindingPoseFrom(hand.instancia.transform))
			{
				fixedBindingPose = true;
				hasBindingPose = true;
				bindingPoseResetRequired = false;
			}
		}
	}

	private void ApplyPose(IIKUpdater obj)
	{
		if (hasBindingPose && !PoserEnabled)
		{
			hasBindingPose = false;
		}
		if (!PoserEnabled)
		{
			return;
		}
		if (!hasBindingPose)
		{
			if (!bindingPoseResetRequired)
			{
				bindingPoseResetRequired = true;
			}
		}
		else
		{
			solver.Apply();
		}
	}

	public void ResetIdlePose()
	{
		bindingPoseResetRequired = true;
	}

	private void Ik_onAllIKsUpdated(IIKUpdater obj)
	{
		if (!PoserEnabled)
		{
			return;
		}
		if (bindingPoseResetRequired)
		{
			if (!fixedBindingPose)
			{
				solver.CaptureBindingPose();
			}
			bindingPoseResetRequired = false;
			hasBindingPose = true;
		}
		if (hasBindingPose)
		{
			TransitionComplete = solver.Update(Time.deltaTime);
		}
	}

	public void SetContactMatrix(bool[][] contactMatrix)
	{
		solver.SetContactMatrix(contactMatrix);
	}
}
