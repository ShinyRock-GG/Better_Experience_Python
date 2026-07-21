using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;
using UnityEngine;

namespace BetterExperience.Features.GeneTool;

internal class GeneTable
{
	private class GeneRow
	{
		internal string id;

		internal Drawable row;

		internal DrawableButton value;

		internal DrawableButton zero;

		internal DrawableButton half;

		internal DrawableButton one;

		internal DrawableButton watchToggle;

		internal bool watched;

		internal float lastValue;

		internal string group;

		internal DrawableButton subtractStep;

		internal DrawableButton addStep;
	}

	private DrawableTextBox filter;

	private string actualFilter;

	private Dictionary<string, GeneRow> rows = new Dictionary<string, GeneRow>();

	private GridLayout grid;

	private float step = 0.1f;

	private DrawableButton stepbtn;

	private DrawableButton stepDownAll;

	private DrawableButton stepUpAll;

	public GameSession Session { get; internal set; }

	public Drawable Root { get; }

	public GeneWatchFeature GeneWatch { get; set; }

	public GeneFactoryInfo GeneFactory { get; internal set; }

	public Dictionary<string, string> Substitutons { get; } = new Dictionary<string, string>();

	public event Action<string> EditGene = delegate
	{
	};

	public event Action<string, float> SetGene = delegate
	{
	};

	public event Action<(string, float)[]> UpdateGenes = delegate
	{
	};

	public GeneTable()
	{
		VLayout<Drawable> root = new VLayout<Drawable>();
		CreateFilter(root);
		CreateControlRow(root);
		int w = Math.Max(800, Screen.width - 480) - 20;
		int h = Math.Max(600, Screen.height - 120) - 100;
		DrawableScrollView scroll = root.ScrollPane();
		scroll.PreferredSize = new Vector2(w, h);
		grid = scroll.Grid();
		Root = root;
	}

	private void CreateFilter(VLayout<Drawable> root)
	{
		HLayout<Drawable> firstrow = root.HLayout();
		firstrow.Label("Filter:");
		filter = firstrow.TextBox();
		filter.PreferredSize = new Vector2(300f, 20f);
		filter.OnTextChange += OnFilterChanged;
		firstrow.Label("Type <gene name> or .<gene group name> or !w for watched");
	}

	private void CreateControlRow(VLayout<Drawable> root)
	{
		HLayout<Drawable> snndrow = root.HLayout();
		snndrow.Label("Batch controls:  ");
		snndrow.Label("step: ");
		stepbtn = snndrow.Button(step.ToString());
		snndrow.Label("   ");
		stepDownAll = snndrow.Button("--");
		stepUpAll = snndrow.Button("++");
		stepDownAll.OnClick += delegate
		{
			Step(null, -1);
		};
		stepUpAll.OnClick += delegate
		{
			Step(null, 1);
		};
		stepbtn.OnClick += delegate
		{
			Session.Modal.RequestInput("Input new step", step.ToString()).OnResult += delegate(string result)
			{
				if (result != null)
				{
					try
					{
						step = float.Parse(result);
						stepbtn.Text = step.ToString();
					}
					catch (Exception ex)
					{
						new Logger().Error(ex, "failed to parse");
					}
				}
			};
		};
	}

	private void Step(string fixedGene, int dir)
	{
		List<GeneRow> targets = new List<GeneRow>();
		if (fixedGene != null)
		{
			if (rows.TryGetValue(fixedGene, out var row))
			{
				targets.Add(row);
			}
		}
		else
		{
			foreach (GeneRow geneRow in rows.Values)
			{
				if (!geneRow.row.Transient)
				{
					targets.Add(geneRow);
				}
			}
		}
		List<(string, float)> updates = new List<(string, float)>();
		foreach (GeneRow target in targets)
		{
			float value = target.lastValue + step * (float)dir;
			value = Mathf.Clamp01(value);
			if (value != target.lastValue)
			{
				updates.Add((target.id, value));
			}
		}
		if (updates.Count > 0)
		{
			this.UpdateGenes(updates.ToArray());
		}
	}

