using System;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using Assets._ReusableScripts.Globales.Mapas;
using Assets.TValle.BeachGirl.Characters.Male.Runtime.Controllers;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Physics;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class SurfaceAssistanceService : SessionService
{
	private class Raycaster
	{
		private bool cachedRaycast;

		private RaycastHit cachedRaycastHit;

		public Func<bool> InvalidateCachePredicate { get; set; } = () => true;

		public int LayerMask { get; set; }

		public float RaycastDistance { get; set; } = 0.7f;

		public RaycastHit Hit => cachedRaycastHit;

		public float Distance => Hit.distance;

		public float SphereCastRadius { get; private set; } = 0.01f;

		public bool Raycast(params Transform[] others)
		{
			if (!InvalidateCachePredicate())
			{
				return cachedRaycast;
			}
			cachedRaycast = false;
			foreach (Transform t in others)
			{
				if (t != null && Physics.SphereCast(new Ray(Camera.main.transform.position, t.position - Camera.main.transform.position), SphereCastRadius, out var hit, RaycastDistance, LayerMask))
				{
					if (!cachedRaycast || cachedRaycastHit.distance > hit.distance)
					{
						cachedRaycastHit = hit;
					}
					cachedRaycast = true;
				}
			}
			return cachedRaycast;
		}
	}

	private const float MIN_Z_OFFSET = -0.2f;

	private const float MAX_Z_OFFSET = 0f;

	private HandUserController handController;

	private Transform middleFingerRoot;

	private Transform middleFingerTip;

	private Transform hand;

	private SmartHandService smartHand;

	private Raycaster raycaster;

	private HandCameraControllerV2 handCamera;

	private bool interpolationEnabled;

	private float interpolationTarget;

	private bool IsHand
	{
		get
		{
			if (handCamera.handController.tipoDePose == HandTipoDePose.massage)
			{
				return base.Session.MainCamera.IsPov;
			}
			return false;
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		ConfiguracionGlobal.Layers layers = MapaSingleton<ConfiguracionGlobal>.instance.layers;
		if (Enabled)
		{
			raycaster = new Raycaster();
			handCamera = base.Session.Player.GameObject.GetComponentInChildren<HandCameraControllerV2>();
			raycaster.LayerMask = layers.skins.ToLayerMask() | layers.convexSkins.ToLayerMask();
			base.Scope.EventHandler(delegate(UpdatingHandPositionV2 h)
			{
				handCamera.updatingHandPosition += h;
			}, delegate(UpdatingHandPositionV2 h)
			{
				handCamera.updatingHandPosition -= h;
			}, OnUpdatingIKHand);
			handController = base.Session.Player.GameObject.GetComponentInChildren<HandUserController>();
			Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
			PhysicalPuppet p = new PhysicalPuppet(base.Session.Player.GameObject);
			middleFingerRoot = p.GetIKBoneTransform(HumanBodyBones.RightMiddleProximal);
			middleFingerTip = p.GetIKBoneTransform(HumanBodyBones.RightMiddleDistal);
			hand = p.GetIKBoneTransform(HumanBodyBones.RightHand);
			smartHand = base.Scope.Parent.Lookup<SmartHandService>();
		}
	}

	private void OnUpdate()
	{
		if (interpolationEnabled && IsHand && !smartHand.ClothesInteraction.HasActiveSphere)
		{
			float newdepth = interpolationTarget;
			float deltadepth = Math.Abs(newdepth - handController.depthPosition);
			if (deltadepth > 0.01f)
			{
				handController.depthPosition = Mathf.Lerp(handController.depthPosition, newdepth, Time.deltaTime);
			}
		}
	}

	private void OnUpdatingIKHand(ref Vector3 targetWorldPosition, ref Quaternion targetWorldRotation, Transform handBone, Transform pose, HandCameraControllerV2 sender)
	{
		if (IsHand)
		{
			FixProjection(ref targetWorldPosition, ref targetWorldRotation, pose);
		}
	}

	private void FixProjection(ref Vector3 targetWorldPosition, ref Quaternion targetWorldRotation, Transform pose)
	{
		interpolationEnabled = true;
		if (raycaster.Raycast(hand, middleFingerTip, middleFingerRoot))
		{
			float distance = raycaster.Distance;
			distance -= 0.01f;
			float excessDepth = Vector3.Distance(Camera.main.transform.position, hand.position) - Vector3.Distance(Camera.main.transform.position, pose.position);
			interpolationTarget = distance - handController.defaultDepth + excessDepth;
		}
		else
		{
			interpolationTarget = -0.2f;
		}
		interpolationTarget = Mathf.Clamp(interpolationTarget, -0.2f, 0f);
	}
}
