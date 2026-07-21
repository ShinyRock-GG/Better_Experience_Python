using System;
using BetterExperience.GameScopes;

namespace BetterExperience.Features.Lexicon;

internal class LexiconProcessor : IDisposable
{
	private ITextProcessor[] processors;

	public LexiconProcessor(PersistenceService service, bool withConv)
	{
		if (withConv)
		{
			processors = new ITextProcessor[4]
			{
				new BodyPartsProcessor(service),
				new WordsProcessor(service),
				new ExpressionsProcessor(service),
				new ConversationProcessor(service)
			};
		}
		else
		{
			processors = new ITextProcessor[3]
			{
				new BodyPartsProcessor(service),
				new WordsProcessor(service),
				new ExpressionsProcessor(service)
			};
		}
	}

	public void Process()
	{
		ITextProcessor[] array = processors;
		foreach (ITextProcessor p in array)
		{
			p.Process();
		}
	}

	public void Dispose()
	{
	}
}
