using System;
using System.Collections.Generic;
using BetterExperience.PyStory.AI;

namespace BetterExperience.PyStory;

public class BehaviorNode
{
	private Logger logger = Logger.Create<BehaviorNode>();

	private List<BehaviorNode> _nodes = new List<BehaviorNode>();

	public Func<bool> Condition { get; }

	public IReadOnlyList<StimuliReactor> Reactors { get; }

	public IReadOnlyList<BehaviorNode> Behaviors => _nodes;

	public BehaviorNode(Func<bool> condition)
	{
		Condition = condition;
	}

	public BehaviorNode(Func<bool> condition, IList<StimuliReactor> reactors)
	{
		Condition = condition;
		Reactors = new List<StimuliReactor>(reactors);
	}

	public bool React(SimpleAi.SimpleStimulus stimuli)
	{
		if (Condition == null || Condition())
		{
			if (Behaviors != null && Behaviors.Count > 0)
			{
				foreach (BehaviorNode b in Behaviors)
				{
					if (b.React(stimuli))
					{
						return true;
					}
				}
			}
			if (Reactors != null && Reactors.Count > 0)
			{
				foreach (StimuliReactor r in Reactors)
				{
					if (React(r, stimuli))
					{
						return true;
					}
				}
			}
			return false;
		}
		return false;
	}

	private bool React(StimuliReactor matcher, SimpleAi.SimpleStimulus stimuli)
	{
		if (matcher.Receiver != null && !matcher.Receiver.Contains(stimuli.Key.Receiver))
		{
			return false;
		}
		if (matcher.Sender != null && !matcher.Sender.Contains(stimuli.Key.Sender))
		{
			return false;
		}
		if (matcher.Stimulus != null && !matcher.Stimulus.Contains(stimuli.Key.Stimulus))
		{
			return false;
		}
		try
		{
			if (matcher.Reactor(stimuli))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "OOPS! reactor thrown error");
			return true;
		}
		return false;
	}

	public void Add(BehaviorNode bn)
	{
		if (_nodes == null)
		{
			_nodes = new List<BehaviorNode>();
		}
		if (!_nodes.Contains(bn))
		{
			_nodes.Add(bn);
		}
	}
}
