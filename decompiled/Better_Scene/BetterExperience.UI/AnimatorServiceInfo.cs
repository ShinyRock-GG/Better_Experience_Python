using System.Collections.Generic;
using System.Text;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Poser;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.UI;

internal class AnimatorServiceInfo : Label
{
	private InteractionManager interactionManager;

	private PoseAnimationController controller;

	private StringBuilder buffer = new StringBuilder();

	private List<IAnimationClipState> states = new List<IAnimationClipState>();

	public bool ShowInfo { get; set; }

	public AnimatorServiceInfo(InteractionManager interactionManager)
	{
		this.interactionManager = interactionManager;
		controller = interactionManager.AnimationController;
		controller.ActiveStateChanged.Add(Redraw);
		controller._OnLayerClipComplete.Add(delegate
		{
			Redraw();
		});
		base.style.position = Position.Absolute;
		base.style.bottom = 5f;
		base.style.right = 5f;
		base.style.color = Color.white;
		Redraw();
		base.schedule.Execute(TimerRedraw).Every(1000L);
	}

	private void TimerRedraw()
	{
		if (ShowInfo)
		{
			Redraw();
		}
		else if (buffer.Length > 0)
		{
			text = "";
			buffer.Clear();
		}
	}

	private void Redraw()
	{
		if (!ShowInfo)
		{
			return;
		}
		buffer.Clear();
		states.Clear();
		buffer.Append("Animation Info:\n");
		buffer.Append("Queue state: ").Append(interactionManager.QueueLength).Append("\n");
		if (interactionManager.CurrentOperation != null)
		{
			buffer.Append("Current op:\n");
			buffer.Append("     ").Append(interactionManager.CurrentOperation).Append("\n");
		}
		for (int i = 0; i < 11; i++)
		{
			AnimatorLayer l = (AnimatorLayer)i;
			IAnimationClipState state = controller.GetClipByLayer(l);
			if (state != null && !states.Contains(state))
			{
				states.Add(state);
				buffer.Append(l).Append(": ");
				Format(state);
				buffer.Append("\n");
			}
		}
		IAnimationClipState additive = controller.GetClipByLayer(AnimatorLayer.Additive);
		if (additive != null)
		{
			buffer.Append(AnimatorLayer.Additive).Append(": ");
			Format(additive);
			buffer.Append("\n");
		}
		text = buffer.ToString();
	}

	private void Format(IAnimationClipState state)
	{
		buffer.Append(state.Clip.UniqueName);
		if (state.Cyclic)
		{
			buffer.Append(" Loop ");
		}
		buffer.AppendFormat("{0:0.0}", state.Time).Append("/");
		buffer.AppendFormat("{0:0.0}", state.Length);
	}
}
