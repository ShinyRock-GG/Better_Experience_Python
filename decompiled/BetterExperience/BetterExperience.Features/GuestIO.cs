using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using BetterExperience.Features.Console;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Pools;

namespace BetterExperience.Features;

internal class GuestIO : PluginFeature
{
	private class GIOService : SessionService
	{
		private class GIODump : Dictionary<string, Dictionary<string, float>>
		{
		}

		private bool dumpOnArrive;

		private PersistenceService persistence;

		public GIOService(bool dumpOnArrive)
		{
			this.dumpOnArrive = dumpOnArrive;
		}

		public override void OnInit()
		{
			base.OnInit();
			ConsoleService console = Lookup<ConsoleService>();
			console.RegisterCommand(CommandExport, base.Scope);
			console.RegisterCommand(CommandImport, base.Scope);
			console.RegisterCommand(CommandLocation, base.Scope);
			console.RegisterCommand(CommandBackup, base.Scope);
			console.RegisterCommand(CommandReset, base.Scope);
			console.RegisterCommand(CommandEve, base.Scope);
			console.RegisterCommand(CommandRandomize, base.Scope);
			persistence = Lookup<PersistenceService>();
		}

		public override void OnStart()
		{
			if (dumpOnArrive)
			{
				base.Session.OnGuestReady += delegate
				{
					CommandBackup();
				};
			}
		}

		[ConsoleCommand("Reset current guest to standard guest prefab", new string[] { "gio", "eve" })]
		private string CommandEve()
		{
			if (base.Session.Guest == null || base.Session.Guest.GuestInstance == null)
			{
				return "no guest";
			}
			base.Session.Guest.GuestInstance.Reset();
			base.Session.Guest.SynchronizeCharacterWithInstance();
			return "ok";
		}

		[ConsoleCommand("Randomize current guest", new string[] { "gio", "rand" })]
		private string CommandRandomize()
		{
			if (base.Session.Guest == null || base.Session.Guest.GuestInstance == null)
			{
				return "no guest";
			}
			base.Session.Guest.GuestInstance.Randmize();
			base.Session.Guest.SynchronizeCharacterWithInstance();
			return "ok";
		}

		[ConsoleCommand("Backup current guest variables", new string[] { "gio", "backup" })]
		private string CommandBackup()
		{
			return GenericExport(GetBackupFiles());
		}

		[ConsoleCommand("Reset current guest variables from last stored backup", new string[] { "gio", "reset" })]
		private string CommandReset()
		{
			return GenericImport(GetBackupFiles());
		}

		private string[] GetBackupFiles()
		{
			return new string[2] { "guest_last_appearance", "guest_last_personality" };
		}

		private string[] GetManualFiles()
		{
			return new string[2] { "guest_io_appearance", "guest_io_personality" };
		}

		[ConsoleCommand("Print current import/export file paths", new string[] { "gio", "files" })]
		private string CommandLocation()
		{
			StringBuilder output = new StringBuilder();
			output.AppendLine("Backup:");
			string[] files = GetBackupFiles();
			output.Append("   Appearance: ").AppendLine(files[0]);
			output.Append("   Personality: ").AppendLine(files[1]);
			output.AppendLine("Manual:");
			files = GetManualFiles();
			output.Append("   Appearance: ").AppendLine(files[0]);
			output.Append("   Personality: ").AppendLine(files[1]);
			return output.ToString();
		}

		[ConsoleCommand("Modify current guest's apperance and behaviour with json files", new string[] { "gio", "import" })]
		private string CommandImport()
		{
			return GenericImport(GetManualFiles());
		}

		[ConsoleCommand("Save current guest's apperance and behaviour into json files", new string[] { "gio", "export" })]
		private string CommandExport()
		{
			return GenericExport(GetManualFiles());
		}

