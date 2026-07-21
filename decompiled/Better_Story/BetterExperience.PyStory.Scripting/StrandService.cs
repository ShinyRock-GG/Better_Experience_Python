using System;
using System.Collections;
using System.Collections.Generic;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.PyStory.Scripting;

public class StrandService
{
	private const int STRAND_UNFAIL_TIMEOUT = 5;

	protected Logger logger;

	public List<Func<PyStrand, IEnumerator>> StrandEpilogueGens = new List<Func<PyStrand, IEnumerator>>();

	public DispatcherService dispatcher { get; }

	public PyStrand MainStrand { get; }

	public ScopeSupport scriptingScope { get; private set; } = new ScopeSupport();

	public StrandService(DispatcherService dispatcherService)
	{
		logger = new Logger(GetType());
		dispatcher = dispatcherService;
		MainStrand = new PyStrand(scriptingScope);
	}

	public Coroutine StartPyEngineCoroutine(IEnumerator coro, PyStrand strand)
	{
		if (strand.Frames.Count > 0)
		{
			throw new ArgumentException("Cannot wrap active strand in coroutine");
		}
		if (logger.EnableDebug)
		{
			logger.Debug("Starting coroutine for {0}", coro.GetType());
		}
		strand.Frames.Push(new PyStrandFrame(coro, strand));
		return dispatcher.StartCoroutine(PyScriptCoroInternal(strand), scriptingScope);
	}

	private IEnumerator PyScriptCoroInternal(PyStrand strand)
	{
		if (logger.EnableDebug)
		{
			logger.Debug("Starting coroutine {0}", strand.Frames.Peek().DescribeContext());
		}
		while (strand.Scope.Started)
		{
			if (strand.IsFailed && Time.time - strand.FailedAt > 5f)
			{
				strand.IsFailed = false;
			}
			if (strand.Frames.Count > 0)
			{
				PyStrandFrame frame = strand.Frames.Peek();
				if (!strand.IsFailed && frame.StepForward())
				{
					if (logger.EnableDebug)
					{
						logger.Debug("Step {0} {1}", frame.DescribeContext(), frame.Flow.Current);
					}
					object current = frame.Flow.Current;
					if (current is IEnumerator ie2)
					{
						strand.Frames.Push(new PyStrandFrame(ie2, strand));
						continue;
					}
					current = frame.Flow.Current;
					if (current is IEnumerable ie3)
					{
						strand.Frames.Push(new PyStrandFrame(ie3.GetEnumerator(), strand));
					}
					else
					{
						yield return frame.Flow.Current;
					}
					continue;
				}
				strand.Frames.Pop();
				if (strand.Frames.Count != 0)
				{
					continue;
				}
				if (logger.EnableDebug)
				{
					logger.Debug("Coroutine {0} complete", frame.DescribeContext());
				}
				if (!strand.Scope.Started)
				{
					logger.Info("Frame {0} interrupted due to disposed scope", frame.DescribeContext());
				}
				else if (strand.IsFailed)
				{
					logger.Info("Frame {0} interrupted due to strand failure", frame.DescribeContext());
				}
				foreach (Func<PyStrand, IEnumerator> e in StrandEpilogueGens)
				{
					IEnumerator it = e(strand);
					while (it.MoveNext())
					{
						yield return it.Current;
					}
				}
			}
			else
			{
				if (strand.Queue.Count <= 0)
				{
					break;
				}
				PyStrandFrame next = strand.Queue[0];
				strand.Queue.RemoveAt(0);
				strand.Frames.Push(next);
				if (logger.EnableDebug)
				{
					logger.Debug("Starting next queued frame");
				}
			}
		}
	}

	internal void SpawnNext(IEnumerator e, PyStrand targetStrand)
	{
		targetStrand = CheckedStrand(targetStrand);
		if (targetStrand.Frames.Count > 0)
		{
			targetStrand.SubmitFirst(e);
		}
		else
		{
			StartPyEngineCoroutine(e, targetStrand);
		}
	}

	internal void SpawnLast(IEnumerator e, PyStrand targetStrand)
	{
		targetStrand = CheckedStrand(targetStrand);
		if (targetStrand.Frames.Count > 0)
		{
			targetStrand.SubmitLast(e);
		}
		else
		{
			StartPyEngineCoroutine(e, targetStrand);
		}
	}

	private PyStrand CheckedStrand(PyStrand target)
	{
		if (target == null)
		{
			target = MainStrand;
		}
		return target;
	}
}
