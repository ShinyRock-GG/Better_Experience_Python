using System;
using System.Collections;
using System.Diagnostics;
using IronPython.Runtime;
using UnityEngine;

namespace BetterExperience.PyStory.Scripting;

public class PyStrandFrame
{
	private Stopwatch stopwatch = new Stopwatch();

	public static PyStrandFrame CurrentFrame { get; private set; }

	public IEnumerator Flow { get; }

	public string Name { get; set; }

	public PyStrandFrame Parent { get; }

	public PyStrand Strand { get; }

	public PyStrandFrame(IEnumerator e, PyStrand strand)
	{
		Flow = e;
		Parent = ((strand.Frames.Count > 0) ? strand.Frames.Peek() : null);
		Name = "unnamed";
		PythonGenerator pg = (PythonGenerator)((e is PythonGenerator) ? e : null);
		if (pg != null)
		{
			if (pg.gi_code != null && pg.gi_code.co_name != null)
			{
				Name = pg.gi_code.co_name + ":" + pg.gi_code.co_filename + ":" + pg.gi_code.co_firstlineno;
			}
			else
			{
				Name = "PythonGenerator";
			}
		}
		else
		{
			Name = e.GetType().Name;
		}
		Strand = strand;
	}

	public bool StepForward()
	{
		stopwatch.Restart();
		PyStrandFrame prevThread = CurrentFrame;
		try
		{
			CurrentFrame = this;
			return Flow.MoveNext();
		}
		catch (Exception e)
		{
			Strand.IsFailed = true;
			Strand.FailedAt = Time.time;
			Strand.Scope.NotifyCrash(e);
			return false;
		}
		finally
		{
			stopwatch.Stop();
			if (stopwatch.ElapsedMilliseconds > 1 && BetterExperience.Plugin.ProfilerLevel.Value > 0)
			{
				Logger.Global.Info("Slow code: {0} {1} ms", Name, stopwatch.ElapsedMilliseconds);
			}
			CurrentFrame = prevThread;
		}
	}

	internal string DescribeContext()
	{
		string result = Name;
		for (PyStrandFrame p = Parent; p != null; p = p.Parent)
		{
			result = p.Name + " > " + result;
		}
		return "[ " + Strand.Id + ((Strand.Name != null) ? (" " + Strand.Name) : "") + " ] " + result;
	}
}
