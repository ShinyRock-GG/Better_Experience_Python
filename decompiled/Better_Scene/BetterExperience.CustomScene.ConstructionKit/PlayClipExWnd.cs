using System;
using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class PlayClipExWnd : VisualElement
{
	private readonly InteractionManager interactionMgr;

	private readonly PoseAnimationClip clip;

	private TextField blendingTimeText;

	private DropdownField labels;

	private DropdownField completionModes;

	private DropdownField layersDropdown;

	private AnimatorLayer[] _layers = new AnimatorLayer[1];

	public Observable OnPlay { get; } = new Observable();

	public Observable OnClose { get; } = new Observable();

	public float blendingTime => float.Parse(blendingTimeText.value);

	public AnimatorLayer[] layers
	{
		get
		{
			_layers[0] = (AnimatorLayer)layersDropdown.index;
			return _layers;
		}
	}

	public AnimationCompletionMode completionMode => (AnimationCompletionMode)completionModes.index;

	public string label
	{
		get
		{
			if (labels.index != 0)
			{
				return labels.value;
			}
			return null;
		}
	}

	public PlayClipExWnd(InteractionManager im, PoseAnimationClip clip)
	{
		interactionMgr = im;
		this.clip = clip;
		CreateUI();
	}

	private void CreateUI()
	{
		TableBuilder val = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Label((VisualElement)this, "Clip");
			UIBuilder.Label((VisualElement)this, clip.UniqueName);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		TableBuilder val2 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Label((VisualElement)this, "Label");
			List<string> items = new List<string>();
			items.Add("[None]");
			items.AddRange(clip.Labels.Keys);
			labels = UIBuilder.Dropdown((VisualElement)this, (IEnumerable<string>)items);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		TableBuilder val3 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Label((VisualElement)this, "Blending time");
			blendingTimeText = UIBuilder.TextBox((VisualElement)this, "-1");
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		TableBuilder val4 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Label((VisualElement)this, "Completion mode");
			Array arr = Enum.GetValues(typeof(AnimationCompletionMode));
			string[] model = new string[arr.Length];
			for (int i = 0; i < arr.Length; i++)
			{
				model[i] = arr.GetValue(i).ToString();
			}
			completionModes = UIBuilder.Dropdown((VisualElement)this, (IEnumerable<string>)model);
		}
		finally
		{
			((IDisposable)val4)?.Dispose();
		}
		TableBuilder val5 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Label((VisualElement)this, "Layer");
			Array arr2 = Enum.GetValues(typeof(AnimatorLayer));
			string[] model2 = new string[arr2.Length];
			for (int j = 0; j < arr2.Length; j++)
			{
				model2[j] = arr2.GetValue(j).ToString();
			}
			layersDropdown = UIBuilder.Dropdown((VisualElement)this, (IEnumerable<string>)model2);
		}
		finally
		{
			((IDisposable)val5)?.Dispose();
		}
		TableBuilder val6 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
		try
		{
			UIBuilder.Button((VisualElement)this, "Play").clicked += delegate
			{
				OnClose.Invoke();
				OnPlay.Invoke();
			};
			UIBuilder.Button((VisualElement)this, "close").clicked += OnClose.Invoke;
		}
		finally
		{
			((IDisposable)val6)?.Dispose();
		}
	}
}
