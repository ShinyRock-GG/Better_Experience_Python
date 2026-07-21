using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BetterExperience.Features.Console;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.AlternativeGenetics;

internal class AlternativeGeneticsService : SessionService
{
	private class GeneticsOverlay
	{
		private GridLayout guiGrid;

		private GeneticsFactory factory;

		private PersistenceService persistence;

		private DispatcherService dispatcher;

		private GeneticsOverlaySettings settings;

		private Dictionary<string, Action<GenePool>> PoolGuiUpdater = new Dictionary<string, Action<GenePool>>();

		public GeneticsOverlay(GridLayout mainGrid, AlternativeGeneticsService service)
		{
			guiGrid = mainGrid;
			factory = service.Factory;
			persistence = service.Scope.Lookup<PersistenceService>();
			dispatcher = service.Scope.Lookup<DispatcherService>();
			settings = persistence.Persisted(() => new GeneticsOverlaySettings());
		}

		internal void ReloadTable()
		{
			while (guiGrid.Children.Count > 0)
			{
				guiGrid.Remove(guiGrid.Children[0]);
			}
			foreach (KeyValuePair<string, PoolingGroup> groupAndId in factory.Groups)
			{
				PoolingGroup g = groupAndId.Value;
				DrawableButton toggle = guiGrid.Button("");
				toggle.PreferredSize = new Vector2(10f, 10f);
				toggle.Position = new Vector2(0f, 2f);
				toggle.OnClick += delegate
				{
					settings.Toggle(groupAndId.Key);
					dispatcher.InvokeLater(ReloadTable);
				};
				guiGrid.Label("     " + g.Data.Settings.Name + " - " + g.Data.Settings.Profile);
				guiGrid.NewLine();
				if (settings.IsHidden(groupAndId.Key))
				{
					continue;
				}
				KeyValuePair<string, GenePool>[] pools = g.Pools.ToArray();
				KeyValuePair<string, GenePool>[] array = pools;
				for (int num = 0; num < array.Length; num++)
				{
					KeyValuePair<string, GenePool> kv = array[num];
					string poolId = kv.Key;
					GenePool pool = kv.Value;
					if (pool.Data.Settings.Hidden)
					{
						continue;
					}
					DrawableLabel incompletion = guiGrid.Label("+");
					guiGrid.Add(new DrawableLabel(kv.Value.Data.Settings.Name));
					guiGrid.Add(new DrawableLabel("["));
					DrawableLabel epochAndError = guiGrid.Add(new DrawableLabel("999 00.0"));
					guiGrid.Add(new DrawableLabel("]"));
					guiGrid.Add(new DrawableLabel("O"));
					DrawableLabel osize = guiGrid.Add(new DrawableLabel("00"));
					guiGrid.Add(new DrawableLabel("M"));
					DrawableLabel msize = guiGrid.Add(new DrawableLabel("11"));
					guiGrid.Add(new DrawableLabel("Y"));
					DrawableLabel ysize = guiGrid.Add(new DrawableLabel("22"));
					DrawableLabel remaining = guiGrid.Add(new DrawableLabel("99"));
					HLayout<Drawable> poolRow = guiGrid.Children[guiGrid.Children.Count - 1];
					Action<GenePool> update = delegate(GenePool apool)
					{
						List<GeneSet> generation = apool.GetGeneration(GeneGeneration.Old);
						List<GeneSet> generation2 = apool.GetGeneration(GeneGeneration.Mature);
						List<GeneSet> generation3 = apool.GetGeneration(GeneGeneration.Young);
						poolRow.Visible = apool.Data.Active && apool.Data.Enabled;
						if (poolRow.Visible && pool.Data.GeneOrder.Count > 0 && pool.UnguidedGeneCount() == 0)
						{
							poolRow.Visible = false;
						}
						poolRow.Transient = !poolRow.Visible;
						incompletion.Text = ((generation2.Count > 0) ? "+ " : "- ");
						epochAndError.Text = apool.Data.Epoch.ToString("D3") + " " + apool.Data.Error.ToString("F1");
						osize.Text = generation.Count.ToString("D2");
						msize.Text = generation2.Count.ToString("D2");
						ysize.Text = generation3.Count.ToString("D2");
						remaining.Text = apool.Remaining.ToString("D2");
					};
					update(pool);
					PoolGuiUpdater[poolId] = update;
					guiGrid.NewLine();
				}
			}
		}

