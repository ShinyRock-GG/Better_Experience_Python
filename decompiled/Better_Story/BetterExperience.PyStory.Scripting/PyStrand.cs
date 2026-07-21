using System;
using System.Collections;
using System.Collections.Generic;
using BetterExperience.GameScopes;

namespace BetterExperience.PyStory.Scripting;

public class PyStrand
{
	private static int STRAND_ID_COUNTER = 1;

	public int Id { get; }

	public ScopeSupport Scope { get; }

	public string Name { get; set; } = null;

	public Stack<PyStrandFrame> Frames { get; } = new Stack<PyStrandFrame>();

	public bool HasDialogueSequence { get; internal set; }

	public bool IsFailed { get; set; }

	public float FailedAt { get; set; }

	public bool IsAlive => Scope.Started && !IsFailed;

	public List<PyStrandFrame> Queue { get; } = new List<PyStrandFrame>();

	public PyStrand(ScopeSupport scope)
	{
		Scope = scope;
		Id = STRAND_ID_COUNTER++;
	}

	public void SubmitLast(IEnumerator gen)
	{
		if (Frames.Count == 0)
		{
			throw new ArgumentException("Cannot submit generators to inactive strand");
		}
		Queue.Add(new PyStrandFrame(gen, this));
	}

	public void SubmitFirst(IEnumerator gen)
	{
		if (Frames.Count == 0)
		{
			throw new ArgumentException("Cannot submit generators to inactive strand");
		}
		Queue.Insert(0, new PyStrandFrame(gen, this));
	}
}
