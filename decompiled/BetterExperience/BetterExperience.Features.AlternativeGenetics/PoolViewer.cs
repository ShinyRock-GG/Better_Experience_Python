using System;
using System.Collections.Generic;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Pools;
using BetterExperience.Wrappers.Windows;

namespace BetterExperience.Features.AlternativeGenetics;

internal class PoolViewer
{
	private GenePool pool;

	private GameSession session;

	private GeneSet backupGS;

	public Dictionary<GeneSet, Action<bool>> activeToggle = new Dictionary<GeneSet, Action<bool>>();

	public DrawableWindow Window { get; private set; }

	public PoolViewer(GenePool pool, GameSession session)
	{
		this.pool = pool;
		this.session = session;
		GuestInstance gi = session.Guest.GuestInstance;
		backupGS = pool.ExtractGeneSet(gi.ExtractAll().Values, norate: true);
		CreateGUI();
	}

	private void CreateGUI()
	{
		DrawableWindow wnd = new DrawableWindow(300, 600);
		DrawableScrollView scroll = wnd.ScrollPane();
		VLayout<Drawable> layout = scroll.VLayout();
		layout.Button("Close").OnClick += OnClose;
		layout.Label("Pool viewer: " + pool.Data.Settings.Name);
		layout.Button("Reset").OnClick += delegate
		{
			ApplyGS(backupGS);
		};
		GridLayout grid = layout.Grid();
		int i = 0;
		foreach (GeneSet gs in pool.GetGeneration(GeneGeneration.Old))
		{
			using (grid.Row())
			{
				string text = "geneset-" + i++ + "[" + gs.GenAttempts + "]";
				grid.Button("load").OnClick += delegate
				{
					ApplyGS(gs);
				};
				DrawableLabel label = grid.Label(text);
				grid.Button("delete").OnClick += delegate
				{
					DeleteGS(gs);
				};
				activeToggle[gs] = delegate(bool active)
				{
					if (active)
					{
						label.Text = ">>" + text + "<<";
					}
					else
					{
						label.Text = text;
					}
				};
			}
		}
		Window = wnd;
	}

	private void OnClose()
	{
		Restore();
		backupGS = null;
		Window.Visible = false;
	}

	private void DeleteGS(GeneSet gs)
	{
		MayBeResult<bool> promise = session.Modal.MessageBoxYesNo("Are you sure you want to completely remove gene set from pool?");
		promise.OnResult += delegate(bool result)
		{
			if (result)
			{
				pool.GetGeneration(GeneGeneration.Old).Remove(gs);
				session.Modal.MessageError("Done");
			}
		};
	}

	public void Restore()
	{
		if (backupGS != null)
		{
			ApplyGS(backupGS);
		}
	}

	private void ApplyGS(GeneSet gs)
	{
		if (session.Guest == null || session.Guest.GuestInstance == null)
		{
			return;
		}
		List<GeneInfo> update = new List<GeneInfo>();
		for (int i = 0; i < gs.Vector.Length; i++)
		{
			float value = gs.Vector[i];
			if (!float.IsNaN(value))
			{
				GeneInfo gi = new GeneInfo
				{
					Id = new GeneId(pool.Data.GeneOrder[i]),
					Value = value
				};
				update.Add(gi);
			}
		}
		session.Guest.GuestInstance.UpdateAll(update);
		session.Guest.SynchronizeCharacterWithInstance();
		foreach (KeyValuePair<GeneSet, Action<bool>> kv in activeToggle)
		{
			kv.Value(kv.Key == gs);
		}
	}
}
