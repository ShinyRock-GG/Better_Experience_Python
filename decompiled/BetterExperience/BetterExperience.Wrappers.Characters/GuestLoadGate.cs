using System;
using System.Collections;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.Globales.Updater;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

// SMA 23.1: interview characters load ASYNCHRONOUSLY. The model's genetics (AlteradoresDe*.mapaDeValores),
// its NPC identity bind and the NPCCharacter subject all land a few seconds AFTER the `stared` event that
// BE currently materializes on. Reading at `stared` captures a bare, unbound, geneless prefab -- which is
// why BE reported the character as "geneless". Confirmed by GuestDiagnostics: at T0 mapaDeValores=NULL /
// isBinded=False / NPCCharacter ABSENT; by T+3s all are populated (15 appearance + 6 personality gene-sets).
//
// This gate waits until the async load has landed before running BE's gene-dependent materialize, with a
// timeout fallback so it can never hang. mapaDeValores is used as the "loaded" signal because it is exactly
// what the modifier system needs and it flips together with the NPC bind.
public static class GuestLoadGate
{
	public static void WhenLoaded(FemaleChar character, Action onLoaded, float timeoutSeconds = 20f)
	{
		if (onLoaded == null)
		{
			return;
		}
		try
		{
			if (character == null || IsLoaded(character))
			{
				onLoaded();
				return;
			}
			GlobalUpdater updater = GlobalUpdater.instancia;
			if (updater == null)
			{
				new Logger().Info("[BE] GuestLoadGate: no GlobalUpdater -- proceeding immediately (un-deferred)");
				onLoaded();
				return;
			}
			updater.StartCoroutine(WaitRoutine(character, onLoaded, timeoutSeconds));
		}
		catch (Exception ex)
		{
			new Logger().Error("[BE] GuestLoadGate: failed to start wait ({0}) -- proceeding immediately", ex.GetType().Name);
			onLoaded();
		}
	}

	private static bool IsLoaded(FemaleChar character)
	{
		AlteradoresDeAparienciaFemenina apar = character.GetComponentEnRoot<AlteradoresDeAparienciaFemenina>();
		// No modifier component at all -> nothing gene-dependent to wait for; treat as ready.
		if (apar == null)
		{
			return true;
		}
		return apar.mapaDeValores != null;
	}

	private static IEnumerator WaitRoutine(FemaleChar character, Action onLoaded, float timeoutSeconds)
	{
		Logger log = new Logger();
		float elapsed = 0f;
		while (character != null && !IsLoaded(character) && elapsed < timeoutSeconds)
		{
			yield return new WaitForSeconds(0.2f);
			elapsed += 0.2f;
		}
		if (character == null)
		{
			log.Info("[BE] GuestLoadGate: character destroyed before load completed -- aborting materialize");
			yield break;
		}
		bool loaded = IsLoaded(character);
		log.Info("[BE] GuestLoadGate: proceeding after {0:0.0}s (loaded={1}{2})", elapsed, loaded,
			loaded ? "" : " -- TIMEOUT, materializing geneless as fallback");
		onLoaded();
	}
}