		private string GenericImport(string[] files)
		{
			if (base.Session.Guest == null)
			{
				return "Import failed: no one to export";
			}
			if (base.Session.Guest.GuestInstance == null)
			{
				return "Import failed: no readable data available";
			}
			StringBuilder reply = new StringBuilder();
			GIODump appearance = ReadObject(reply, files[0]);
			GIODump personality = ReadObject(reply, files[1]);
			GuestInstance gi = base.Session.Guest.GuestInstance;
			List<GeneInfo> update = new List<GeneInfo>();
			if (appearance == null)
			{
				reply.AppendLine("Missing appearance data");
			}
			else
			{
				foreach (Dictionary<string, float> gs in appearance.Values)
				{
					foreach (KeyValuePair<string, float> kv in gs)
					{
						update.Add(new GeneInfo
						{
							Id = new GeneId(kv.Key),
							Value = kv.Value
						});
					}
				}
			}
			if (personality == null)
			{
				reply.AppendLine("Missing personality data");
			}
			else
			{
				foreach (Dictionary<string, float> gs2 in personality.Values)
				{
					foreach (KeyValuePair<string, float> kv2 in gs2)
					{
						update.Add(new GeneInfo
						{
							Id = new GeneId(kv2.Key),
							Value = kv2.Value
						});
					}
				}
			}
			Dictionary<GeneId, GeneInfoEx> actual = base.Session.Guest.GuestInstance.ExtractAll();
			update.RemoveIf((GeneInfo gene) => actual.TryGetValue(gene.Id, out var value) && value.Value == gene.Value);
			if (update.Count == 0)
			{
				reply.AppendLine("Nothing written");
			}
			else
			{
				gi.UpdateAll(update);
				reply.Append("Update complete. Updated ").Append(update.Count).AppendLine(" values");
				base.Session.Guest.SynchronizeCharacterWithInstance();
				reply.AppendFormat("Character synchronized");
			}
			return reply.ToString();
		}

		private string GenericExport(string[] files)
		{
			if (base.Session.Guest == null)
			{
				return "Export failed: no one to export";
			}
			if (base.Session.Guest.GuestInstance == null)
			{
				return "Export failed: no readable data available";
			}
			StringBuilder reply = new StringBuilder();
			GuestInstance gi = base.Session.Guest.GuestInstance;
			GIODump appearance = Dump(gi.ExtractAppearance());
			GIODump personality = Dump(gi.ExtractPersonality());
			WriteObject(reply, appearance, files[0]);
			WriteObject(reply, personality, files[1]);
			return reply.ToString();
		}

		private void WriteObject(StringBuilder reply, GIODump appearance, string fname)
		{
			persistence.Persist(appearance, fname, exchange: true);
			reply?.Append("Wrote file '").Append(fname).AppendLine("'");
		}

		private GIODump ReadObject(StringBuilder reply, string fname)
		{
			GIODump result = persistence.Persisted(() => (GIODump)null, fname, exchange: true);
			if (result == null)
			{
				reply.Append("File not found: ").AppendLine(fname);
				return null;
			}
			reply.Append("Read file: ").AppendLine(fname);
			return result;
		}

		private GIODump Dump(Dictionary<GeneId, GeneInfoEx> dictionary)
		{
			GIODump result = new GIODump();
			foreach (GeneInfoEx gene in dictionary.Values)
			{
				result.GetValueOrAdd(gene.Group, () => new Dictionary<string, float>()).Add(gene.Id.ToString(), gene.Value);
			}
			return result;
		}
	}

	private ConfigEntry<bool> enableGIO;

	private ConfigEntry<bool> enableDumpOnArrive;

	public override bool Enabled => enableGIO.Value;

	public override void Configure(ConfigFile config)
	{
		enableGIO = config.Bind<bool>("Features", "EnableGuestIO", true, "Enable guest IO");
		enableDumpOnArrive = config.Bind<bool>("GuestIO", "DumpOnArrive", true, "Dump guest variables on start of interview");
	}

	public override void OnInit()
	{
		Lookup<PluginOptionsService>().Expose(enableGIO, base.Scope);
		Lookup<PluginOptionsService>().Expose(enableDumpOnArrive, base.Scope);
	}

	public override void OnStart()
	{
		Lookup<SessionTracker>().SessionServices.Add(() => new GIOService(enableDumpOnArrive.Value));
	}
}
