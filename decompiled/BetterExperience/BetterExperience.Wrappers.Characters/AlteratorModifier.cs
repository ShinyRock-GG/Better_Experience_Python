using System;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;

namespace BetterExperience.Wrappers.Characters;

public class AlteratorModifier
{
	private ModificadoresDeAlterador modifiers;

	private Action invalidateCallback;

	private Alterador alterator;

	public string Name => modifiers.alteradorName;

	public int Count => modifiers.modificadores.Length;

	public float this[int index]
	{
		get
		{
			return modifiers.modificadores[index];
		}
		set
		{
			modifiers.modificadores[index] = value;
			Invalidate();
		}
	}

	public AlteratorModifier(ModificadoresDeAlterador modifiers, Action invalidateCallback, Alterador alterador2)
	{
		this.modifiers = modifiers;
		this.invalidateCallback = invalidateCallback;
		alterator = alterador2;
	}

	public void Invalidate()
	{
		invalidateCallback();
		modifiers.TryApply(alterator, null);
	}
}
