using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.GeneTool;

internal class GeneWatchFeature : PluginService
{
	private class Installer : SessionService
	{
		private GeneWatchFeature service;

		internal Installer(GeneWatchFeature s)
		{
			service = s;
		}

		public override void OnStart()
		{
			base.OnStart();
			VLayout<Drawable> layput = new VLayout<Drawable>();
			layput.Add(new DrawableLabel(""));
			layput.Add(service.layout);
			layput.Add(new DrawableLabel(""));
			Lookup<OverlayService>().TopRightPane.Add(layput, base.Scope);
			PersistenceService persistence = Lookup<PersistenceService>();
			service.Data = persistence.Persisted(() => new GeneWatchData());
			service.Restore();
			base.Session.PreSave.Add(delegate
			{
				if (service.Dirty)
				{
					persistence.Persist(service.Data);
					service.Dirty = false;
				}
			});
		}
	}

	private GridLayout layout;

	private GeneWatchData Data { get; set; }

	public bool Dirty { get; private set; }

	public override void OnStart()
	{
		layout = CreateGUI();
		layout.Transient = true;
		Lookup<SessionTracker>().SessionServices.Add(() => new Installer(this));
	}

	public bool IsWatched(string gene)
	{
		return Data.Watched.Contains(gene);
	}

	public void Restore()
	{
		foreach (string geneId in Data.Watched)
		{
			layout.Label(geneId);
			layout.Label("?");
			layout.NewLine();
		}
	}

	private GridLayout CreateGUI()
	{
		GridLayout root = new GridLayout();
		root.Label("Gene Watch");
		root.Label("");
		root.NewLine();
		return root;
	}

	internal void SetWatchState(string geneId, float value)
	{
		int startCount = Data.Watched.Count;
		if (float.IsNaN(value))
		{
			int idx = Data.Watched.IndexOf(geneId);
			if (idx >= 0)
			{
				Data.Watched.RemoveAt(idx);
				if (layout.Children.Count > 1 + idx)
				{
					HLayout<Drawable> row = layout.Children[1 + idx];
					layout.Remove(row);
				}
			}
		}
		else
		{
			string txtValue = $"{value:0.000}";
			if (!Data.Watched.Contains(geneId))
			{
				Data.Watched.Add(geneId);
				layout.Label(geneId);
				layout.Label(txtValue);
				layout.NewLine();
			}
			else
			{
				int idx2 = Data.Watched.IndexOf(geneId);
				if (layout.Children.Count > 1 + idx2)
				{
					HLayout<Drawable> row2 = layout.Children[1 + idx2];
					if (row2.Children[1] is DrawableLabel lbl)
					{
						lbl.Text = txtValue;
					}
				}
			}
		}
		Dirty |= Data.Watched.Count != startCount;
		layout.Transient = Data.Watched.Count == 0;
	}
}
