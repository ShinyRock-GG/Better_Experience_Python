using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

public class ModifierManager
{
	private AlteradoresDeAparienciaFemenina meshModifiers;

	private AlteradoresDePersonalidadFemenina scriptModifiers;

	private Dictionary<string, AlteratorModifier> alteradors = new Dictionary<string, AlteratorModifier>();

	public IReadOnlyDictionary<string, AlteratorModifier> Modifiers => alteradors;

	public ModifierManager(GameObject owner)
	{
		// SMA 23.1 - CONFIRMED root cause (this is NOT a mystery band-aid).
		//
		// AlteradoresDeAparienciaFemenina (mesh/body sliders) and AlteradoresDePersonalidadFemenina
		// (personality modifiers) ARE present on the character in 23.1 -- the runtime log shows
		// "component=found mapaDeValores=NULL", i.e. the component is found; only its value map is null.
		//
		// mapaDeValores is a public field on AlteradoresAdmin<T_Mapa> that is populated by the
		// character-load pipeline, NOT by the component itself: ScenaCharacteresManager assigns it
		// (ScenaCharacteresManager: "componentEnRoot.mapaDeValores = mapa") when a real interview
		// character's appearance/genetics map is loaded (also RazaData / DEBUG_MapLoaderToFemale).
		//
		// The static ScenaConMainProtagonistaFemenina shell ('Female Avatar Root Haired') never runs
		// that load, so the component exists but mapaDeValores stays null -- the SAME root cause as
		// GuestInstance==null (no genetics backing a static character). The components are NOT removed,
		// renamed, or relocated. The null guards below are therefore CORRECT, not band-aids: with no
		// value map there are simply no modifiers to expose, and SynchronizeCharacterWithInstance()
		// correctly no-ops (nothing to sync). On a normal pool/loaded character both maps are
		// populated and the modifiers work exactly as before -- no 23.1 change is needed here.
		meshModifiers = owner.GetComponentInChildren<AlteradoresDeAparienciaFemenina>();
		scriptModifiers = owner.GetComponentInChildren<AlteradoresDePersonalidadFemenina>();

		// SMA 23.1: Two-level null guard required.
		// First crash (IL 0x00029): meshModifiers itself was null — component not found.
		// Second crash (IL 0x00057): meshModifiers was found but mapaDeValores is null —
		// component exists on the character in 23.1 but is not initialized/populated.
		// Both levels must be guarded independently.
		// Same pattern applies to scriptModifiers below.
		if (meshModifiers == null || meshModifiers.mapaDeValores == null)
		{
			new Logger().Info("[BE] ModifierManager: mesh modifier chain unavailable on '{0}' (component={1} mapaDeValores={2}) — mesh modifiers disabled",
				owner.name,
				meshModifiers == null ? "NULL" : "found",
				meshModifiers != null && meshModifiers.mapaDeValores == null ? "NULL" : "found");
		}
		else
		{
			(from x in meshModifiers.mapaDeValores.ObtenerAlteradorModificadores()
				select new AlteratorModifier(x, _InvalidateMesh, meshModifiers.Obtener(x.alteradorName))).ForEach(delegate(AlteratorModifier x)
			{
				alteradors.Add(x.Name, x);
			});
		}
		if (scriptModifiers == null || scriptModifiers.mapaDeValores == null)
		{
			new Logger().Info("[BE] ModifierManager: script modifier chain unavailable on '{0}' (component={1} mapaDeValores={2}) — script modifiers disabled",
				owner.name,
				scriptModifiers == null ? "NULL" : "found",
				scriptModifiers != null && scriptModifiers.mapaDeValores == null ? "NULL" : "found");
		}
		else
		{
			(from x in scriptModifiers.mapaDeValores.ObtenerAlteradorModificadores()
				select new AlteratorModifier(x, _InvalidateScript, scriptModifiers.Obtener(x.alteradorName))).ForEach(delegate(AlteratorModifier x)
			{
				alteradors.Add(x.Name, x);
			});
		}
	}

	private void _InvalidateMesh()
	{
		meshModifiers.flagToForceUpdateValores = true;
	}

	private void _InvalidateScript()
	{
		scriptModifiers.flagToForceUpdateValores = true;
	}
}
