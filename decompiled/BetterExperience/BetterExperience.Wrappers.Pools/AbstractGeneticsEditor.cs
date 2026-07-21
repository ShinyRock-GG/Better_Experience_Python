using System;
using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores.Clases;
using Assets._ReusableScripts.Genetica;
using Assets.Base.Plugins.Runtime;

namespace BetterExperience.Wrappers.Pools;

internal abstract class AbstractGeneticsEditor<T> : IGeneticsEditor, IDisposable where T : ISujeto
{
	protected T impl;

	protected GuestPool pool;

	protected abstract IReadOnlyList<ModificadoresDeAlterador> Prefab { get; }

	protected abstract IReadOnlyList<ModificadoresDeAlterador> Current { get; }

	public AbstractGeneticsEditor(T impl, GuestPool pool)
	{
		this.impl = impl;
		this.pool = pool;
	}

	public virtual void Dispose()
	{
		impl.FixSymetria();
	}

	protected void RandomizeValues()
	{
		ref T reference = ref impl;
		object source = impl;
		reference.Randomizar((ISujeto)source, 0, TipoDeRandomizadoParaSujeto.guiada);
	}

	public void Randomize(int cycles = 1)
	{
		Reset();
		while (cycles-- > 0)
		{
			RandomizeValues();
		}
	}

	public void Reset()
	{
		Dictionary<string, ModificadoresDeAlterador> dicc = Prefab.ToDictionary((ModificadoresDeAlterador m) => m.alteradorName);
		Dictionary<string, ModificadoresDeAlterador> modifiers = Current.ToDictionary((ModificadoresDeAlterador x) => x.alteradorName);
		foreach (KeyValuePair<string, ModificadoresDeAlterador> kv in dicc)
		{
			if (modifiers.TryGetValue(kv.Key, out var current))
			{
				ModificadoresDeAlterador standard = kv.Value;
				for (int i = 0; i < current.modificadores.Length; i++)
				{
					current.modificadores[i] = standard.modificadores[i];
				}
			}
		}
	}
}
