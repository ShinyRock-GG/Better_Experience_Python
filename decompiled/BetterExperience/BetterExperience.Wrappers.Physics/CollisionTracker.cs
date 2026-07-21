using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Skins;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Wrappers.Physics;

internal class CollisionTracker
{
	public struct CollisionKey : IEquatable<CollisionKey>
	{
		public PuppetSenderPart SenderPart { get; set; }

		public Rigidbody Sender { get; set; }

		public PuppetReceiverPart ReceiverPart { get; set; }

		public Rigidbody Receiver { get; set; }

		public Collider ReceiverCollider { get; set; }

		public Collider SenderCollider { get; internal set; }

		public override bool Equals(object obj)
		{
			if (obj is CollisionKey key)
			{
				return Equals(key);
			}
			return false;
		}

		public bool Equals(CollisionKey other)
		{
			if (SenderPart == other.SenderPart && EqualityComparer<Rigidbody>.Default.Equals(Sender, other.Sender) && ReceiverPart == other.ReceiverPart)
			{
				return EqualityComparer<Rigidbody>.Default.Equals(Receiver, other.Receiver);
			}
			return false;
		}

		public override int GetHashCode()
		{
			int hashCode = -219435366;
			hashCode = hashCode * -1521134295 + SenderPart.GetHashCode();
			hashCode = hashCode * -1521134295 + EqualityComparer<Rigidbody>.Default.GetHashCode(Sender);
			hashCode = hashCode * -1521134295 + ReceiverPart.GetHashCode();
			return hashCode * -1521134295 + EqualityComparer<Rigidbody>.Default.GetHashCode(Receiver);
		}
	}

	public PhysicalPuppet Sender { get; }

	public PhysicalPuppet Receiver { get; }

	public BitMask<PuppetSenderPart> SenderFilter { get; set; } = BitMask<PuppetSenderPart>.AllOf();

	public BitMask<PuppetReceiverPart> ReceiverFilter { get; set; } = BitMask<PuppetReceiverPart>.AllOf();

	public Func<bool> GlobalFilter { get; set; } = () => true;

	public Observable<HitPartEnum, PuppetSenderPart, Collision> OnCollision { get; } = new Observable<HitPartEnum, PuppetSenderPart, Collision>();

	public Dictionary<CollisionKey, List<ContactPoint>> Collisions { get; } = new Dictionary<CollisionKey, List<ContactPoint>>();

	public Func<Collision, bool> CollisionFilter { get; set; } = (Collision c) => true;

	public bool CollectCollisions { get; set; } = true;

	public CollisionTracker(PhysicalPuppet sender, PhysicalPuppet receiver)
	{
		Sender = sender;
		Receiver = receiver;
	}

	public void Start(ScopeSupport scope)
	{
		Receiver.OnSkinCollisionStay.Add(OnReceive, scope);
	}

	private void OnReceive(HitSkin arg1, Collision arg2)
	{
		if (!GlobalFilter())
		{
			return;
		}
		PuppetSenderPart? sender = Sender.GetBodyPart(arg2.rigidbody);
		if (!sender.HasValue)
		{
			return;
		}
		PuppetReceiverPart receiver = (PuppetReceiverPart)arg1.hitParte;
		if (!CollisionFilter(arg2) || !SenderFilter.Contains(sender.Value) || !ReceiverFilter.Contains(receiver))
		{
			return;
		}
		OnCollision.Invoke(arg1.hitParte, sender.Value, arg2);
		if (!CollectCollisions)
		{
			return;
		}
		ContactPoint[] contacts = arg2.contacts;
		for (int i = 0; i < contacts.Length; i++)
		{
			ContactPoint point = contacts[i];
			CollisionKey key = new CollisionKey
			{
				ReceiverPart = receiver,
				Receiver = point.thisCollider.attachedRigidbody,
				ReceiverCollider = point.thisCollider,
				SenderPart = sender.Value,
				Sender = point.otherCollider.attachedRigidbody,
				SenderCollider = point.otherCollider
			};
			Collisions.GetValueOrAdd(key, () => new List<ContactPoint>()).Add(point);
		}
	}
}
