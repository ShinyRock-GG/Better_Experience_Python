using System.Collections.Generic;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using PixelCrushers.DialogueSystem;

namespace BetterExperience.Features.Lexicon;

internal class ConversationProcessor : ITextProcessor
{
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

		public DialogEntryMapping()
		{
		}

		public DialogEntryMapping(DialogueEntry e)
		{
			Actor = e.ActorID;
			Condition = e.conditionsString;
			ConditionPriority = e.conditionPriority;
			Script = e.userScript;
			Text = e.DialogueText;
			MenuText = e.MenuText;
			foreach (Link link in e.outgoingLinks)
			{
				Links[link.destinationDialogueID + "@" + link.destinationConversationID] = (int)link.priority;
			}
		}
	}

	private PersistenceService persistence;

	private Dictionary<int, ConversationMapping> mapping;

	public ConversationProcessor(PersistenceService persistence)
	{
		this.persistence = persistence;
	}

	public void Process()
	{
		bool persist = false;
		if (mapping == null)
		{
			persist = true;
			mapping = persistence.Persisted(() => new Dictionary<int, ConversationMapping>(), "lexicon/conversations", exchange: true);
		}
		foreach (Conversation c in DialogueManager.DatabaseManager.MasterDatabase.conversations)
		{
			if (c.id <= 10)
			{
				continue;
			}
			if (!mapping.TryGetValue(c.id, out var conv))
			{
				ConversationMapping conversationMapping = (mapping[c.id] = new ConversationMapping());
				conv = conversationMapping;
			}
			foreach (DialogueEntry e in c.dialogueEntries)
			{
				DialogEntryMapping entry = conv.Entries.GetValueOrAdd(e.id + "@" + c.id, () => new DialogEntryMapping(e));
				e.DefaultDialogueText = entry.Text;
				e.DialogueText = entry.Text;
				e.LocalizedDialogueText = entry.Text;
				e.MenuText = entry.MenuText;
				e.DefaultMenuText = entry.MenuText;
				e.LocalizedMenuText = e.MenuText;
			}
		}
		if (persist)
		{
			persistence.Persist(mapping, "lexicon/conversations", exchange: true);
		}
	}
}