		internal void Save()
		{
			persistence.Persist(settings);
		}

		internal void UpdateGuiInfo()
		{
			foreach (PoolingGroup g in factory.Groups.Values)
			{
				foreach (KeyValuePair<string, GenePool> kv in g.Pools)
				{
					if (PoolGuiUpdater.TryGetValue(kv.Key, out var updater))
					{
						updater(kv.Value);
					}
				}
			}
		}
	}

	private class GeneticsOverlaySettings
	{
		public List<string> HiddenGroups { get; set; } = new List<string>();

		internal void Toggle(string key)
		{
			if (HiddenGroups == null)
			{
				HiddenGroups = new List<string>();
			}
			if (!HiddenGroups.Contains(key))
			{
				HiddenGroups.Add(key);
			}
			else
			{
				HiddenGroups.Remove(key);
			}
		}

		internal bool IsHidden(string key)
		{
			if (HiddenGroups != null)
			{
				return HiddenGroups.Contains(key);
			}
			return false;
		}
	}

	private GeneticsFactory factory;

	private OverlayService overlayService;

	private GridLayout guiGrid;

	private GeneticsOverlay overlay;

	private ConsoleCommands consoleCommands;

	public Observable<GuestInstance> OnGuestModified { get; private set; } = new Observable<GuestInstance>();

	public bool ShowOverlay { get; set; } = true;

	public int MaxModifiedLevel { get; set; } = int.MaxValue;

	public bool SwapSlutnesAndModeling { get; set; }

	public bool DisableAutorating { get; set; } = true;

	public GeneticsFactory Factory => factory;

