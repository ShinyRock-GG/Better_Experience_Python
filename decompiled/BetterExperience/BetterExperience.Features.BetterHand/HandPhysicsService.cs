using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Characters.Controlladores;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Physics;
using UnityEngine;
using UnityEngine.Pool;

namespace BetterExperience.Features.BetterHand;

internal class HandPhysicsService : SessionService
{
	public class CollisionListener
	{
		public Func<CollisionTracker.CollisionKey, bool> KeyFilter { get; set; }

		public Action<Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>>> Callback { get; set; }
	}

	private CollisionTracker tracker;

	private HandControllerV2 handController;

	private List<CollisionListener> subscibers = new List<CollisionListener>();

	public override void OnStart()
	{
		base.OnStart();
		handController = base.Session.Player.GameObject.GetComponentInChildren<HandControllerV2>();
	}

	public void SubscribePhysics(CollisionListener listener, ScopeSupport scope)
	{
		if (tracker == null)
		{
			CreateTracker();
		}
		subscibers.Add(listener);
		scope.OnDispose += delegate
		{
			subscibers.Remove(listener);
		};
	}

	private void CreateTracker()
	{
		GuestCharacter guest = base.Session.Guest;
		PhysicalPuppet playerPuppet = new PhysicalPuppet(base.Session.Player.GameObject);
		tracker = new CollisionTracker(playerPuppet, guest.Puppet);
		tracker.GlobalFilter = () => handController.tipoDePose == HandTipoDePose.massage;
		tracker.SenderFilter = BitMask<PuppetSenderPart>.Of(PuppetSenderPart.handR);
		tracker.Start(base.Scope);
		Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
	}

	private void OnUpdate()
	{
		Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>> tmp;
		using (CollectionPool<Dictionary<CollisionTracker.CollisionKey, List<ContactPoint>>, KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>>>.Get(out tmp))
		{
			foreach (CollisionListener listener in subscibers)
			{
				tmp.Clear();
				if (listener.KeyFilter == null)
				{
					listener.Callback(tracker.Collisions);
					continue;
				}
				foreach (KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> kv in tracker.Collisions)
				{
					if (kv.Value.Count != 0 && listener.KeyFilter(kv.Key))
					{
						tmp.Add(kv.Key, kv.Value);
					}
				}
				listener.Callback(tmp);
				tmp.Clear();
			}
			foreach (KeyValuePair<CollisionTracker.CollisionKey, List<ContactPoint>> collision in tracker.Collisions)
			{
				collision.Value.Clear();
			}
		}
	}
}
