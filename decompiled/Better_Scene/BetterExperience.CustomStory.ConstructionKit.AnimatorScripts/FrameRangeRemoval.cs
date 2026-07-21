using System;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Wrappers.Windows;

namespace BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;

internal class FrameRangeRemoval
{
	private PoseAnimationClip p;

	private AnimatorScriptRegistry animatorScriptRegistry;

	public FrameRangeRemoval(PoseAnimationClip p, AnimatorScriptRegistry animatorScriptRegistry)
	{
		this.p = p;
		this.animatorScriptRegistry = animatorScriptRegistry;
	}

	internal void Process()
	{
		MayBeResult<string> promise = animatorScriptRegistry.Service.Session.Modal.RequestInput("Type 2 numbers: first removed frame and last removed", "0 1");
		promise.OnResult += Promise_OnResult;
	}

	private void Promise_OnResult(string obj)
	{
		if (obj == null)
		{
			return;
		}
		string[] parts = obj.Split(new char[1] { ' ' });
		int startFrame;
		int endFrame;
		if (parts.Length != 2)
		{
			animatorScriptRegistry.Service.Session.Modal.MessageError("Wrong input. Two numbers expected. Got " + parts.Length);
		}
		else if (!int.TryParse(parts[0], out startFrame))
		{
			animatorScriptRegistry.Service.Session.Modal.MessageError("Not a number (1)");
		}
		else if (!int.TryParse(parts[1], out endFrame))
		{
			animatorScriptRegistry.Service.Session.Modal.MessageError("Not a number (2)");
		}
		else
		{
			if (endFrame < startFrame || startFrame > p.States.Count)
			{
				return;
			}
			endFrame = Math.Min(endFrame, p.States.Count - 1);
			if (startFrame > 0)
			{
				PoseAnimationFrame pred = p.States[startFrame - 1];
				if (pred.Next.Remove(p.States[startFrame]) && p.States.Count - 1 > endFrame)
				{
					pred.Next.Add(p.States[endFrame + 1]);
				}
			}
			for (int i = startFrame; i <= endFrame; i++)
			{
				p.States.RemoveAt(startFrame);
			}
		}
	}
}