	private void OnFilterChanged()
	{
		string text = filter.Text;
		if (text.Length < 2)
		{
			text = null;
		}
		if (actualFilter != text)
		{
			actualFilter = text;
			RunFilter((actualFilter != null) ? actualFilter.ToLower() : null);
		}
	}

	private void RunFilter(string filter)
	{
		bool watched = "!w" == filter;
		string group = ((filter != null && filter.StartsWith(".")) ? filter.Substring(1) : null);
		foreach (KeyValuePair<string, GeneRow> kv in rows)
		{
			if (watched)
			{
				kv.Value.row.Transient = !kv.Value.watched;
			}
			else if (group != null)
			{
				kv.Value.row.Transient = kv.Value.group != null && !kv.Value.group.ToLower().Contains(group);
			}
			else
			{
				kv.Value.row.Transient = filter != null && !kv.Key.ToLower().Contains(filter);
			}
		}
	}

	internal void SetGenes(Dictionary<GeneId, GeneInfoEx>.ValueCollection values)
	{
		IOrderedEnumerable<GeneInfoEx> list = from x in values.ToList()
			orderby x.Id.ToString()
			select x;
		foreach (GeneInfoEx k in list)
		{
			string gene = k.Id.ToString();
			GeneRow row = rows.GetValueOrAdd(gene, () => CreateGeneRow(gene));
			row.value.Text = k.Value.ToString("F4");
			row.lastValue = k.Value;
			if (row.watched)
			{
				GeneWatch.SetWatchState(gene, k.Value);
			}
		}
	}

	private GeneRow CreateGeneRow(string gene)
	{
		GeneRow row = new GeneRow();
		row.id = gene;
		using (grid.Row())
		{
			if (GeneFactory != null)
			{
				string gid = new GeneId(gene).Item1;
				if (GeneFactory.GeneToGroup.TryGetValue(gid, out var group))
				{
					row.group = group;
				}
			}
			string geneDisplayName = gene;
			foreach (KeyValuePair<string, string> kv in Substitutons)
			{
				geneDisplayName = geneDisplayName.Replace(kv.Key, kv.Value);
			}
			grid.Label(geneDisplayName);
			row.value = grid.Button("?");
			grid.Label("     ");
			row.zero = grid.Button(">0<");
			row.half = grid.Button(">0.5<");
			row.one = grid.Button(">1<");
			grid.Label("     ");
			row.watchToggle = grid.Button("?");
			grid.Label("     ");
			row.subtractStep = grid.Button("--");
			row.addStep = grid.Button("++");
			row.row = grid.Children[grid.Children.Count - 1];
			row.value.OnClick += delegate
			{
				this.EditGene(gene);
			};
			row.zero.OnClick += delegate
			{
				this.SetGene(gene, 0f);
			};
			row.half.OnClick += delegate
			{
				this.SetGene(gene, 0.5f);
			};
			row.one.OnClick += delegate
			{
				this.SetGene(gene, 1f);
			};
			row.addStep.OnClick += delegate
			{
				Step(gene, 1);
			};
			row.subtractStep.OnClick += delegate
			{
				Step(gene, -1);
			};
			row.watched = GeneWatch.IsWatched(gene);
			Action updateWatchButtonText = delegate
			{
				row.watchToggle.Text = (row.watched ? "[Unwatch]" : "[Watch]");
			};
			updateWatchButtonText();
			row.watchToggle.OnClick += delegate
			{
				row.watched = !row.watched;
				updateWatchButtonText();
				if (row.watched)
				{
					GeneWatch.SetWatchState(gene, row.lastValue);
				}
				else
				{
					GeneWatch.SetWatchState(gene, float.NaN);
				}
			};
			row.row.OffscreenHint = true;
		}
		return row;
	}
}
