using System;
using System.Collections.Generic;
using BetterExperience.PyStory.AI;

namespace BetterExperience.PyStory;

public class StimuliReactor
{
	public IList<StimulusType> Stimulus { get; }

	public IList<HumanBodyPartsEng> Receiver { get; }

	public IList<SenderBodyPartEng> Sender { get; }

	public Func<SimpleAi.SimpleStimulus, bool> Reactor { get; }

	public StimuliReactor(IList<StimulusType> stimulus, IList<HumanBodyPartsEng> receiver, IList<SenderBodyPartEng> sender, Func<SimpleAi.SimpleStimulus, bool> reactor)
	{
		Stimulus = stimulus;
		Receiver = receiver;
		Sender = sender;
		Reactor = reactor;
	}
}
