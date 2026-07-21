using System;
using System.Collections.Generic;
using System.IO;
using BetterExperience.GameScopes;
using BetterExperience.Properties;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;

namespace BetterExperience.Features.AlternativeGenetics;

internal class GeneticsFactory
{
	private PersistenceService persistence;

	private PoolManager poolManager;

	private MultithreadingFeature multithreading;

	private AlternativeGeneticsActiveProfiles activeProfiles;

	private AlternativeGeneMapping Mapping { get; set; }

	public Dictionary<string, PoolingGroup> Groups { get; private set; }

	public Observable<GenePool, GeneSet> PostProcessGeneSet { get; } = new Observable<GenePool, GeneSet>();

	public Dictionary<GenePool, (int, int)> Migrations { get; } = new Dictionary<GenePool, (int, int)>();

	public GeneFactoryInfo GeneFactory { get; set; }

	public Dictionary<string, string> RatingSwap { get; set; } = new Dictionary<string, string>();

	public event Action<GenePool> PostCompact = delegate
	{
	};

	public event Action<GenePool, PoolStateSnapshot, PoolStateSnapshot> PostRefresh = delegate
	{
	};

	public event Action PoolsCreated = delegate
	{
	};

	public event Action<GenePool> OnNewEpochStart = delegate
	{
	};

	public GeneticsFactory(PersistenceService persistence, PoolManager poolManager, MultithreadingFeature multithreading)
	{
		GeneticsFactory geneticsFactory = this;
		this.persistence = persistence;
		this.poolManager = poolManager;
		this.multithreading = multithreading;
		if (poolManager.Count == 0)
		{
			ScopeSupport tmpScope = new ScopeSupport();
			tmpScope.Name = "DeferredPoolLoader";
			poolManager.Scope.AddChild(tmpScope);
			poolManager.OnPoolCreated.Add(delegate
			{
				geneticsFactory.GeneFactory = poolManager.GeneFactory;
				geneticsFactory.Load();
				tmpScope.Dispose();
			}, tmpScope);
		}
		else
		{
			GeneFactory = poolManager.GeneFactory;
			Load();
		}
	}

	public Dictionary<string, GenePool> GetPools()
	{
		Dictionary<string, GenePool> result = new Dictionary<string, GenePool>();
		foreach (PoolingGroup g in Groups.Values)
		{
			foreach (KeyValuePair<string, GenePool> kv in g.Pools)
			{
				result[kv.Key] = kv.Value;
			}
		}
		return result;
	}

	private void Load()
	{
		Mapping = persistence.Persisted(() => new AlternativeGeneMapping());
		activeProfiles = persistence.Persisted(() => new AlternativeGeneticsActiveProfiles());
		AlternativeGeneticsInit protopool = persistence.Persisted(delegate
		{
			string path = Path.Combine(persistence.Dir, "AlternativeGeneticsInit.json");
			File.WriteAllBytes(path, Resources.AlternativeGeneticsInit);
			return persistence.Persisted(() => (AlternativeGeneticsInit)null);
		});
		Groups = new Dictionary<string, PoolingGroup>();
		foreach (KeyValuePair<string, PoolSettingsInit> kv in protopool)
		{
			PoolingGroupData groupdata = LoadGroupProfile(kv.Key, activeProfiles.GetValueOrAdd(kv.Key, () => "default"));
			if (groupdata == null)
			{
				groupdata = new PoolingGroupData
				{
					Settings = kv.Value.Settings
				};
			}
			PoolingGroup poolingGroup = (Groups[kv.Key] = new PoolingGroup(groupdata, multithreading));
			PoolingGroup group = poolingGroup;
			if (group.Data.Settings.Profile == "default")
			{
				foreach (KeyValuePair<string, PoolSettings> pooli in kv.Value.Pools)
				{
					GenePoolData pooldata = groupdata.Pools.GetValueOrAdd(pooli.Key, () => new GenePoolData
					{
						Settings = pooli.Value
					});
				}
			}
			List<string> poolsToErase = new List<string>();
			foreach (KeyValuePair<string, GenePoolData> pooli2 in groupdata.Pools)
			{
				GenePoolData pooldata2 = pooli2.Value;
				GenePool pool = new GenePool(pooldata2, GeneFactory, Mapping);
				group.Pools[pooli2.Key] = pool;
				if (groupdata.Settings.Profile == "default")
				{
					if (kv.Value.Pools.ContainsKey(pooli2.Key))
					{
						pool.MigrateSettings(kv.Value.Pools[pooli2.Key]);
					}
					else
					{
						poolsToErase.Add(pooli2.Key);
					}
				}
				if (!poolsToErase.Contains(pooli2.Key))
				{
					pool.OnNewEpochStart += delegate(GenePool sender)
					{
						this.OnNewEpochStart(sender);
					};
				}
			}
			foreach (string pid in poolsToErase)
			{
				groupdata.Pools.Remove(pid);
				group.Pools.Remove(pid);
			}
			group.MigrateSettings(kv.Value.Settings);
			group.Create();
		}
		this.PoolsCreated();
	}

	internal void SetProfile(string groupId, string profileId)
	{
		activeProfiles[groupId] = profileId;
	}

	public PoolingGroupData LoadGroupProfile(string groupId, string profileId)
	{
		string fname = GetFileName(groupId, profileId);
		return persistence.Persisted(() => (PoolingGroupData)null, fname);
	}

