using System;
using System.Collections.Generic;
using Assets._ReusableScripts.Genetica.NPCs;
using Assets.Productos.Juegos.Reception.Scripts.Genetica.Globales;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;

namespace BetterExperience.Wrappers.Pools;

public class PoolManager
{
	private List<GuestPool> pools = new List<GuestPool>();

	public ScopeSupport Scope { get; } = new ScopeSupport
	{
		Name = "PoolManager"
	};

	public Observable<GuestPool, GuestInstance> OnGuestClassified { get; } = new Observable<GuestPool, GuestInstance>();

	public Observable<GuestInstance> OnGuestLoaded { get; } = new Observable<GuestInstance>();

	public Observable<GuestPool> OnPoolCreated { get; } = new Observable<GuestPool>();

	public PiscinasDeNPCs Instance
	{
		get
		{
			if (Singleton<PiscinasDeNPCs>.IsInScene)
			{
				return Singleton<PiscinasDeNPCs>.instance;
			}
			throw new Exception("PoolManager not found");
		}
	}

	public int Count => pools.Count;

	public GeneFactoryInfo GeneFactory
	{
		get
		{
			if (pools.Count <= 0)
			{
				return null;
			}
			return pools[0].GeneFactory;
		}
	}

	public object Impl { get; private set; }

	public GuestPool AnyPool
	{
		get
		{
			if (Count <= 0)
			{
				return null;
			}
			return pools[0];
		}
	}

	public PoolManager()
	{
		SMAGlobalPatches.OnNewPoolCreated.Add(SMAGlobalPatches_OnNewPoolCreated, Scope);
		SMAGlobalPatches.OnPoolDestroyed.Add(SMAGlobalPatches_OnPoolDestroyed, Scope);
		SMAGlobalPatches.OnGuestClassified.Add(SMAGlobalPatches_OnGuestClassified, Scope);
		SMAGlobalPatches.BeforeCharacterLoaded.Add(SMAGlobalPatches_BeforeCharacterLoaded, Scope);
		foreach (PiscinaDeNpcsManager pooli in (IEnumerable<PiscinaDeNpcsManager>)Instance)
		{
			pools.Add(new GuestPool(pooli));
		}
	}

	private void SMAGlobalPatches_BeforeCharacterLoaded(ISujetoIdentificableNpc obj)
	{
		GuestInstance guest = FindGuest(obj);
		if (guest == null)
		{
			RefreshPools();
		}
		guest = FindGuest(obj);
		if (guest == null)
		{
			new Logger().Error("OOPS, guest {0} not found in wrappers", obj.NpcID.ToString());
		}
		else
		{
			OnGuestLoaded.Invoke(guest);
		}
	}

	private void RefreshPools()
	{
		foreach (GuestPool pool in pools)
		{
			pool.Invalidate();
		}
	}

	public GuestInstance FindGuest(ISujetoIdentificableNpc sujeto)
	{
		foreach (GuestPool pool in pools)
		{
			foreach (GuestInstance gi in pool.Guests)
			{
				if (gi.Instance == sujeto)
				{
					return gi;
				}
			}
		}
		return null;
	}

	public GuestInstance FindGuest(string sujetoId)
	{
		foreach (GuestPool pool in pools)
		{
			foreach (GuestInstance gi in pool.Guests)
			{
				if (gi.Id == sujetoId)
				{
					return gi;
				}
			}
		}
		return null;
	}

	private void SMAGlobalPatches_OnGuestClassified(PiscinaDeNpcsManager poolRef, ISujetoIdentificableNpc guestRef)
	{
		GuestPool pool = GetPool(poolRef);
		if (pool == null)
		{
			return;
		}
		foreach (GuestInstance guest in pool.Guests)
		{
			if (guest.Instance == guestRef)
			{
				OnGuestClassified.Invoke(pool, guest);
				break;
			}
		}
	}

	private void SMAGlobalPatches_OnPoolDestroyed(PiscinaDeNpcsManager obj)
	{
		GuestPool pool = GetPool(obj);
		if (pool != null)
		{
			pools.Remove(pool);
		}
	}

	private void SMAGlobalPatches_OnNewPoolCreated(PiscinaDeNpcsManager obj)
	{
		if (Instance.EsValida(obj))
		{
			GuestPool pool = new GuestPool(obj);
			pools.Add(pool);
			OnPoolCreated.Invoke(pool);
		}
	}

	private GuestPool GetPool(PiscinaDeNpcsManager pool)
	{
		foreach (GuestPool x in pools)
		{
			if (x.Instance == pool)
			{
				return x;
			}
		}
		return null;
	}
}
