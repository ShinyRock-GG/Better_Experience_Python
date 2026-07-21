using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Physics;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class HandGrabService : SessionService
{
	private class GrabInstance
	{
		public ContactPoint? ContactPoint { get; private set; }

		public PuppetReceiverPart GrabPart { get; private set; }

		public FixedJoint GrabJoint { get; private set; }

		public bool IsGrab => GrabJoint != null;

		public bool CanGrab
		{
			get
			{
				if (!IsGrab)
				{
					return ContactPoint.HasValue;
				}
				return false;
			}
		}

		public PhysicalPuppet Puppet { get; internal set; }

		internal void SetCollision(CollisionTracker.CollisionKey key, List<ContactPoint> value)
		{
			foreach (ContactPoint k in value)
			{
				if (k.thisCollider.attachedRigidbody != null && k.otherCollider.attachedRigidbody != null)
				{
					ContactPoint = k;
					GrabPart = key.ReceiverPart;
					break;
				}
			}
		}

		internal void ResetCollision()
		{
			ContactPoint = null;
		}

		internal void Drop()
		{
			if (IsGrab)
			{
				Object.Destroy(GrabJoint);
				GrabJoint = null;
				Puppet.SetLimbRelaxed(GrabPart, relaxed: false);
			}
		}

		internal void Grab()
		{
			if (CanGrab)
			{
				ContactPoint contact = ContactPoint.Value;
				Collider partA = contact.thisCollider;
				Collider partB = contact.otherCollider;
				Rigidbody puppetBody = Puppet.GetDraggableRigidbody(GrabPart);
				if (puppetBody == null)
				{
					puppetBody = partA.attachedRigidbody;
				}
				GrabJoint = puppetBody.gameObject.AddComponent<FixedJoint>();
				GrabJoint.anchor = puppetBody.centerOfMass;
				GrabJoint.connectedBody = partB.attachedRigidbody;
				GrabJoint.connectedAnchor = partB.attachedRigidbody.centerOfMass;
				Puppet.SetLimbRelaxed(GrabPart, relaxed: true);
			}
		}

		internal bool ShouldBreakLink()
		{
			if (IsGrab && GrabJoint.currentForce.sqrMagnitude > 10000000f)
			{
				return true;
			}
			return false;
		}
	}

	private HandControllerV2 handController;

	private GrabInstance grabber = new GrabInstance();

	private SmartHandService smartHand;

	private bool IsHand
	{
		get
		{
			if (handController.tipoDePose != HandTipoDePose.massage)
			{
				return handController.tipoDePose == HandTipoDePose.finger;
			}
			return true;
		}
	}

	private bool IsGrabbing => grabber.IsGrab;

	private bool CanLock
	{
		get
		{
			if (IsHand && grabber.CanGrab)
			{
				return !smartHand.ClothesInteraction.HasActiveSphere;
			}
			return false;
		}
	}

	public override void OnStart()
	{
		handController = base.Session.Player.GameObject.GetComponentInChildren<HandControllerV2>();
		Plugin.DoUpdate.Add(OnUpdate, base.Scope);
		GuestCharacter guest = base.Session.Guest;
		PhysicalPuppet phy = new PhysicalPuppet(base.Session.Player.GameObject);
		List<Collider> palmColliders = phy.ColliderByName("CC_Base_Hand.R_Collider");
		List<Collider> fingerColliders = phy.ColliderByName("CC_Base_Finger12.R");
		Lookup<HandPhysicsService>().SubscribePhysics(new HandPhysicsService.CollisionListener
		{
			KeyFilter = delegate(CollisionTracker.CollisionKey key)
			{
				if (handController.tipoDePose == HandTipoDePose.massage)
				{
					return palmColliders.Contains(key.SenderCollider);
				}
				return handController.tipoDePose != HandTipoDePose.finger || key.SenderCollider.name == "CC_Base_Finger12.R";
			},
			Callback = OnPhysicsCallback
		}, base.Scope);
		grabber.Puppet = guest.Puppet;
		smartHand = base.Scope.Parent.Lookup<SmartHandService>();
	}

	public override void OnStop()
	{
		StopGrabbing();
	}

	private void OnUpdate()
	{
		HandleInput();
	}

	private void HandleInput()
	{
		if (IsGrabbing)
		{
			if (!IsHand || smartHand.ClothesInteraction.HasActiveSphere)
			{
				StopGrabbing();
			}
			else if (grabber.ShouldBreakLink())
			{
				StopGrabbing();
			}
		}
		HandleHoldActivation();
	}

	private void OnPhysicsCallback(Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>> collisions)
	{
		KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> pair = SelectWhereMostContact(collisions);
		if (pair.Value == null)
		{
			grabber.ResetCollision();
		}
		else
		{
			grabber.SetCollision(pair.Key, pair.Value);
		}
	}

	private KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> SelectWhereMostContact(Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>> collisions)
	{
		KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> selected = default(KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>>);
		foreach (KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> kv in collisions)
		{
			if (selected.Value == null || kv.Value.Count > selected.Value.Count)
			{
				selected = kv;
			}
		}
		return selected;
	}

	private void StopGrabbing()
	{
		grabber.Drop();
		smartHand.LockGrip = false;
	}

	private void StartGrabbing()
	{
		grabber.Grab();
		smartHand.LockGrip = true;
	}

	private void HandleHoldActivation()
	{
		if (Input.GetMouseButton(0))
		{
			if (CanLock)
			{
				StartGrabbing();
			}
		}
		else if (IsGrabbing)
		{
			StopGrabbing();
		}
	}
}