	private static string GetFileName(string groupId, string profileId)
	{
		return Path.Combine("ag_profiles", groupId + "_" + profileId);
	}

	public void Save()
	{
		foreach (KeyValuePair<string, PoolingGroup> kv in Groups)
		{
			string groupId = kv.Key;
			PoolingGroup group = kv.Value;
			persistence.Persist(group.Data, GetFileName(groupId, group.Data.Settings.Profile));
		}
		persistence.Persist(activeProfiles);
	}

	internal void SaveProfile(string groupId, PoolingGroupData Data)
	{
		persistence.Persist(Data, GetFileName(groupId, Data.Settings.Profile));
	}

	public void UpdateStatistics(GuestInstance guest)
	{
		Dictionary<GeneId, GeneInfoEx> genes1 = ExtractGenes(guest);
		SwapRatings(guest, genes1);
		UpdateStatistics(guest.Id, genes1);
	}

	private void SwapRatings(GuestInstance guest, Dictionary<GeneId, GeneInfoEx> genes1)
	{
		if (RatingSwap.Count == 0)
		{
			return;
		}
		Dictionary<string, float> ratings = new Dictionary<string, float>(guest.Rating);
		foreach (KeyValuePair<string, string> kv in RatingSwap)
		{
			string ga = kv.Key;
			string gb = kv.Value;
			if (ratings.TryGetValue(ga, out var ra) && ratings.TryGetValue(gb, out var rb))
			{
				ratings[ga] = rb;
				ratings[gb] = ra;
			}
			else
			{
				new Logger().Error("Unable to swap group ratings: {0}<>{1}", ga, gb);
			}
		}
		foreach (GeneInfoEx gene in genes1.Values)
		{
			if (ratings.TryGetValue(gene.Group, out var rate))
			{
				gene.Rating = rate;
				continue;
			}
			new Logger().Error("Unable to find group rating: {0}", gene.Group);
		}
	}

	private void UpdateStatistics(string id, Dictionary<GeneId, GeneInfoEx> genes)
	{
		foreach (PoolingGroup group in Groups.Values)
		{
			if (group.Data.Enabled && group.Data.Active)
			{
				group.UpdateStatistics(id, genes);
			}
		}
	}

	internal Dictionary<PoolingGroup, PoolingGroup.GeneSetGroup> Apply(GuestInstance guest)
	{
		foreach (string k in guest.Rating.Keys)
		{
			bool found = false;
			foreach (PoolingGroup group in Groups.Values)
			{
				foreach (GenePool pool in group.Pools.Values)
				{
					if (pool.Data.Settings.Groups.Contains(k))
					{
						found = true;
						break;
					}
				}
			}
			if (!found)
			{
				new Logger().Error("Gene group {0} is not mapped", k);
			}
		}
		Dictionary<GeneId, GeneInfoEx> stats = ExtractGenes(guest);
		Dictionary<PoolingGroup, PoolingGroup.GeneSetGroup> results = new Dictionary<PoolingGroup, PoolingGroup.GeneSetGroup>();
		foreach (PoolingGroup group2 in Groups.Values)
		{
			PoolingGroup.GeneSetGroup result = group2.Apply(guest, stats.Values);
			results[group2] = result;
		}
		return results;
	}

	private Dictionary<GeneId, GeneInfoEx> ExtractGenes(GuestInstance guest)
	{
		Dictionary<GeneId, GeneInfoEx> stats = guest.ExtractAll();
		if (Mapping.Count > 0)
		{
			foreach (GeneInfoEx gene in stats.Values)
			{
				if (Mapping.TryGetValue(gene.Id.Item1, out var newGroup))
				{
					gene.Rating = guest.Rating[newGroup];
					gene.Group = newGroup;
				}
			}
		}
		return stats;
	}

	internal List<GenePool> ListAppliedPools(GuestInstance guestInstance)
	{
		List<GenePool> applied = new List<GenePool>();
		foreach (PoolingGroup group in Groups.Values)
		{
			foreach (GenePool pool in group.Pools.Values)
			{
				string genesetid = pool.GetMappedGeneset(guestInstance.Id);
				if (genesetid != null)
				{
					List<GeneSet> yg = pool.GetGeneration(GeneGeneration.Young);
					GeneSet geneset = yg.Find((GeneSet gs) => genesetid == gs.Id);
					if (geneset != null && geneset.Ancestors.Count > 0)
					{
						applied.Add(pool);
					}
				}
			}
		}
		return applied;
	}

	internal List<(GenePool, GeneSet)> ListAppliedSets(GuestInstance guestInstance)
	{
		List<(GenePool, GeneSet)> applied = new List<(GenePool, GeneSet)>();
		foreach (PoolingGroup group in Groups.Values)
		{
			foreach (GenePool pool in group.Pools.Values)
			{
				string genesetid = pool.GetMappedGeneset(guestInstance.Id);
				if (genesetid != null)
				{
					List<GeneSet> yg = pool.GetGeneration(GeneGeneration.Young);
					GeneSet geneset = yg.Find((GeneSet gs) => genesetid == gs.Id);
					if (geneset != null && geneset.Ancestors.Count > 0)
					{
						applied.Add((pool, geneset));
					}
				}
			}
		}
		return applied;
	}
}
