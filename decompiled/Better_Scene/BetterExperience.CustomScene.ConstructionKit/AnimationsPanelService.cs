using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Base.Bones.Gizmos.Runtime;
using BepInEx.Configuration;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using BetterExperience.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class AnimationsPanelService : SessionService
{
	private class AnimationsPane : VisualElement
	{
		private VisualElement grid;

		public Button NewPoseBtn { get; }

		public Observable<PoseAnimationClip> PlayPose { get; } = new Observable<PoseAnimationClip>();

		public Observable<PoseAnimationClip> PlayPoseAdvanced { get; } = new Observable<PoseAnimationClip>();

		public Observable<PoseAnimationClip> LoadPose { get; } = new Observable<PoseAnimationClip>();

		public Observable<PoseAnimationClip> DeletePose { get; } = new Observable<PoseAnimationClip>();

		public Observable<PoseAnimationClip> EditDescriptor { get; } = new Observable<PoseAnimationClip>();

		public Observable<PoseAnimationClip> EditTags { get; } = new Observable<PoseAnimationClip>();

		public Button QuickSettings { get; }

		public FilterTextField FilterField { get; }

		public AnimationsPane()
		{
			//IL_0096: Unknown result type (might be due to invalid IL or missing references)
			//IL_00a0: Expected O, but got Unknown
			VisualElement seq = UIBuilder.HLayout((VisualElement)this);
			seq.style.minHeight = 30f;
			QuickSettings = UIBuilder.StylePadding<Button>(UIBuilder.StyleMargin<Button>(UIBuilder.StyleAlign<Button>(UIBuilder.StyleHeight<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.Button(seq, ""), 15), 15), Align.Center), 0), 0);
			FilterField = new FilterTextField();
			((VisualElement)(object)FilterField).style.flexGrow = 1f;
			seq.Add((VisualElement)(object)FilterField);
			NewPoseBtn = UIBuilder.Button(seq, "+");
			NewPoseBtn.style.fontSize = 12f;
			NewPoseBtn.style.paddingTop = 0f;
			NewPoseBtn.style.paddingBottom = 0f;
			ScrollView scroll = UIBuilder.Scroll((VisualElement)this);
			grid = UIBuilder.VLayout((VisualElement)scroll);
		}

		internal void ClearGrid()
		{
			grid.Clear();
		}

		internal void AddPose(PoseAnimationClip pose, bool hidePosture, bool hideVariant, bool showTags, InteractionDescriptor descriptor)
		{
			TableBuilder val = UIBuilder.Row(grid, (Action<VisualElement>)Style);
			Label tags;
			try
			{
				Button btn = UIBuilder.StyleAlign<Button>(UIBuilder.Button(grid, ""), Align.Center);
				btn.style.width = 13f;
				btn.style.height = 13f;
				btn.style.paddingRight = 2f;
				btn.clicked += delegate
				{
					GenericDropdownMenu genericDropdownMenu = new GenericDropdownMenu();
					genericDropdownMenu.AddItem("Play", isChecked: false, delegate
					{
						PlayPose.Invoke(pose);
					});
					genericDropdownMenu.AddItem("Play Advanced", isChecked: false, delegate
					{
						PlayPoseAdvanced.Invoke(pose);
					});
					genericDropdownMenu.AddSeparator("");
					genericDropdownMenu.AddItem("Load", isChecked: false, delegate
					{
						LoadPose.Invoke(pose);
					});
					genericDropdownMenu.AddItem("Delete", isChecked: false, delegate
					{
						DeletePose.Invoke(pose);
					});
					genericDropdownMenu.AddSeparator("");
					genericDropdownMenu.AddItem("Edit descriptor", isChecked: false, delegate
					{
						EditDescriptor.Invoke(pose);
					});
					genericDropdownMenu.AddSeparator("");
					genericDropdownMenu.AddItem("Tags", isChecked: false, delegate
					{
						EditTags.Invoke(pose);
					});
					genericDropdownMenu.DropDown(btn.worldBound, btn);
				};
				VisualElement container = UIBuilder.VLayout(grid);
				if (hidePosture && hideVariant)
				{
					UIBuilder.Label(container, pose.Name);
				}
				else if (hidePosture)
				{
					UIBuilder.Label(container, pose.Name + "." + pose.Variant);
				}
				else if (hideVariant)
				{
					UIBuilder.Label(container, pose.Posture.Id + "." + pose.Name);
				}
				else
				{
					UIBuilder.Label(container, pose.UniqueName);
				}
				tags = UIBuilder.Label(container, "");
				if (showTags && descriptor != null && descriptor.Tags != null && descriptor.Tags.Count > 0)
				{
					tags.text = "<i>" + string.Join(" ", descriptor.Tags) + "</i>";
				}
				else
				{
					UIBuilder.Hide((VisualElement)tags);
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			tags.style.fontSize = 13f;
			tags.style.paddingLeft = 5f;
		}

		private void Style(VisualElement e)
		{
			float sz = 0f;
			e.style.marginBottom = 0f;
			e.style.marginTop = sz;
			e.style.marginLeft = sz;
			e.style.marginRight = 0f;
			e.style.paddingBottom = 0f;
			e.style.paddingTop = sz;
			e.style.paddingLeft = sz;
			e.style.paddingRight = 0f;
			e.style.fontSize = 13f;
			if (e is Label || e is Button)
			{
				e.style.whiteSpace = WhiteSpace.Normal;
			}
		}
	}

	private class AnimationDescriptorEditor : VisualElement
	{
		private class FromToElement : VisualElement
		{
			private InteractionDescriptor.InteractionTransition model;

			private DropdownField fromClip;

			private DropdownField toClip;

			private TextField displayName;

			private TextField cancelDisplayName;

			private CheckBox reversible;

			public event Action OnRemove = delegate
			{
			};

			public FromToElement(InteractionDescriptor.InteractionTransition t, List<string> clips)
			{
				model = t;
				base.style.backgroundColor = new Color(0.7f, 0.7f, 0.7f);
				UIBuilder.StyleMargin<FromToElement>(this, 1);
				VisualElement transitionTable = UIBuilder.VLayout((VisualElement)this);
				List<string> keys = new List<string>();
				keys.Add("[None]");
				keys.AddRange(clips);
				TableBuilder val = UIBuilder.Row(transitionTable, (Action<VisualElement>)Style);
				try
				{
					UIBuilder.Label(transitionTable, "From").style.width = 40f;
					transitionTable.Add(fromClip = UIBuilder.StyleWidth<DropdownField>(new DropdownField(), 190));
					UIBuilder.Label(transitionTable, "To").style.width = 40f;
					transitionTable.Add(toClip = UIBuilder.StyleWidth<DropdownField>(new DropdownField(), 190));
					fromClip.choices = keys;
					toClip.choices = keys;
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				TableBuilder val2 = UIBuilder.Row(transitionTable, (Action<VisualElement>)Style);
				try
				{
					UIBuilder.WithTooltip<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label(transitionTable, "In"), 40), "Display Name");
					displayName = UIBuilder.StyleWidth<TextField>(UIBuilder.TextBox(transitionTable, "asd"), 190);
					UIBuilder.WithTooltip<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label(transitionTable, "Out"), 40), "Cancellation Name");
					cancelDisplayName = UIBuilder.StyleWidth<TextField>(UIBuilder.TextBox(transitionTable, "asdf"), 190);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				TableBuilder val3 = UIBuilder.Row(transitionTable, (Action<VisualElement>)Style);
				try
				{
					transitionTable.Add(reversible = UIBuilder.WithTooltip<CheckBox>(new CheckBox("Reversible"), "Can this clip be played in reverse to switch from [To] to [From]?"));
					UIBuilder.Button(transitionTable, "Remove").clicked += delegate
					{
						this.OnRemove();
					};
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				if (t.As == null)
				{
					t.As = new InteractionDescriptor();
				}
				Read();
			}

			private void Read()
			{
				if (model.From != null)
				{
					fromClip.value = model.From;
				}
				else
				{
					fromClip.index = 0;
				}
				if (model.To != null)
				{
					toClip.value = model.To;
				}
				else
				{
					toClip.index = 0;
				}
				displayName.value = model.As.DisplayName;
				cancelDisplayName.value = model.As.CancelDisplayName;
				reversible.value = model.Reversible;
			}

			public void Write()
			{
				if (fromClip.index == 0)
				{
					model.From = null;
				}
				else
				{
					model.From = fromClip.value;
				}
				if (toClip.index == 0)
				{
					model.To = null;
				}
				else
				{
					model.To = toClip.value;
				}
				model.Reversible = reversible.value;
				model.As.DisplayName = displayName.value.TrimToNull();
				model.As.CancelDisplayName = cancelDisplayName.value.TrimToNull();
			}
		}

		private Label clipId;

		private DropdownField clipType;

		private TextField displayName;

		private TextField cancelDisplayName;

		private DropdownField rootMotionType;

		private VisualElement transitionTable;

		private List<InteractionDescriptor.InteractionTransition> transitions = new List<InteractionDescriptor.InteractionTransition>();

		private List<string> otherClips;

		public InteractionDescriptor Model { get; set; }

		public event Action OnClose = delegate
		{
		};

		public event Action OnSave = delegate
		{
		};

		public AnimationDescriptorEditor()
		{
			VisualElement layout = UIBuilder.VLayout((VisualElement)this);
			TableBuilder val = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Interaction ID:"), "This descriptor is shared between all variants of clip");
				clipId = UIBuilder.Label(layout, "");
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Type"), "Clip type\nPose - this clip is available from idle state\nSubpose - clip is available only from specific pose\nTransition - hidden clip that is played on transition between states");
				layout.Add(clipType = new DropdownField());
				clipType.choices = new string[3]
				{
					InteractionType.pose.ToString(),
					InteractionType.subpose.ToString(),
					InteractionType.transition.ToString()
				}.ToList();
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Display Name"), "Text to display when requesting this pose");
				displayName = UIBuilder.TextBox(layout, "");
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			TableBuilder val4 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Cancellation Name"), "Text to display when cancelling this pose");
				cancelDisplayName = UIBuilder.TextBox(layout, "");
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			TableBuilder val5 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Root motion Type"), "Movement animation support\n\nNone - no root motion\nHipForward - walk animation support. Extracts forward motion from hip bone and applies to root transform\nHipSpin - rotation animation support. Extracts angular motion from hip bone around up-axis and applies to root transform");
				layout.Add(rootMotionType = new DropdownField());
				rootMotionType.choices = new string[3]
				{
					RootMotionType.None.ToString(),
					RootMotionType.HipForward.ToString(),
					RootMotionType.HipSpin.ToString()
				}.ToList();
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
			UIBuilder.LayoutColSizes(layout, new int[2] { 150, 300 });
			TableBuilder val6 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.WithTooltip<Label>(UIBuilder.Label(layout, "Supported Transitions"), "Subposes and transitions require context\n\nSubposes require [From] clip that means 'This subpose is available from [From] pose'\n\nTransitions require both clips and and will be played when transitioning from [From] to [To]");
				Button addTransition = UIBuilder.StyleHeight<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.Button(layout, "+"), 20), 20);
				addTransition.clicked += AddTransition_onClick;
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
			ScrollView scroll = UIBuilder.Scroll(layout);
			scroll.style.width = 500f;
			scroll.style.height = 200f;
			scroll.Add(transitionTable = new VisualElement());
			scroll.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
			VisualElement seq = UIBuilder.HLayout(layout);
			UIBuilder.Button(seq, "Save").clicked += Save_clicked;
			UIBuilder.Button(seq, "Close").clicked += delegate
			{
				this.OnClose();
			};
			layout.style.width = new Length(100f, LengthUnit.Percent);
			layout.style.height = new Length(100f, LengthUnit.Percent);
		}

		private void AddTransition_onClick()
		{
			InteractionDescriptor.InteractionTransition t = new InteractionDescriptor.InteractionTransition();
			transitions.Add(t);
			FromToElement e = new FromToElement(t, otherClips);
			transitionTable.Add(e);
			e.OnRemove += delegate
			{
				transitionTable.Remove(e);
				transitions.Remove(t);
			};
		}

		private void Save_clicked()
		{
			foreach (VisualElement c in transitionTable.Children())
			{
				if (c is FromToElement e)
				{
					e.Write();
				}
			}
			Model.SupportsTransitions = transitions;
			if (Enum.TryParse<InteractionType>(clipType.value, out var _ct))
			{
				Model.Type = _ct;
			}
			Model.DisplayName = displayName.value.TrimToNull();
			Model.CancelDisplayName = cancelDisplayName.value.TrimToNull();
			if (Enum.TryParse<RootMotionType>(rootMotionType.value, out var _rmt))
			{
				Model.RootMotionType = _rmt;
			}
			this.OnSave();
		}

		private void AfterModelSet()
		{
			clipId.text = Model.Id;
			clipType.value = Model.Type.ToString();
			displayName.value = Model.DisplayName;
			cancelDisplayName.value = Model.CancelDisplayName;
			rootMotionType.value = Model.RootMotionType.ToString();
			transitions = new List<InteractionDescriptor.InteractionTransition>();
			if (Model.SupportsTransitions != null)
			{
				transitions.AddRange(Model.SupportsTransitions);
			}
			RefreshTable();
		}

		private void RefreshTable()
		{
			transitionTable.Clear();
			foreach (InteractionDescriptor.InteractionTransition t in transitions)
			{
				FromToElement e = new FromToElement(t, otherClips);
				transitionTable.Add(e);
				e.OnRemove += delegate
				{
					transitionTable.Remove(e);
					transitions.Remove(t);
				};
			}
		}

		public void SetModel(InteractionDescriptor desc, List<string> otherClips)
		{
			this.otherClips = otherClips;
			Model = desc;
			AfterModelSet();
		}

		private static void Style(VisualElement e)
		{
			float sz = 1f;
			e.style.marginBottom = 0f;
			e.style.marginTop = sz;
			e.style.marginLeft = sz;
			e.style.marginRight = 0f;
			e.style.paddingBottom = 0f;
			e.style.paddingTop = sz;
			e.style.paddingLeft = sz;
			e.style.paddingRight = 0f;
			e.style.fontSize = 13f;
			if (e is Label || e is Button)
			{
				e.style.whiteSpace = WhiteSpace.Normal;
			}
		}
	}

	private POIManager poiManager;

	private InteractionManager interactionManager;

	private ClipEditorPanelService animatorTool;

	private PoseManager poseManager;

	private UITKManagedPanel rootUI;

	private ConfigEntry<bool> hidePostureName;

	private ConfigEntry<bool> hideVariantName;

	private List<string> allTags = new List<string>();

	private ConfigEntry<bool> showTags;

	private bool active;

	private AnimationsPane Window { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		Window = new AnimationsPane();
		poiManager = Lookup<POIManager>();
		interactionManager = Lookup<InteractionManager>();
		animatorTool = Lookup<ClipEditorPanelService>();
		poseManager = Lookup<PoseManager>();
		Lookup<ExtendedPoseEditor>().AddTab("Poses", Window, base.Scope);
		Window.QuickSettings.clicked += QuickSettings_clicked;
		Window.NewPoseBtn.clicked += NewAnimationBtn_OnClick;
		Window.DeletePose.Add(OnRemovePose, base.Scope);
		Window.LoadPose.Add(OnLoadPose, base.Scope);
		Window.EditDescriptor.Add(OnEditDescriptor, base.Scope);
		Window.EditTags.Add(OnEditTags, base.Scope);
		Window.PlayPose.Add(OnPlayPose, base.Scope);
		Window.PlayPoseAdvanced.Add(OnPlayAdvanced, base.Scope);
		Window.FilterField.ValueChanged.Add(delegate
		{
			RefreshPoseList();
		});
		poiManager.RegisteredPlacesChanged.Add(RefreshPoseList, base.Scope);
		interactionManager.OnCurrentPostureChanged.Add(RefreshPoseList, base.Scope);
		poseManager.OnPosesChanged.Add(OnPosturePosesChanged, base.Scope);
		poseManager.OnPosesChanged.Add(delegate
		{
			InvalidateTags();
		}, base.Scope);
		CustomSceneFeature feature = Lookup<CustomSceneFeature>();
		rootUI = feature.EditorUiPanel;
		hidePostureName = feature.PluginConfig.Bind<bool>("Editor.Animations", "HidePosture", false, (ConfigDescription)null);
		hidePostureName.SettingChanged += delegate
		{
			RefreshPoseList();
		};
		hideVariantName = feature.PluginConfig.Bind<bool>("Editor.Animations", "HideVariant", false, (ConfigDescription)null);
		hideVariantName.SettingChanged += delegate
		{
			RefreshPoseList();
		};
		showTags = feature.PluginConfig.Bind<bool>("Editor.Animations", "ShowTags", false, (ConfigDescription)null);
		showTags.SettingChanged += delegate
		{
			RefreshPoseList();
		};
		RefreshPoseList();
		InvalidateTags();
		Lookup<ExtendedPoseEditor>().OnStateChanged.Add(delegate(SkeletonEditorMode state)
		{
			SetActive(state != null);
		}, base.Scope);
	}

	private void SetActive(bool enabled)
	{
		active = enabled;
		if (active)
		{
			RefreshPoseList();
			InvalidateTags();
		}
	}

	private void InvalidateTags()
	{
		if (!active)
		{
			return;
		}
		allTags.Clear();
		foreach (Posture p in poseManager.Postures.Values)
		{
			foreach (PoseAnimationClip clip in p.Poses.AllClips.Values)
			{
				if (!p.Poses.ClipDescriptors.TryGetValue(clip, out var d) || d.Tags == null)
				{
					continue;
				}
				foreach (string t in d.Tags)
				{
					if (!allTags.Contains(t))
					{
						allTags.Add(t);
					}
				}
			}
		}
	}

	private void QuickSettings_clicked()
	{
		GenericDropdownMenu gdm = new GenericDropdownMenu();
		gdm.AddItem("Hide posture name", hidePostureName.Value, delegate
		{
			hidePostureName.Value = !hidePostureName.Value;
		});
		gdm.AddItem("Hide variant name", hideVariantName.Value, delegate
		{
			hideVariantName.Value = !hideVariantName.Value;
		});
		gdm.AddItem("Show tags", showTags.Value, delegate
		{
			showTags.Value = !showTags.Value;
		});
		gdm.DropDown(Window.QuickSettings.worldBound, Window.QuickSettings);
	}

	private void OnPosturePosesChanged(Posture obj)
	{
		if (interactionManager.CurrentPosture != null && interactionManager.CurrentPosture.Is(obj))
		{
			RefreshPoseList();
		}
	}

	private void OnEditDescriptor(PoseAnimationClip obj)
	{
		AnimationDescriptorEditor ade = new AnimationDescriptorEditor();
		InteractionDescriptor desc = obj.Descriptor;
		if (desc == null)
		{
			logger.Error("No descriptor at {0} / {1}", obj.UniqueName, obj.Posture.Id);
			return;
		}
		List<string> otherClips = (from x in (from x in obj.Posture.Poses.AllClips.Values
				where !x.IsGenerated
				select x.Name).Distinct()
			where x != obj.Name
			select x).ToList();
		if (!otherClips.Contains("Idle"))
		{
			otherClips.Insert(0, "Idle");
		}
		ade.SetModel(desc, otherClips);
		Action close = PopupWindowEx.ShowModal(ade, 550, 500, rootUI.GameView);
		ade.OnClose += close;
		ade.OnSave += delegate
		{
			poseManager.SaveDescriptor(desc);
			poseManager.InvalidateClips(obj.Posture, obj.Name, desc);
			close();
		};
	}

	private void OnEditTags(PoseAnimationClip obj)
	{
		InteractionDescriptor desc = obj.Descriptor;
		if (desc == null)
		{
			logger.Error("No descriptor at {0} / {1}", obj.UniqueName, obj.Posture.Id);
			return;
		}
		ItemPicker picker = new ItemPicker(allTags);
		if (desc.Tags != null)
		{
			picker.SetSelection(desc.Tags);
		}
		ComponentPopUp popup = new ComponentPopUp(picker);
		popup.SetCenterScreen(500, 600);
		popup.OnSave.Add(delegate
		{
			desc.Tags = new List<string>(picker.Selection);
			poseManager.SaveDescriptor(desc);
			poseManager.InvalidateClips(obj.Posture, obj.Name, desc);
		});
		popup.ShowModal(rootUI.GameView);
	}

	private void NewAnimationBtn_OnClick()
	{
		GenericDropdownMenu gdm = new GenericDropdownMenu();
		gdm.AddItem("New clip...", isChecked: false, TryStartClipEditor);
		gdm.AddItem("Save snapshot as clip", isChecked: false, SaveSnapshotAsClip);
		gdm.DropDown(Window.NewPoseBtn.worldBound, Window.NewPoseBtn);
	}

	private void SaveSnapshotAsClip()
	{
		AskForClipName(delegate(PoseAnimationClip dummyClip)
		{
			if (dummyClip.Posture.Poses.FindClips(dummyClip.UniqueName).Count != 0)
			{
				base.Session.Modal.MessageError("Clip " + dummyClip.UniqueName + " already exists");
			}
			else
			{
				BoneConfiguration item = interactionManager.TakeSnapshot(interactionManager.AnimationController.PostureOffset);
				dummyClip.Frames.Add(item);
				poseManager.WritePose(dummyClip, asTransition: true);
			}
		});
	}

	private void TryStartClipEditor()
	{
		AskForClipName(delegate(PoseAnimationClip targetClip)
		{
			animatorTool.EditClip(targetClip);
		});
	}

	private void AskForClipName(Action<PoseAnimationClip> callback)
	{
		POIPosture posture = interactionManager.CurrentPosture;
		if (posture == null)
		{
			base.Session.Modal.MessageError("Select posture first");
			return;
		}
		base.Session.Modal.RequestInput("Name new pose\nFormat: <Display Name>.<VariantId>", "<PoseName>.default").OnResult += delegate(string result)
		{
			if (result != null)
			{
				string text = "";
				string text2 = "";
				if (result.Contains("."))
				{
					string[] array = result.Split(new char[1] { '.' });
					if (array.Length != 2)
					{
						base.Session.Modal.MessageError("Invalid name: single dot expected");
						return;
					}
					text = array[0];
					text2 = array[1].ToLower().Trim();
				}
				else
				{
					text = result;
					text2 = "default";
				}
				PoseAnimationClip obj = new PoseAnimationClip(posture.Poses.Posture, text, text2);
				callback(obj);
			}
		};
	}

	private void OnRemovePose(PoseAnimationClip obj)
	{
		base.Session.Modal.MessageError("Not Implemented Yet");
	}

	private void OnPlayPose(PoseAnimationClip obj)
	{
		interactionManager.AnimationController.StartAnimation(obj);
	}

	private void OnPlayAdvanced(PoseAnimationClip obj)
	{
		PlayClipExWnd wnd = new PlayClipExWnd(interactionManager, obj);
		Action close = PopupWindowEx.ShowModal(wnd, 550, 500, rootUI.GameView);
		wnd.OnClose.Add(close);
		wnd.OnPlay.Add(delegate
		{
			if (interactionManager.HasActiveInteraction)
			{
				interactionManager.InterruptInteraction();
			}
			interactionManager.AnimationController.StartAnimation(obj, wnd.blendingTime, wnd.layers, wnd.completionMode, wnd.label);
		});
	}

	private void OnLoadPose(PoseAnimationClip obj)
	{
		ClipEditorPanelService animator = TryLookup<ClipEditorPanelService>();
		if (animator != null)
		{
			if (animator.IsEditingClip)
			{
				animator.TryImportKeyFrames(obj);
				return;
			}
			PoseAnimationClip copy = new PoseAnimationClipData(obj).ToClip(obj.UniqueName, poseManager);
			animator.EditClip(copy);
		}
	}

	private void RefreshPoseList()
	{
		if (!active)
		{
			return;
		}
		Window.ClearGrid();
		string filter = Window.FilterField.FilterValue;
		if (filter != null)
		{
			filter = filter.ToLower().Trim();
		}
		Func<string, bool> filterTest = (string x) => filter == null || x.ToLower().Contains(filter);
		Func<PoseAnimationClip, bool> filterTestEx = (PoseAnimationClip x) => filterTest(x.UniqueName);
		if (filter != null && filter.StartsWith("."))
		{
			filter = filter.Substring(1);
			filterTestEx = delegate(PoseAnimationClip x)
			{
				PosturePoseCollection poses2 = interactionManager.CurrentPosture.Poses;
				InteractionDescriptor descriptor = x.Descriptor;
				if (descriptor == null)
				{
					return false;
				}
				List<string> tags = descriptor.Tags;
				if (tags == null)
				{
					return false;
				}
				foreach (string current in tags)
				{
					if (current.Contains(filter))
					{
						return true;
					}
				}
				return false;
			};
		}
		logger.Debug("Refresh list");
		if (interactionManager.CurrentPosture == null)
		{
			return;
		}
		logger.Debug("Has posture");
		PosturePoseCollection poses = interactionManager.CurrentPosture.Poses;
		foreach (PoseAnimationClip pose in poses.AllClips.Values.OrderBy((PoseAnimationClip x) => x.UniqueName).Where(filterTestEx))
		{
			if (!pose.IsGenerated)
			{
				InteractionDescriptor desc = pose.Descriptor;
				Window.AddPose(pose, hidePostureName.Value, hideVariantName.Value, showTags.Value, desc);
			}
		}
	}
}
