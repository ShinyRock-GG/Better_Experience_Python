using System;
using System.Collections;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.AI.Ropa;
using BepInEx.Configuration;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class ClothesInteractionSupport : SessionService
{
	private Renderer[] renderers;

	private IInputHandle lmbKey;

	public bool HasActiveSphere { get; private set; }

	public bool HasGrabbingSphere { get; private set; }

	public Observable<bool> OnHasActiveSphereChanged { get; } = new Observable<bool>();

	public override void OnStart()
	{
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		base.OnStart();
		GuiaDeRopaInteractable[] interactiblePiceces = base.Session.Guest.Impl.GetComponentsInChildren<GuiaDeRopaInteractable>();
		renderers = interactiblePiceces.Select((GuiaDeRopaInteractable x) => x.gameObject.GetComponentInChildren<Renderer>(includeInactive: true)).ToArray();
		lmbKey = Lookup<DispatcherService>().Input.KeyboardEvent(new KeyboardShortcut(KeyCode.Mouse0, Array.Empty<KeyCode>()), base.Scope);
		Lookup<DispatcherService>().StartCoroutine(TrackerThread(), base.Scope);
	}

	private IEnumerator TrackerThread()
	{
		while (true)
		{
			if (HasActiveSphere)
			{
				yield return null;
			}
			else
			{
				yield return new WaitForSeconds(0.1f);
			}
			bool active = false;
			bool grabbing = false;
			if (lmbKey.IsHold)
			{
				Renderer[] array = renderers;
				foreach (Renderer sphere in array)
				{
					if (!(sphere == null) && sphere.gameObject.activeSelf)
					{
						bool active2 = sphere.material.color == Color.green;
						bool active3 = sphere.material.color == Color.cyan;
						active = active || active2 || active3;
						grabbing = grabbing || active3;
					}
				}
			}
			HasGrabbingSphere = grabbing;
			if (HasActiveSphere != active)
			{
				HasActiveSphere = active;
				OnHasActiveSphereChanged.Invoke(active);
			}
		}
	}
}
