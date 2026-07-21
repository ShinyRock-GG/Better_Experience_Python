using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BetterExperience.Features.Console;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Pools;

namespace BetterExperience.Features.AlternativeGenetics;

internal class ConsoleCommands
{
	[ConsoleCommand("Restart alternative genetics", new string[] { "ag", "restart" })]
	public class RestartAG
	{
	}

	[ConsoleCommand("Create new genetic profile", new string[] { "ag", "np" })]
	public class NewProfileCmd
	{
		[ConsoleCommandArg(Key = "from", Name = "source profile")]
		public string SourceProfile { get; set; }

		[ConsoleCommandArg(Key = "of", Name = "group id")]
		public string Group { get; set; }

		[ConsoleCommandArg(Key = "profile_id", Mode = ConsoleArgMode.Tail, Name = "new profile id")]
		public string ProfileId { get; set; }
	}

	[ConsoleCommand("Select genetic profile", new string[] { "ag", "sp" })]
	public class SelectProfileCmd
	{
		[ConsoleCommandArg(Key = "of", Name = "group id")]
		public string Group { get; set; }

		[ConsoleCommandArg(Key = "profile_id", Mode = ConsoleArgMode.Tail, Name = "profile id")]
		public string ProfileId { get; set; }
	}

	[ConsoleCommand("Reset genetic profile", new string[] { "ag", "rp" })]
	public class ResetProfileCmd
	{
		[ConsoleCommandArg(Key = "of", Name = "group id")]
		public string Group { get; set; }
	}

	[ConsoleCommand("Dump current old generations as CSV", new string[] { "ag", "dump" })]
	public class PoolDumpCsv
	{
		[ConsoleCommandArg(Key = "pool", Mode = ConsoleArgMode.Tail, Name = "Pool to compute")]
		public string Pool { get; set; }
	}

	[ConsoleCommand("Score current guest agains pool and export as CSV", new string[] { "ag", "score" })]
	public class GuestScoreQuery
	{
		[ConsoleCommandArg(Key = "pool", Mode = ConsoleArgMode.Tail, Name = "Pool to score against")]
		public string Pool { get; set; }
	}

	[ConsoleCommand("Compute distance distribution between classes and export as CSV", new string[] { "ag", "dist" })]
	public class PoolCrossClassScore
	{
		[ConsoleCommandArg(Key = "pool", Mode = ConsoleArgMode.Tail, Name = "Pool to compute")]
		public string Pool { get; set; }

		[ConsoleCommandArg(Key = "pc", Mode = ConsoleArgMode.KeyValue, Name = "use perceptive mode")]
		public float Perceptive { get; set; }
	}

	private class SimpleCsv
	{
		private int cols;

		private List<string[]> rows = new List<string[]>();

		public string this[int row, int col]
		{
			get
			{
				for (int i = rows.Count; i <= row; i++)
				{
					rows.Add(new string[cols]);
				}
				return rows[row][col];
			}
			set
			{
				for (int i = rows.Count; i <= row; i++)
				{
					rows.Add(new string[cols]);
				}
				rows[row][col] = value;
			}
		}

		public SimpleCsv(int cols)
		{
			this.cols = cols;
		}

		internal void Write(string fname)
		{
			List<string> strrows = new List<string>();
			foreach (string[] r in rows)
			{
				strrows.Add(string.Join(";", r));
			}
			File.WriteAllLines(fname, strrows);
		}
	}

	private AlternativeGeneticsService alternativeGenetics;

	private PersistenceService persistence;

	public ConsoleCommands(AlternativeGeneticsService alternativeGenetics)
	{
		this.alternativeGenetics = alternativeGenetics;
		persistence = alternativeGenetics.Scope.Lookup<PersistenceService>();
	}

	public string RestartAgCmd(RestartAG query)
	{
		if (alternativeGenetics.Session.Guest != null)
		{
			return "Not available during interview";
		}
		alternativeGenetics.Factory.Save();
		ScopeSupport parentScope = alternativeGenetics.Scope.Parent;
		alternativeGenetics.Scope.Dispose();
		parentScope.AddService(new AlternativeGeneticsService());
		return "ok";
	}

