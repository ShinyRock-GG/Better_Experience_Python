using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

public class RelIK2EditorPlugin : AbstractEditorPlugin
{
	public class EffectorEditor : VisualElement
	{
		private RelIK2EditorPlugin controller;

		private Func<EffectorOverride> getter;

		private Action<EffectorOverride> setter;

		private Func<(Vector3, Quaternion)> captor;

		private EffectorOverride _override;

		public string label { get; private set; }

		public Toggle OffsetToggle { get; }

		public Toggle AngleToggle { get; }

		public Button CaptureBtn { get; }

		public Button RemoveBtn { get; }

		public TextField OffsetBox { get; }

		public Button SetAnchorBtn { get; }

		public EffectorOverride Effector
		{
			get
			{
				if (_override == null)
				{
					_override = getter();
					if (_override == null)
					{
						_override = new EffectorOverride();
					}
				}
				return _override;
			}
		}

		public void ClearCache()
		{
			_override = null;
		}

		public EffectorEditor(string label, RelIK2EditorPlugin relIK2EditorPlugin, Func<EffectorOverride> getter, Action<EffectorOverride> setter, Func<(Vector3, Quaternion)> captor)
		{
			controller = relIK2EditorPlugin;
			this.getter = getter;
			this.setter = setter;
			this.captor = captor;
			this.label = label;
			base.style.flexDirection = FlexDirection.Column;
			UIBuilder.StyleBorder<EffectorEditor>(this, 1);
			base.style.borderTopColor = Color.black;
			TableBuilder val = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)Style);
			try
			{
				UIBuilder.StyleMargin<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, label), 150), 0);
				OffsetToggle = UIBuilder.StyleMargin<CheckBox>(UIBuilder.StyleWidth<CheckBox>(UIBuilder.AddElement<CheckBox>((VisualElement)this, UIBuilder.WithTooltip<CheckBox>(new CheckBox("Offset"), "Follow offset changes")), 100), 0);
				AngleToggle = UIBuilder.StyleMargin<CheckBox>(UIBuilder.StyleWidth<CheckBox>(UIBuilder.AddElement<CheckBox>((VisualElement)this, UIBuilder.WithTooltip<CheckBox>(new CheckBox("Angle"), "Follow angle changes")), 100), 0);
				CaptureBtn = UIBuilder.StyleMargin<Button>(UIBuilder.StylePadding<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.WithTooltip<Button>(UIBuilder.Button((VisualElement)this, "Capture"), "Capture base offset and angle now"), 75), 1), 0);
				RemoveBtn = UIBuilder.StyleMargin<Button>(UIBuilder.StylePadding<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.WithTooltip<Button>(UIBuilder.Button((VisualElement)this, "Remove"), "Remove effector override"), 75), 1), 0);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)Style);
			try
			{
				UIBuilder.StyleMargin<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, ""), 150), 0);
				OffsetBox = UIBuilder.StylePadding<TextField>(UIBuilder.StyleMargin<TextField>(UIBuilder.StyleWidth<TextField>(UIBuilder.WithTooltip<TextField>(UIBuilder.TextBox((VisualElement)this, ""), "Stored offset and angle"), 200), 0), 1);
				OffsetBox.isReadOnly = true;
				OffsetBox.style.backgroundColor = new Color(0.79607844f, 0.79607844f, 0.79607844f);
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)Style);
			try
			{
				UIBuilder.StyleMargin<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, "   Anchor"), 150), 0);
				SetAnchorBtn = UIBuilder.StyleMargin<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.WithTooltip<Button>(UIBuilder.Button((VisualElement)this, "[None]"), "Assign anchor transform"), 200), 0);
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			CaptureBtn.clicked += CaptureBtn_clicked;
			SetAnchorBtn.clicked += SetAnchorBtn_clicked;
			OffsetToggle.RegisterValueChangedCallback(delegate
			{
				OnOffsetToggleChange();
			});
			AngleToggle.RegisterValueChangedCallback(delegate
			{
				OnAngleToggleChange();
			});
		}

		public static void Style(VisualElement obj)
		{
			ClipEditorPanelService.AnimationHelperWindow.Style(obj);
		}

		private void OnOffsetToggleChange()
		{
			if (Effector != null)
			{
				Effector.EnableOffset = OffsetToggle.value;
				controller.SaveEffectors();
				ReadState();
			}
		}

		private void OnAngleToggleChange()
		{
			if (Effector != null)
			{
				Effector.EnableAngle = AngleToggle.value;
				controller.SaveEffectors();
				ReadState();
			}
		}

		private void SetAnchorBtn_clicked()
		{
			if (Effector != null)
			{
				controller.ShowSelectAnchor(Effector.Anchor, delegate(string newAnchor)
				{
					Logger.Global.Error("Set anchor {0}", newAnchor);
					Effector.Anchor = newAnchor;
					controller.SaveEffectors();
					ReadState();
				});
			}
		}

		private void CaptureBtn_clicked()
		{
			if (Effector == null)
			{
				Logger.Global.Error(GetType().Name + ": Null effector " + label);
				return;
			}
			if (Effector.Anchor == null)
			{
				controller.Session.Modal.MessageError("Assign anchor first");
				Logger.Global.Error(GetType().Name + ": No anchor " + label);
				return;
			}
			var (o, a) = captor();
			Effector.Offset = o;
			Effector.Angle = a;
			Logger.Global.Error("capt {0} {1}", o, a);
			controller.SaveEffectors();
			ReadState();
		}

		public void ReadState()
		{
			if (Effector != null)
			{
				OffsetBox.value = Effector.Offset.ToString() + " " + Effector.Angle.ToString();
				if (Effector.Anchor != null)
				{
					SetAnchorBtn.text = Effector.Anchor;
				}
				else
				{
					SetAnchorBtn.text = "None";
				}
				OffsetToggle.value = Effector.EnableOffset;
			}
		}
	}

	private EffectorEditor handLeftEffector;

	private EffectorEditor handRightEffector;

	private EffectorEditor shoulderLeftEffector;

	private EffectorEditor shoulderRightEffector;

	private EffectorEditor playerRootEditor;

	private RelIK2Feature.RelIK2Service relik;

	private List<EffectorEditor> effectorEditors = new List<EffectorEditor>();

	public Button OnAddBtn { get; }

	public override string ToolName => "IK";

	protected RelIK2Feature.IKEffectorSet IK => base.InteractionManager.AnimationController.IKEffectorSet;

	protected EffectorData Data => base.Clip.EffectorData;

	public RelIK2EditorPlugin()
	{
		VisualElement rows = UIBuilder.VLayout((VisualElement)this);
		playerRootEditor = UIBuilder.AddElement<EffectorEditor>(rows, new EffectorEditor("Player Root", this, () => Data.PlayerRoot, delegate(EffectorOverride value)
		{
			Data.PlayerRoot = value;
		}, () => IK.PlayerRoot.Capture()));
		handLeftEffector = UIBuilder.AddElement<EffectorEditor>(rows, new EffectorEditor("Hand Left", this, () => Data.HandLeft, delegate(EffectorOverride value)
		{
			Data.HandLeft = value;
		}, () => IK.HandLeft.Capture()));
		handRightEffector = UIBuilder.AddElement<EffectorEditor>(rows, new EffectorEditor("Hand Right", this, () => Data.HandRight, delegate(EffectorOverride value)
		{
			Data.HandRight = value;
		}, () => IK.HandRight.Capture()));
		shoulderLeftEffector = UIBuilder.AddElement<EffectorEditor>(rows, new EffectorEditor("Shoulder Left", this, () => Data.ShoulderLeft, delegate(EffectorOverride value)
		{
			Data.ShoulderLeft = value;
		}, () => IK.ShoulderLeft.Capture()));
		shoulderRightEffector = UIBuilder.AddElement<EffectorEditor>(rows, new EffectorEditor("Shoulder Right", this, () => Data.ShoulderRight, delegate(EffectorOverride value)
		{
			Data.ShoulderRight = value;
		}, () => IK.ShoulderRight.Capture()));
		OnAddBtn = UIBuilder.Button(rows, "ADD");
		OnAddBtn.clicked += OnAddBtn_clicked;
		effectorEditors.Add(playerRootEditor);
		effectorEditors.Add(handRightEffector);
		effectorEditors.Add(handLeftEffector);
		effectorEditors.Add(shoulderLeftEffector);
		effectorEditors.Add(shoulderRightEffector);
		effectorEditors.ForEach(delegate(EffectorEditor x)
		{
			UIBuilder.Hide((VisualElement)x);
		});
	}

	private void OnAddBtn_clicked()
	{
		List<EffectorEditor> allowed = effectorEditors.Where((EffectorEditor x) => !UIBuilder.IsVisible((VisualElement)x)).ToList();
		ItemPicker.PickOne(base.GameView, allowed.Select((EffectorEditor x) => x.label).ToList(), delegate(int index)
		{
			UIBuilder.Show((VisualElement)allowed[index]);
		});
	}

	public override void InitializeServices()
	{
		base.InitializeServices();
		relik = base.Scope.Lookup<RelIK2Feature.RelIK2Service>();
	}

	protected override void OnClipOpened()
	{
		base.OnClipOpened();
		if (base.Clip.EffectorData == null)
		{
			base.Clip.EffectorData = new EffectorData();
		}
		foreach (EffectorEditor e in effectorEditors)
		{
			e.ClearCache();
			e.ReadState();
		}
	}

	public void SaveEffectors()
	{
		bool any = TryUpdateEffector(handLeftEffector, delegate(EffectorOverride dat)
		{
			base.Clip.EffectorData.HandLeft = dat;
		});
		any |= TryUpdateEffector(handRightEffector, delegate(EffectorOverride dat)
		{
			base.Clip.EffectorData.HandRight = dat;
		});
		any |= TryUpdateEffector(shoulderRightEffector, delegate(EffectorOverride dat)
		{
			base.Clip.EffectorData.ShoulderRight = dat;
		});
		any |= TryUpdateEffector(shoulderLeftEffector, delegate(EffectorOverride dat)
		{
			base.Clip.EffectorData.ShoulderLeft = dat;
		});
		any |= TryUpdateEffector(playerRootEditor, delegate(EffectorOverride dat)
		{
			base.Clip.EffectorData.PlayerRoot = dat;
		});
		base.Service.UpdateCurrentPose();
	}

	private bool TryUpdateEffector(EffectorEditor editor, Action<EffectorOverride> setter)
	{
		if (!UIBuilder.IsVisible((VisualElement)editor))
		{
			if (base.Clip.EffectorData != null)
			{
				setter(null);
			}
			return false;
		}
		if (base.Clip.EffectorData == null)
		{
			base.Clip.EffectorData = new EffectorData();
		}
		setter(editor.Effector);
		return true;
	}

	protected override void OnSelectedFrameChanged()
	{
		base.OnSelectedFrameChanged();
		effectorEditors.ForEach(delegate(EffectorEditor x)
		{
			x.ReadState();
		});
	}

	private void ShowSelectAnchor(string anchor, Action<string> selectionCallback)
	{
		ItemPicker picker = new ItemPicker(relik.Anchors.Select((RelIK2Feature.IKAnchor x) => x.Id), multiselect: false, custominput: false);
		if (anchor != null)
		{
			picker.SetSelection(new string[1] { anchor });
		}
		ComponentPopUp popup = new ComponentPopUp(picker);
		popup.SetCenterScreen(400, 500);
		popup.OnSave.Add(delegate
		{
			if (picker.Selection.Count == 0)
			{
				selectionCallback(null);
			}
			else
			{
				string obj = picker.Selection[0];
				selectionCallback(obj);
			}
		});
		popup.ShowModal(base.GameView);
	}
}
