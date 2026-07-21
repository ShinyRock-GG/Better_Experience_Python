using System;
using Assets;
using Assets._ReusableScripts;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ootii.Camaras;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using com.ootii.Actors.AnimationControllers;
using com.ootii.Cameras;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

public class PlayerCharacter : IDisposable
{
	private Logger logger = new Logger();

	private CameraController cameraController;

	private MotionController motionController;

	private MotionAndActorControllerActivable smaMotionController;

	private ModificadorDeBool disablePlayerLocomotionRef;

	private ModificadorDeBool enableCameraLookRef;

	private IIKUpdater ikUpdater;

	private PelvisMovementController pelvisCtl;

	public Observable OnIkUpdated { get; } = new Observable();

	public GameObject GameObject => GameObject.Find("/Male Avatar");

	public MaleChar Character => (MaleChar)Singleton<CurrentMainChar>.instance.main.character;

	public Transform RootMotion => Character.animatorRootMotionTransform;

	public bool LocomotionEnabled
	{
		get
		{
			return !disablePlayerLocomotionRef.valor.valor;
		}
		set
		{
			bool inputDisabled = !value;
			disablePlayerLocomotionRef.valor.valor = inputDisabled;
			smaMotionController.Actualizar();
		}
	}

	public bool CameraLookEnabled
	{
		get
		{
			return enableCameraLookRef.valor.valor;
		}
		set
		{
			enableCameraLookRef.valor.valor = value;
		}
	}

	public bool ActionsEnabled
	{
		get
		{
			if (Singleton<PlayerInputProxy>.existeEnScena)
			{
				return Singleton<PlayerInputProxy>.instance.activoMovement;
			}
			return false;
		}
		set
		{
			if (Singleton<PlayerInputProxy>.existeEnScena)
			{
				Singleton<PlayerInputProxy>.instance.activoMovement = value;
				Singleton<PlayerInputProxy>.instance.activoOverall = value;
			}
		}
	}

	public float PelvisY
	{
		get
		{
			return pelvisCtl.currentLocalTarget.y;
		}
		set
		{
			// MovePelvisTarget not on PelvisMovementController in SMA 23.1; pelvis Y setter disabled
		}
	}

	public float PelvisZ
	{
		get
		{
			return pelvisCtl.currentLocalTarget.z;
		}
		set
		{
			// MovePelvisTarget not on PelvisMovementController in SMA 23.1; pelvis Z setter disabled
		}
	}

	public PlayerCharacter()
	{
		CurrentMainChar mainchar = Singleton<CurrentMainChar>.instance;
		MainCharCamera maincam = mainchar.camara as MainCharCamera;
		cameraController = maincam.GetComponent<CameraController>();
		Character character = ((MonoBehaviour)cameraController.CharacterController).GetComponentInParent<Character>();
		motionController = character.GetComponentInChildren<MotionController>();
		smaMotionController = character.GetComponentInChildren<MotionAndActorControllerActivable>();
		disablePlayerLocomotionRef = smaMotionController.estanDesactivadosModificable.ObtenerModificadorNotNull(Guid.NewGuid().ToString());
		disablePlayerLocomotionRef.valor.valor = false;
		enableCameraLookRef = Singleton<PlayerInputProxy>.instance.activoModificableViewAND.ObtenerModificadorNotNull(Guid.NewGuid().ToString());
		enableCameraLookRef.valor.valor = true;
		ikUpdater = GameObject.GetComponentInChildren<IIKUpdater>();
		ikUpdater.onAllIKsUpdated += delegate
		{
			OnIkUpdated.Invoke();
		};
		pelvisCtl = GameObject.GetComponentInChildren<PelvisMovementController>();
	}

	public void Move(Vector3 rMovement)
	{
		if (!LocomotionEnabled)
		{
			GameObject.transform.Translate(rMovement, motionController.ActorController.transform);
		}
		else
		{
			GameObject.transform.Translate(rMovement, motionController.ActorController.transform);
		}
	}

	public void Rotate(float yaw)
	{
		motionController.ActorController.Rotate(Quaternion.AngleAxis(yaw, motionController.ActorController.Transform.up));
		Character.animatorRootMotionTransform.rotation *= Quaternion.AngleAxis(yaw, motionController.ActorController.Transform.up);
	}

	public void Dispose()
	{
		if (disablePlayerLocomotionRef != null)
		{
			disablePlayerLocomotionRef.TryRemoverDeOwner();
		}
	}

	internal void AddScale(Vector3 vector3)
	{
		GameObject.transform.localScale += vector3;
	}

	internal void ResetScale()
	{
		GameObject.transform.localScale = Vector3.one;
	}
}