	public string NewProfile(NewProfileCmd cmd)
	{
		StringBuilder reply = new StringBuilder();
		ExecCreateProfile(reply, cmd);
		return reply.ToString();
	}

	private void ExecCreateProfile(StringBuilder reply, NewProfileCmd cmd)
	{
		if (cmd.ProfileId == null)
		{
			reply.AppendLine("Missing mandatory profile name");
			return;
		}
		if (cmd.Group == null)
		{
			reply.AppendLine("Missing mandatory group id");
			return;
		}
		if (!alternativeGenetics.Factory.Groups.TryGetValue(cmd.Group, out var _))
		{
			reply.Append("No group found: ").Append(cmd.Group).AppendLine();
			return;
		}
		if (alternativeGenetics.Factory.LoadGroupProfile(cmd.Group, cmd.ProfileId) != null)
		{
			reply.Append("Profile already exists: ").AppendLine(cmd.ProfileId);
			return;
		}
		PoolingGroupData template = null;
		if (cmd.SourceProfile != null)
		{
			template = alternativeGenetics.Factory.LoadGroupProfile(cmd.Group, cmd.SourceProfile);
			if (template == null)
			{
				reply.Append("No source profile found: ").AppendLine(cmd.SourceProfile);
				return;
			}
		}
		if (template == null)
		{
			template = alternativeGenetics.Factory.LoadGroupProfile(cmd.Group, "default");
			foreach (KeyValuePair<string, GenePoolData> p in template.Pools)
			{
				foreach (KeyValuePair<GeneGeneration, List<GeneSet>> generation in p.Value.Generations)
				{
					generation.Value.Clear();
				}
				p.Value.Epoch = 0;
				p.Value.Error = 0f;
			}
		}
		template.Settings.Profile = cmd.ProfileId;
		alternativeGenetics.Factory.SaveProfile(cmd.Group, template);
		reply.AppendLine("Profile created");
	}

	public string SelectProfile(SelectProfileCmd cmd)
	{
		StringBuilder reply = new StringBuilder();
		ExecSelectProfile(reply, cmd);
		return reply.ToString();
	}

	private void ExecSelectProfile(StringBuilder reply, SelectProfileCmd cmd)
	{
		if (alternativeGenetics.Session.Guest != null)
		{
			reply.AppendLine("Not available during interview");
			return;
		}
		if (cmd.ProfileId == null)
		{
			reply.AppendLine("Missing mandatory profile name");
			return;
		}
		if (cmd.Group == null)
		{
			reply.AppendLine("Missing mandatory group id");
			return;
		}
		if (!alternativeGenetics.Factory.Groups.TryGetValue(cmd.Group, out var group))
		{
			reply.Append("No group found: ").Append(cmd.Group).AppendLine();
			return;
		}
		if (group.Data.Settings.Profile == cmd.ProfileId)
		{
			reply.AppendLine("Already selected");
			return;
		}
		PoolingGroupData target = alternativeGenetics.Factory.LoadGroupProfile(cmd.Group, cmd.ProfileId);
		if (target == null)
		{
			reply.Append("Profile does not exist: ").AppendLine(cmd.ProfileId);
			return;
		}
		alternativeGenetics.Factory.SetProfile(cmd.Group, cmd.ProfileId);
		alternativeGenetics.Factory.Save();
		ScopeSupport parentScope = alternativeGenetics.Scope.Parent;
		alternativeGenetics.Scope.Dispose();
		parentScope.AddService(new AlternativeGeneticsService());
		reply.AppendLine("Profile set");
	}

	public string ResetProfile(ResetProfileCmd cmd)
	{
		StringBuilder reply = new StringBuilder();
		ExecResetProfile(reply, cmd);
		return reply.ToString();
	}

	private void ExecResetProfile(StringBuilder reply, ResetProfileCmd cmd)
	{
		if (alternativeGenetics.Session.Guest != null)
		{
			reply.AppendLine("Not available during interview");
			return;
		}
		if (!alternativeGenetics.Factory.Groups.TryGetValue(cmd.Group, out var group))
		{
			reply.Append("No group found: ").Append(cmd.Group).AppendLine();
			return;
		}
		foreach (KeyValuePair<string, GenePool> pool in group.Pools)
		{
			pool.Value.Clear();
		}
		reply.AppendLine("Reset");
	}

