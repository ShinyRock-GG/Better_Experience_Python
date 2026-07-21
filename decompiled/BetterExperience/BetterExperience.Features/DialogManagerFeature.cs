using System.Collections.Generic;
using BetterExperience.Features.Console;
using BetterExperience.GameScopes;
using PixelCrushers.DialogueSystem;

namespace BetterExperience.Features;

internal class DialogManagerFeature : PluginFeature
{
	public class DialogManagerService : SessionService
	{
		[ConsoleCommand("Dump dialog database to file", new string[] { "dm", "dump" })]
		public class DMExport
		{
		}

		[ConsoleCommand("Import dialog database to file", new string[] { "dm", "import" })]
		public class DMImport
		{
		}

		public override void OnStart()
		{
			base.OnStart();
			Lookup<ConsoleService>().RegisterCommand<DMExport>(OnExportDialogs, base.Scope);
			Lookup<ConsoleService>().RegisterCommand<DMImport>(OnImportDialogs, base.Scope);
		}

		public string OnImportDialogs(DMImport cmd)
		{
			Dictionary<int, ConversationMapping> data = Lookup<PersistenceService>().Persisted(() => (Dictionary<int, ConversationMapping>)null, "dialogs_dump", exchange: true);
			if (data != null)
			{
				PatchDialogs(data);
				return "done";
			}
			return "file not found";
		}

		private void PatchDialogs(Dictionary<int, ConversationMapping> patch)
		{
			foreach (Conversation c in DialogueManager.DatabaseManager.MasterDatabase.conversations)
			{
				if (!patch.TryGetValue(c.id, out var conv))
				{
					continue;
				}
				foreach (DialogueEntry e in c.dialogueEntries)
				{
					if (conv.Entries.TryGetValue(e.id + "@" + c.id, out var epatch))
					{
						if (epatch.MenuText != null)
						{
							e.MenuText = epatch.MenuText;
							e.LocalizedMenuText = epatch.MenuText;
							e.DefaultMenuText = epatch.MenuText;
						}
						if (epatch.Text != null)
						{
							e.DialogueText = epatch.Text;
							e.LocalizedDialogueText = epatch.Text;
							e.DefaultDialogueText = epatch.Text;
						}
					}
				}
			}
		}

		public string OnExportDialogs(DMExport cmd)
		{
			Dictionary<int, ConversationMapping> mapping = new Dictionary<int, ConversationMapping>();
			foreach (Conversation c in DialogueManager.DatabaseManager.MasterDatabase.conversations)
			{
				ConversationMapping conversationMapping = (mapping[c.id] = new ConversationMapping());
				ConversationMapping conv = conversationMapping;
				foreach (DialogueEntry e in c.dialogueEntries)
				{
					DialogEntryMapping dialogEntryMapping = (conv.Entries[e.id + "@" + c.id] = new DialogEntryMapping());
					DialogEntryMapping entry = dialogEntryMapping;
					entry.Actor = e.ActorID;
					entry.Text = e.DialogueText;
					entry.Condition = e.conditionsString;
					entry.ConditionPriority = e.conditionPriority;
					entry.Script = e.userScript;
					entry.MenuText = e.MenuText;
					foreach (Link link in e.outgoingLinks)
					{
						entry.Links[link.destinationDialogueID + "@" + link.destinationConversationID] = (int)link.priority;
					}
				}
			}
			Lookup<PersistenceService>().Persist(mapping, "dialogs_dump", exchange: true);
			return "done";
		}
	}

	public class ConversationMapping
	{
		public Dictionary<string, DialogEntryMapping> Entries { get; set; } = new Dictionary<string, DialogEntryMapping>();
	}

	public class DialogEntryMapping
	{
		public Dictionary<string, int> Links = new Dictionary<string, int>();

		public int Actor { get; set; }

		public string Text { get; set; }

		public string Condition { get; set; }

		public ConditionPriority ConditionPriority { get; set; }

		public string Script { get; set; }

		public string MenuText { get; set; }
	}

	public override bool Enabled => false;

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().SessionServices.Add(() => new DialogManagerService());
	}
}
