using System;
using System.Collections.Generic;
using BetterExperience.CustomScene.Poser;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

public class PlayerStateEditorPlugin : AbstractEditorPlugin
{
	private class OverrideMapping
	{
		public CheckBox Toggle { get; }

		public TextField Field { get; }

		public Button Capture { get; }

		public Func<FloatOverride> Reader { get; }

		public Action<FloatOverride> Writer { get; }

		public Func<float> Captor { get; }

		public event Action OnChanged = delegate
		{
		};

		public OverrideMapping(CheckBox hipyToggle, TextField hipyField, Button hipyCapture, Func<FloatOverride> reader, Action<FloatOverride> writer, Func<float> captor)
		{
			OverrideMapping overrideMapping = this;
			Toggle = hipyToggle;
			Field = hipyField;
			Capture = hipyCapture;
			Reader = reader;
			Writer = writer;
			Captor = captor;
			Toggle.RegisterValueChangedCallback(delegate
			{
				overrideMapping.Write();
			});
			Field.RegisterValueChangedCallback(delegate
			{
				overrideMapping.Write();
			});
			hipyCapture.clicked += delegate
			{
				overrideMapping.Field.value = captor().ToString();
			};
		}

		public void Read()
		{
			FloatOverride v = Reader();
			Toggle.value = v.Enabled;
			Field.value = v.Value.ToString();
			Logger.Global.Error("Read {0}: {1} {2}", Toggle.label, v.Enabled, v.Value);
		}

		public void Write()
		{
			FloatOverride v = new FloatOverride
			{
				Enabled = Toggle.value
			};
			float.TryParse(Field.value, out var fv);
			v.Value = fv;
			Writer(v);
			this.OnChanged();
			Logger.Global.Error("Write {0}: {1} {2}", Toggle.label, v.Enabled, v.Value);
		}
	}

	private CheckBox hipyToggle;

	private TextField hipyField;

	private Button hipyCapture;

	private CheckBox hipzToggle;

	private TextField hipzField;

	private Button hipzCapture;

	private List<OverrideMapping> bindings = new List<OverrideMapping>();

	public override string ToolName => "Player";

	private void Style(VisualElement obj)
	{
		ClipEditorPanelService.AnimationHelperWindow.Style(obj);
	}

	public PlayerStateEditorPlugin()
	{
		TableBuilder val = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)Style);
		try
		{
			hipyToggle = UIBuilder.StyleWidth<CheckBox>(UIBuilder.AddElement<CheckBox>((VisualElement)this, new CheckBox("Hip Y")), 75);
			hipyField = UIBuilder.StyleWidth<TextField>(UIBuilder.AddElement<TextField>((VisualElement)this, new TextField()), 150);
			hipyCapture = UIBuilder.StyleWidth<Button>(UIBuilder.Button((VisualElement)this, "Capture"), 75);
			bindings.Add(new OverrideMapping(hipyToggle, hipyField, hipyCapture, () => base.Clip.PlayerState.HipY, delegate(FloatOverride v)
			{
				base.Clip.PlayerState.HipY = v;
			}, () => base.Session.Player.PelvisY));
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		TableBuilder val2 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)Style);
		try
		{
			hipzToggle = UIBuilder.StyleWidth<CheckBox>(UIBuilder.AddElement<CheckBox>((VisualElement)this, new CheckBox("Hip Z")), 75);
			hipzField = UIBuilder.StyleWidth<TextField>(UIBuilder.AddElement<TextField>((VisualElement)this, new TextField()), 150);
			hipzCapture = UIBuilder.StyleWidth<Button>(UIBuilder.Button((VisualElement)this, "Capture"), 75);
			bindings.Add(new OverrideMapping(hipzToggle, hipzField, hipzCapture, () => base.Clip.PlayerState.HipZ, delegate(FloatOverride v)
			{
				base.Clip.PlayerState.HipZ = v;
			}, () => base.Session.Player.PelvisZ));
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		foreach (OverrideMapping b in bindings)
		{
			b.OnChanged += delegate
			{
				base.Service.UpdateCurrentPose();
			};
		}
	}

	protected override void OnClipOpened()
	{
		base.OnClipOpened();
		if (base.Clip.PlayerState == null)
		{
			base.Clip.PlayerState = new PlayerStateData();
		}
		foreach (OverrideMapping m in bindings)
		{
			m.Read();
		}
	}
}