	public string PoolDumpCmd(PoolDumpCsv query)
	{
		if (!alternativeGenetics.Factory.GetPools().TryGetValue(query.Pool, out var pool))
		{
			return "No such pool " + query.Pool;
		}
		List<GeneSet> og = pool.GetGeneration(GeneGeneration.Old);
		SimpleCsv csv = new SimpleCsv(pool.Data.GeneOrder.Count + 1);
		csv[0, 0] = "index";
		for (int i = 0; i < pool.Data.GeneOrder.Count; i++)
		{
			csv[0, i + 1] = pool.Data.GeneOrder[i];
		}
		for (int j = 0; j < og.Count; j++)
		{
			GeneSet gs = og[j];
			csv[j + 1, 0] = "old_" + j + "_" + (int)(gs.Rating.Average() * 10f);
			for (int k = 0; k < pool.Data.GeneOrder.Count; k++)
			{
				if (gs.Vector.Length > k)
				{
					csv[j + 1, k + 1] = $"{gs.Vector[k]:0.000}";
				}
			}
		}
		GeneSet[] special = pool.GetSpecialDumpVectors();
		for (int l = 0; l < special.Length; l++)
		{
			GeneSet gs2 = special[l];
			csv[l + 1 + og.Count, 0] = "spec_" + gs2.Id;
			for (int m = 0; m < pool.Data.GeneOrder.Count; m++)
			{
				if (gs2.Vector.Length > m)
				{
					csv[l + 1 + og.Count, m + 1] = $"{gs2.Vector[m]:0.000}";
				}
			}
		}
		string fname = Path.Combine(persistence.ExchangeDir, "gene_dump_" + query.Pool + ".csv");
		csv.Write(fname);
		return "Saved to " + fname;
	}

	public string ScoreQuery(GuestScoreQuery query)
	{
		if (alternativeGenetics.Session.Guest == null)
		{
			return "No guest";
		}
		Dictionary<GeneId, GeneInfoEx> allgenes = alternativeGenetics.Session.Guest.GuestInstance.ExtractAll();
		if (!alternativeGenetics.Factory.GetPools().TryGetValue(query.Pool, out var pool))
		{
			return "No such pool " + query.Pool;
		}
		List<GeneSet> og = pool.GetGeneration(GeneGeneration.Old);
		GeneSet gs = pool.ExtractGeneSet(allgenes.Values, norate: true);
		SimpleCsv csv = new SimpleCsv(og.Count + 1);
		csv[0, 0] = "score";
		for (int i = 0; i < og.Count; i++)
		{
			float score = pool.SimilarityDistance(og[i], gs);
			csv[0, 1 + i] = $"{score:0.000}";
		}
		for (int j = 0; j < gs.Vector.Length; j++)
		{
			csv[1 + j, 0] = pool.Data.GeneOrder[j];
			for (int k = 0; k < og.Count; k++)
			{
				csv[1 + j, 1 + k] = $"{Math.Abs(og[k].Vector[j] - gs.Vector[j]):0.00000}";
			}
		}
		string fname = Path.Combine(persistence.ExchangeDir, "gene_delta_" + query.Pool + ".csv");
		csv.Write(fname);
		return "Saved to " + fname;
	}

	public string DistributionQuery(PoolCrossClassScore query)
	{
		if (!alternativeGenetics.Factory.GetPools().TryGetValue(query.Pool, out var pool))
		{
			return "No such pool " + query.Pool;
		}
		List<GeneSet> og = pool.GetGeneration(GeneGeneration.Old);
		PerceptionScoring perceptionScoring = new PerceptionScoring(query.Perceptive, pool);
		SimpleCsv csv = new SimpleCsv(og.Count);
		for (int i = 0; i < og.Count; i++)
		{
			for (int j = 0; j < og.Count; j++)
			{
				float score = ((query.Perceptive > 0f) ? perceptionScoring.Score(og[i], new GeneSet[1] { og[j] }.ToList())[0] : pool.SimilarityDistance(og[i], og[j]));
				csv[i, j] = $"{score:0.000}";
			}
		}
		string fname = Path.Combine(persistence.ExchangeDir, "gene_crossdist_" + query.Pool + ".csv");
		csv.Write(fname);
		return "Saved to " + fname;
	}
}