	public override void OnStart()
	{
		if (!base.Session.SingleMode)
		{
			consoleCommands = new ConsoleCommands(this);
			factory = new GeneticsFactory(Lookup<PersistenceService>(), base.Session.PoolManager, Lookup<MultithreadingFeature>());
			if (SwapSlutnesAndModeling)
			{
				factory.RatingSwap["summarizing"] = "slutness";
			}
			base.Session.PoolManager.OnGuestLoaded.Add(OnGuestLoaded, base.Scope);
			base.Session.PoolManager.OnGuestClassified.Add(OnGuestClassified, base.Scope);
			base.Session.PreSave.Add(factory.Save);
			overlayService = base.Scope.Lookup<OverlayService>();
			CreateInterface();
			if (factory.Groups != null)
			{
				InitializeInterface();
			}
			else
			{
				factory.PoolsCreated += InitializeInterface;
			}
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.GuestScoreQuery>(consoleCommands.ScoreQuery, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.PoolCrossClassScore>(consoleCommands.DistributionQuery, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.PoolDumpCsv>(consoleCommands.PoolDumpCmd, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.NewProfileCmd>(consoleCommands.NewProfile, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.SelectProfileCmd>(consoleCommands.SelectProfile, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.ResetProfileCmd>(consoleCommands.ResetProfile, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<ConsoleCommands.RestartAG>(consoleCommands.RestartAgCmd, base.Scope);
			base.Session.OnGuestReady += delegate(GuestCharacter guest)
			{
				guest.SynchronizeCharacterWithInstance();
			};
			SMAGlobalPatches.BeforeAutorating.Add(delegate(SMAGlobalPatches.AutoratingInterception interceptor)
			{
				interceptor.Suppress = DisableAutorating;
			}, base.Scope);
			base.Session.Scope.Provide(this, base.Scope);
		}
	}

	private void CreateInterface()
	{
		Drawable gui = CreateGui();
		if (ShowOverlay)
		{
			overlayService.TopRightPane.Add(gui, base.Scope);
		}
	}

	private void InitializeInterface()
	{
		PoolSettingsWindow settings = new PoolSettingsWindow(factory, base.Session);
		Lookup<OverlayService>().AddToFitQueue(settings.Drawable, base.Scope);
		Lookup<PluginOptionsService>().AddOptions("Alt-ve Genetics", settings.Drawable.NativeComponent, base.Scope, settings.RefreshAll);
		if (ShowOverlay)
		{
			overlayService.AddDrawable(settings.ViewerAnchor, base.Scope);
		}
		overlay.ReloadTable();
		factory.PostCompact += Factory_PostCompact;
		factory.OnNewEpochStart += Factory_OnNewEpochStart;
		ProcessMigrations();
		base.Session.PreSave.Add(overlay.Save);
	}

	private void Factory_OnNewEpochStart(GenePool obj)
	{
		overlayService.InfoMessage($"Pool {obj.Data.Settings.Name} upgraded. Current epoch {obj.Data.Epoch}");
	}

	private void ProcessMigrations()
	{
		if (factory.Migrations.Count <= 0)
		{
			return;
		}
		foreach (KeyValuePair<GenePool, (int, int)> kv in factory.Migrations)
		{
			var (success, failure) = kv.Value;
			if (failure > 0)
			{
				overlayService.InfoMessage($"Pool {kv.Key.Data.Settings.Name} migrated to new settings. {success} sucessful changes, {failure} errors. See log for error details");
			}
			else
			{
				overlayService.InfoMessage($"Pool {kv.Key.Data.Settings.Name} migrated to new settings. {success} sucessful changes, 0 errors");
			}
		}
	}

	private void Factory_PostCompact(GenePool obj)
	{
		StringBuilder b = new StringBuilder().Append("Pool ").Append(obj.Data.Settings.Name).Append(" upgraded: ");
		b.Append("Old Gen ").Append(obj.GetGeneration(GeneGeneration.Old).Count).Append("/")
			.Append(obj.Capacity)
			.Append(" ");
		b.Append(" with new threshold ").Append($"{obj.Data.DiversitySimilarityThreshold:0.000}");
		overlayService.InfoMessage(b.ToString());
	}

	private void SendGreetings()
	{
		base.Scope.Lookup<OverlayService>().Notify(new TemporaryNotification
		{
			Drawable = new DockingContainer(Vector2Int.zero, new DrawableLabel("[Alternative genetics Enabled]")
			{
				Position = new Vector2(0f, -50f),
				EnableNative = true
			}),
			FadeIn = 1.5f,
			Duration = 4f,
			FadeOut = 1.5f
		});
	}

	private Drawable CreateGui()
	{
		VLayout<Drawable> root = new VLayout<Drawable>();
		root.Position = Vector2.up * 10f;
		root.Add(new DrawableLabel("Alternative Genetics"));
		guiGrid = root.Add(new GridLayout());
		overlay = new GeneticsOverlay(guiGrid, this);
		guiGrid.Label("Not Initialized Yet");
		return root;
	}

	private void OnGuestLoaded(GuestInstance guest)
	{
		if (factory.GeneFactory == null)
		{
			factory.GeneFactory = guest.Pool.GeneFactory;
		}
		if (guest.Level <= MaxModifiedLevel)
		{
			Dictionary<PoolingGroup, PoolingGroup.GeneSetGroup> results = factory.Apply(guest);
			overlay.UpdateGuiInfo();
			OnGuestModified.Invoke(guest);
			string[] lines = results.Select((KeyValuePair<PoolingGroup, PoolingGroup.GeneSetGroup> kv) => $"{kv.Key.Data.Settings.Name}: {kv.Value.Step}/{kv.Value.MaxSteps}").ToArray();
			overlayService.InfoMessage("Generation groups statistics: " + string.Join(" ", lines));
		}
		else
		{
			overlayService.InfoMessage("No alternative genetics due to level contraints");
		}
	}

	private void OnGuestClassified(GuestPool pool, GuestInstance guest)
	{
		factory.UpdateStatistics(guest);
		overlay.UpdateGuiInfo();
	}
}
