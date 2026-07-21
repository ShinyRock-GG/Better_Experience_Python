using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores.Hands;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Physics;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class SmartHandService : SessionService
{
	private class ColliderInfo
	{
		public List<Collider> Colliders { get; private set; }

		public List<ContactPoint> Contacts { get; private set; }

		public MeshCollider MeshCollider { get; private set; }

		public ColliderInfo(List<Collider> info)
		{
			Colliders = info;
			Contacts = new List<ContactPoint>();
			foreach (Collider c in info)
			{
				if (c is MeshCollider)
				{
					MeshCollider = (MeshCollider)c;
				}
			}
		}

		internal void Reset()
		{
			Contacts.Clear();
		}
	}

	private HandControllerV2 handController;

	private HandPickController pickController;

	private Traverse<float> pPickController_lastW;

	private IGripController gripController;

	private ColliderInfo handCollider;

	private ColliderInfo[] fingerColliders;

	private ColliderInfo[] thumbColliders;

	private List<ColliderInfo> trackedColliders = new List<ColliderInfo>();

	private bool hasContact;

	private HandTipoDePose lastHandPose;

	private bool gripWaitForRelax;

	public ClothesInteractionSupport ClothesInteraction { get; private set; }

	private bool IsHand => handController.tipoDePose == HandTipoDePose.massage;

	public bool LockGrip { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		ClothesInteraction = base.Scope.AddService(new ClothesInteractionSupport());
		handController = base.Session.Player.GameObject.GetComponentInChildren<HandControllerV2>();
		pickController = base.Session.Player.GameObject.GetComponentInChildren<HandPickController>();
		pPickController_lastW = Traverse.Create((object)pickController).Field<float>("m_LastW");
		GuestCharacter guest = base.Session.Guest;
		PhysicalPuppet playerPuppet = new PhysicalPuppet(base.Session.Player.GameObject);
		Lookup<HandPhysicsService>().SubscribePhysics(new HandPhysicsService.CollisionListener
		{
			Callback = OnPhysicsCallback
		}, base.Scope);
		gripController = base.Scope.AddService(new GripController());
		handCollider = new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Hand.R_Collider"));
		fingerColliders = new ColliderInfo[12]
		{
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger12.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger22.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger32.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger42.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger11.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger21.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger31.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger41.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger10.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger20.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger30.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger40.R"))
		};
		thumbColliders = new ColliderInfo[3]
		{
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger02.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger01.R")),
			new ColliderInfo(playerPuppet.ColliderByName("CC_Base_Finger00.R"))
		};
		trackedColliders.Add(handCollider);
		trackedColliders.AddRange(fingerColliders);
		trackedColliders.AddRange(thumbColliders);
		Plugin.DoUpdate.Add(OnUpdate, base.Scope);
	}

	private void OnPhysicsCallback(Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>> collisions)
	{
		foreach (ColliderInfo ci in trackedColliders)
		{
			ci.Reset();
		}
		foreach (KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> kv in collisions)
		{
			List<ContactPoint> contacts = kv.Value;
			foreach (ContactPoint contact in kv.Value)
			{
				Collider receiver = contact.thisCollider;
				Collider sender = contact.otherCollider;
				foreach (ColliderInfo ci2 in trackedColliders)
				{
					if (ci2.Colliders.Contains(sender))
					{
						ci2.Contacts.Add(contact);
					}
				}
			}
		}
		hasContact = false;
		foreach (ColliderInfo ci3 in trackedColliders)
		{
			if (!thumbColliders.Contains(ci3))
			{
				hasContact |= ci3.Contacts.Count > 0;
			}
		}
	}

	private void OnUpdate()
	{
		if (IsHand)
		{
			ComputeExpectedGrip();
		}
		SynchronizeBindingPose();
	}

	private void ComputeExpectedGrip()
	{
		if (LockGrip)
		{
			return;
		}
		int fingerContacts = 0;
		int tipcontacts = 0;
		int proximalContacts = 0;
		int partialContacts = 0;
		int intermediateContacts = 0;
		int anyFingerContacts = 0;
		bool[][] contactMatrix = new bool[5][]
		{
			new bool[3],
			new bool[3],
			new bool[3],
			new bool[3],
			new bool[3]
		};
		contactMatrix[4][0] = thumbColliders[0].Contacts.Count > 0;
		contactMatrix[4][1] = thumbColliders[1].Contacts.Count > 0;
		contactMatrix[4][2] = thumbColliders[2].Contacts.Count > 0;
		for (int i = 0; i < 4; i++)
		{
			int offset = i;
			int localContacts = 0;
			bool proximalContact = false;
			bool tipContact = false;
			bool intermediateContact = false;
			if (fingerColliders[offset].Contacts.Count > 0)
			{
				contactMatrix[offset][0] = true;
				tipcontacts++;
				localContacts++;
				tipContact = true;
				int up = 0;
				int down = 0;
				List<float> angles = new List<float>();
				foreach (ContactPoint contact in fingerColliders[offset].Contacts)
				{
					Vector3 normal = -contact.normal;
					float angle = Vector3.Angle(normal, contact.otherCollider.transform.up);
					angles.Add(angle);
					if (angle <= 90f)
					{
						up++;
					}
					else
					{
						down++;
					}
				}
			}
			if (fingerColliders[offset + 4].Contacts.Count > 0)
			{
				contactMatrix[offset][1] = true;
				localContacts++;
				intermediateContacts++;
				intermediateContact = true;
			}
			if (fingerColliders[offset + 8].Contacts.Count > 0)
			{
				contactMatrix[offset][2] = true;
				localContacts++;
				proximalContacts++;
				proximalContact = true;
			}
			if (localContacts >= 2)
			{
				fingerContacts++;
			}
			else if (tipContact && !proximalContact)
			{
				partialContacts++;
			}
			anyFingerContacts += localContacts;
		}
		bool palmContact = handCollider.Contacts.Count > 0;
		GripPose pose = gripController.CurrentPose;
		bool forcePose = false;
		if (proximalContacts >= 3 || intermediateContacts >= 3 || (palmContact && anyFingerContacts == 0))
		{
			pose = GripPose.FingersDown;
		}
		else if (gripController.CurrentPose == GripPose.FingersDown && fingerContacts < 2 && proximalContacts < 4 && gripController.TransitionComplete)
		{
			pose = GripPose.Idle;
		}
		else if (gripController.CurrentPose == GripPose.FingersDown && gripController.TransitionComplete && !palmContact)
		{
			pose = GripPose.Idle;
		}
		else if (gripController.CurrentPose == GripPose.FingersDown && gripController.TransitionComplete && proximalContacts == 0)
		{
			pose = GripPose.Idle;
		}
		else if (gripController.CurrentPose == GripPose.FingersDown && (proximalContacts > 0 || partialContacts > 0 || fingerContacts > 0))
		{
			pose = GripPose.FingersDown;
		}
		else if (gripController.TransitionComplete)
		{
			pose = GripPose.Idle;
		}
		if (pose == GripPose.FingersDown && fingerContacts <= 4 && gripController.TransitionComplete)
		{
			if (proximalContacts == 0 && intermediateContacts == 0)
			{
				pose = GripPose.Idle;
			}
			else if (tipcontacts < 3 && fingerContacts < 3)
			{
				pose = GripPose.Idle;
			}
			else if (proximalContacts >= 3 && tipcontacts != 4)
			{
				forcePose = true;
			}
		}
		if (ClothesInteraction.HasActiveSphere || !gripController.PoserEnabled)
		{
			pose = GripPose.Idle;
		}
		gripController.SetPose(pose, forcePose);
		gripController.SetContactMatrix(contactMatrix);
	}

	private void SynchronizeBindingPose()
	{
		if (IsHand)
		{
			if (hasContact && lastHandPose != handController.tipoDePose)
			{
				gripController.ResetIdlePose();
				lastHandPose = handController.tipoDePose;
			}
		}
		else if (lastHandPose != handController.tipoDePose)
		{
			gripController.ResetIdlePose();
			lastHandPose = handController.tipoDePose;
		}
		gripController.PoserEnabled = handController.tipoDePose == HandTipoDePose.massage && lastHandPose == HandTipoDePose.massage;
		if (gripController.PoserEnabled && DisableGripCondition())
		{
			gripWaitForRelax = true;
			gripController.PoserEnabled = false;
		}
	}

	private bool DisableGripCondition()
	{
		if (ClothesInteraction.HasActiveSphere)
		{
			return true;
		}
		if (gripWaitForRelax)
		{
			if (pickController.enabled) // pickController.w removed in SMA 23.1
			{
				return true;
			}
			gripWaitForRelax = false;
		}
		return false;
	}
}
