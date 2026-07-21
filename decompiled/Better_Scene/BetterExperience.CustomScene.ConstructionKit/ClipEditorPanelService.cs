using System;
using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi;
using Assets.Base.Bones.Gizmos.Runtime;
using Assets.CustomPoses;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.CustomScene.Poser;
using BetterExperience.CustomStory.ConstructionKit.AnimatorScripts;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Windows;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

public class ClipEditorPanelService : StoryService
{
	public class AnimationHelperController
	{
		private Logger logger = Logger.Create<AnimationHelperController>();

		private AnimationHelperWindow view;

		private PoseAnimationClipModel model;

		private bool playing;

		private float duration;

		private float length;

		public MainModalWindow Modal { get; set; }

		public InteractionManager InteractionManager { get; set; }

		public PoseAnimationClip SingleFrameClip { get; set; }

		public DispatcherService DispatcherService { get; set; }

		public Observable<PoseAnimationClip> OnSave { get; } = new Observable<PoseAnimationClip>();

		public Observable OnSaveManual { get; } = new Observable();

		public Observable<bool> OnExit { get; } = new Observable<bool>();

		public AnimatorScriptRegistry AnimatorScriptRegistry { get; set; }

		public AnimationHelperController(AnimationHelperWindow view, PoseAnimationClipModel model)
		{
			this.view = view;
			this.model = model;
			view.AddKeyFrameBtn.clicked += AddKeyFrameBtn_OnClick;
			model.KeyframeDataChanged += Model_KeyframeDataChanged;
			model.FrameDataChanged += Model_FrameDataChanged;
			model.SelectedFrameChanged += Model_SelectedFrameChanged;
			view.AddKeyFromGallery.clicked += AddKeyFromGallery_OnClick;
			view.AddAnimFrameBtn.clicked += AddAnimFrameBtn_OnClick;
			view.FrameSave.clicked += FrameSave_OnClick;
			view.PlayBtn.clicked += PlayBtn_OnClick;
			view.InsertBefore.clicked += InsertBefore_OnClick;
			view.InsertAfter.clicked += InsertAfter_OnClick;
			view.RemoveFrame.clicked += RemoveFrame_OnClick;
			view.InterpolateBtn.clicked += InterpolateBtn_OnClick;
			view.AbandonClip.clicked += AbandonClip_OnClick;
			view.SaveClip.clicked += SaveClip_OnClick;
			view.ToolsBtn.clicked += ToolsBtn_clicked;
			view.Timeline.SelectedFrameChanged += Timeline_SelectedFrameChanged;
			view.SaveCheckpoint.clicked += OnSaveManual.Invoke;
			Model_KeyframeDataChanged();
			Model_FrameDataChanged();
			Model_LabelDataChanged();
		}

		private void Model_LabelDataChanged()
		{
			view.Labels.Clear();
			model.Clip.LabelOrder.Clear();
			(from x in model.Clip.Labels
				select (Key: x.Key, Value: x.Value, model.Clip.States.IndexOf(x.Value)) into x
				orderby x.Item3
				select x.Key).ForEach(model.Clip.LabelOrder.Add);
			for (int i = 0; i < model.Clip.LabelOrder.Count; i++)
			{
				string label = model.Clip.LabelOrder[i];
				PoseAnimationFrame state = model.Clip.Labels[label];
				LabelRow row = new LabelRow(label);
				view.Labels.Add(row);
				row.ApplyBtn.clicked += delegate
				{
					model.SetSelectedState(state);
				};
			}
			UIBuilder.Recursive<VisualElement>(view.KeyFrames, (Action<VisualElement>)AnimationHelperWindow.Style);
		}

		private void ToolsBtn_clicked()
		{
			GenericDropdownMenu gdm = new GenericDropdownMenu();
			AnimatorScriptRegistry.Reset();
			foreach (KeyValuePair<string, Action<PoseAnimationClip>> e in AnimatorScriptRegistry.Scripts)
			{
				gdm.AddItem(e.Key, isChecked: false, delegate
				{
					RunScript(e.Value);
				});
			}
			gdm.DropDown(view.ToolsBtn.worldBound, view.ToolsBtn);
		}

		private void RunScript(Action<PoseAnimationClip> value)
		{
			try
			{
				Transform rb = InteractionManager.AnimationController.Skeleton.rootBone;
				Transform art = ((Character)(object)InteractionManager.Session.Guest.Impl).animatorRootMotionTransform;
				BoneConfiguration c = InteractionManager.CurrentPosture.Configuration;
				Vector3 contextPos = art.TransformPoint(c.RootOffset);
				Quaternion contextRot = art.rotation * c.RootRotation;
				AnimatorScriptRegistry.DeltaRootPos = -rb.InverseTransformPoint(contextPos);
				AnimatorScriptRegistry.DeltaRootRot = Quaternion.Inverse(contextRot) * rb.rotation;
				value(model.Clip);
				model.InvalidateAll();
				Modal.MessageError("Done");
			}
			catch (Exception ex)
			{
				logger.Error(ex, "script failed");
				Modal.ShowBigMessage(ex.ToString());
			}
		}

		private void Timeline_SelectedFrameChanged(int index)
		{
			List<PoseAnimationFrame> states = model.Clip.States;
			if (states.Count > index && index >= 0)
			{
				PoseAnimationFrame state = states[index];
				model.SetSelectedState(state);
			}
		}

		internal void Dispose()
		{
			view.AddKeyFrameBtn.clicked -= AddKeyFrameBtn_OnClick;
			model.KeyframeDataChanged -= Model_KeyframeDataChanged;
			model.FrameDataChanged -= Model_FrameDataChanged;
			model.SelectedFrameChanged -= Model_SelectedFrameChanged;
			view.AddKeyFromGallery.clicked -= AddKeyFromGallery_OnClick;
			view.AddAnimFrameBtn.clicked -= AddAnimFrameBtn_OnClick;
			view.FrameSave.clicked -= FrameSave_OnClick;
			view.PlayBtn.clicked -= PlayBtn_OnClick;
			view.InsertBefore.clicked -= InsertBefore_OnClick;
			view.InsertAfter.clicked -= InsertAfter_OnClick;
			view.RemoveFrame.clicked -= RemoveFrame_OnClick;
			view.InterpolateBtn.clicked -= InterpolateBtn_OnClick;
			view.AbandonClip.clicked -= AbandonClip_OnClick;
			view.SaveClip.clicked -= SaveClip_OnClick;
			view.ToolsBtn.clicked -= ToolsBtn_clicked;
			view.Timeline.SelectedFrameChanged -= Timeline_SelectedFrameChanged;
			UIBuilder.Hide((VisualElement)view);
		}

		private void AbandonClip_OnClick()
		{
			OnExit.Invoke(arg1: false);
		}

		private void SaveClip_OnClick()
		{
			OnExit.Invoke(arg1: true);
		}

		private void InterpolateBtn_OnClick()
		{
			if (model.SelectedFrame == null)
			{
				return;
			}
			HashSet<int> preds = model.GatherFramePredecessors();
			HashSet<int> succs = model.GatherFrameSuccessors();
			if (preds.Count == 1 && succs.Count == 1)
			{
				BoneConfiguration a = model.Clip.Frames[preds.First()];
				BoneConfiguration b = model.Clip.Frames[succs.First()];
				BoneConfiguration result = new BoneConfiguration(a);
				result.Rotations.Clear();
				foreach (KeyValuePair<string, Quaternion> kv in a.Rotations)
				{
					string bone = kv.Key;
					Quaternion sourceRot = kv.Value;
					if (b.Rotations.TryGetValue(bone, out var targetRot))
					{
						Quaternion rot = Quaternion.Lerp(sourceRot, targetRot, 0.5f);
						result.Rotations[bone] = rot;
					}
				}
				result.HipOffset = Vector3.Lerp(a.HipOffset, b.HipOffset, 0.5f);
				result.RootOffset = Vector3.Lerp(a.RootOffset, b.RootOffset, 0.5f);
				result.RootRotation = Quaternion.Lerp(a.RootRotation, b.RootRotation, 0.5f);
				ApplyBoneTransform(result);
			}
			else
			{
				Modal.MessageError($"Unable to interpolate:\n{preds.Count} predecessing and {succs.Count} successing key frames");
			}
		}

		private void RemoveFrame_OnClick()
		{
			if (model.SelectedFrame == null)
			{
				return;
			}
			int index = model.Clip.States.IndexOf(model.SelectedFrame);
			if (index == -1)
			{
				return;
			}
			Modal.MessageBoxYesNo($"Remove frame {index}. Continue?").OnResult += delegate(bool result)
			{
				if (result)
				{
					if (model.SelectedFrame.Next.Count > 0)
					{
						model.RedirectState(model.SelectedFrame, model.SelectedFrame.Next[0]);
					}
					model.Clip.States.Remove(model.SelectedFrame);
					if (model.SelectedFrame.Label != null)
					{
						model.Clip.Labels.Remove(model.SelectedFrame.Label);
						Model_LabelDataChanged();
					}
					Model_FrameDataChanged();
					if (model.Clip.States.Count > 0)
					{
						if (index < model.Clip.States.Count)
						{
							model.SetSelectedState(model.Clip.States[index]);
						}
						else
						{
							model.SetSelectedState(model.Clip.States[index - 1]);
						}
					}
					else
					{
						model.SetSelectedState(null);
					}
					OnSave.Invoke(model.Clip);
				}
			};
		}

		private void InsertAfter_OnClick()
		{
			if (model.SelectedFrame != null)
			{
				int index = model.Clip.States.IndexOf(model.SelectedFrame);
				if (index != -1)
				{
					CopyInto(index + 1, model.SelectedFrame);
				}
			}
		}

		private void InsertBefore_OnClick()
		{
			if (model.SelectedFrame != null)
			{
				int index = model.Clip.States.IndexOf(model.SelectedFrame);
				if (index != -1)
				{
					CopyInto(index, model.SelectedFrame);
				}
			}
		}

		private void CopyInto(int index, PoseAnimationFrame source)
		{
			PoseAnimationFrame target = model.AddAnimationFrame(index);
			int sourceIndex = model.Clip.States.IndexOf(source);
			model.SetSelectedState(target);
			target.Key = source.Key;
			if (index < sourceIndex)
			{
				model.RedirectState(source, target);
				target.Next.Add(source);
			}
			else
			{
				target.Next.AddRange(source.Next);
				target.Next.Remove(target);
				source.Next.Clear();
				source.Next.Add(target);
			}
			model.SetSelectedState(target);
			OnSave.Invoke(model.Clip);
		}

		private void PlayBtn_OnClick()
		{
			if (playing || model.SelectedFrame == null || model.SelectedFrame.Next.Count == 0)
			{
				view.PlayBtn.text = "Play";
				view.RemainingLabel.text = "";
				playing = false;
			}
			else
			{
				view.PlayBtn.text = "Stop";
				playing = true;
				duration = 0f;
				length = UnityEngine.Random.Range(model.SelectedFrame.MinDuration, model.SelectedFrame.MaxDuration) + model.SelectedFrame.FadeIn;
				ApplyKeyFrame(model.SelectedFrame.Key, model.SelectedFrame.FadeIn);
			}
		}

		private void FrameSave_OnClick()
		{
			PoseAnimationFrame selected = model.SelectedFrame;
			bool changed = false;
			if (float.TryParse(view.FrameFadeIn.value, out var res))
			{
				float val = Mathf.Max(0f, res);
				if (selected.FadeIn != val)
				{
					selected.FadeIn = val;
					changed = true;
				}
			}
			if (int.TryParse(view.FrameKeyFrame.value, out var key) && key >= 0 && key < model.Clip.Frames.Count && selected.Key != key)
			{
				selected.Key = key;
				changed = true;
			}
			if (float.TryParse(view.FrameMinDuration.value, out var a) && float.TryParse(view.FrameMaxDuration.value, out var b))
			{
				float min = Mathf.Min(a, b);
				float max = Mathf.Max(a, b);
				if (min != selected.MinDuration || max != selected.MaxDuration)
				{
					selected.MinDuration = min;
					selected.MaxDuration = max;
					changed = true;
				}
			}
			List<PoseAnimationFrame> bkp = new List<PoseAnimationFrame>(selected.Next);
			selected.Next.Clear();
			string[] nexts = view.FrameNext.value.Split(new char[1] { ',' });
			string[] array = nexts;
			foreach (string n in array)
			{
				if (int.TryParse(n.Trim(), out var id) && id >= 0 && id < model.Clip.States.Count)
				{
					selected.Next.Add(model.Clip.States[id]);
				}
			}
			changed |= !bkp.SequenceEqual(selected.Next);
			string label = view.FrameLabel.value;
			if (label != null)
			{
				label = label.Trim();
				if (label.Length == 0)
				{
					label = null;
				}
			}
			if (label != selected.Label)
			{
				if (selected.Label == null)
				{
					if (model.Clip.Labels.TryGetValue(label, out var existing))
					{
						Modal.MessageError($"Label {label} is already used by {model.Clip.States.IndexOf(existing)}");
						return;
					}
					selected.Label = label;
					model.Clip.Labels[label] = selected;
				}
				else
				{
					string existingLabel = selected.Label;
					if (model.Clip.Labels.TryGetValue(existingLabel, out var existing2))
					{
						if (existing2 != selected)
						{
							logger.Warn("Unexpected label reference");
						}
						else
						{
							model.Clip.Labels.Remove(existingLabel);
						}
					}
					selected.Label = label;
					if (label != null)
					{
						model.Clip.Labels[label] = selected;
					}
				}
				changed = true;
				Model_LabelDataChanged();
			}
			if (changed)
			{
				OnSave.Invoke(model.Clip);
			}
			UpdateKeyFrameRefsCount();
		}

		private void AddAnimFrameBtn_OnClick()
		{
			model.AddAnimationFrame(model.Clip.States.Count);
		}

		private void Model_SelectedFrameChanged()
		{
			view.Timeline.SetSelectedFrame(model.Clip.States.Count, model.Clip.States.IndexOf(model.SelectedFrame));
			if (model.SelectedFrame == null)
			{
				UIBuilder.Hide(view.FrameDataGrid);
			}
			else
			{
				PoseAnimationFrame selected = model.SelectedFrame;
				UIBuilder.Show(view.FrameDataGrid);
				view.FrameFadeIn.value = selected.FadeIn.ToString();
				view.FrameKeyFrame.value = selected.Key.ToString();
				view.FrameMinDuration.value = selected.MinDuration.ToString();
				view.FrameMaxDuration.value = selected.MaxDuration.ToString();
				view.FrameNext.value = ((selected.Next.Count == 0) ? "" : string.Join(",", selected.Next.Select((PoseAnimationFrame x) => model.Clip.States.IndexOf(x)).ToArray()));
				view.FrameLabel.value = selected.Label;
				ApplyKeyFrame(selected.Key, selected.FadeIn);
			}
			if (playing)
			{
				PlayBtn_OnClick();
			}
		}

		private void Model_FrameDataChanged()
		{
			view.Timeline.SetSelectedFrame(model.Clip.States.Count, model.Clip.States.IndexOf(model.SelectedFrame));
			UpdateKeyFrameRefsCount();
			OnSave.Invoke(model.Clip);
			Model_SelectedFrameChanged();
		}

		private void AddKeyFromGallery_OnClick()
		{
			Modal.SelectPosePromGallery().OnResult += delegate(string result)
			{
				if (result != null)
				{
					TargetChar instance = TargetChar.instance;
					Character character = ((instance != null) ? instance.character : null);
					GizmosDeSkeleton skeleton = InteractionManager.AnimationController.Skeleton;
					Vector3 position = skeleton.rootBone.position;
					Quaternion rotation = skeleton.rootBone.rotation;
					SaveLoadCustomPoses.LoadSavedData(character, ref result, 0, null, null);
					skeleton.rootBone.position = position;
					skeleton.rootBone.rotation = rotation;
					Transform transform = skeleton.rootBone.FindDeepChild("CC_Base_Hip");
					if (transform != null)
					{
						Vector3 localPosition = transform.localPosition;
						localPosition.x = 0f;
						localPosition.y = 0f;
						transform.localPosition = localPosition;
					}
					AddKeyFrameBtn_OnClick();
				}
			};
		}

		private void Model_KeyframeDataChanged()
		{
			view.KeyFrames.Clear();
			for (int i = 0; i < model.Clip.Frames.Count; i++)
			{
				KeyFrameRow row = new KeyFrameRow("[ " + i + " ]");
				view.KeyFrames.Add(row);
				int index = i;
				row.ApplyBtn.clicked += delegate
				{
					ApplyKeyFrame(index);
				};
				row.RemoveBtn.clicked += delegate
				{
					RemoveKeyFrame(index);
				};
			}
			UIBuilder.Recursive<VisualElement>(view.KeyFrames, (Action<VisualElement>)AnimationHelperWindow.Style);
			UpdateKeyFrameRefsCount();
			OnSave.Invoke(model.Clip);
		}

		private void UpdateKeyFrameRefsCount()
		{
			List<int>[] counts = new List<int>[model.Clip.Frames.Count].Fill(() => new List<int>());
			for (int i = 0; i < model.Clip.States.Count; i++)
			{
				PoseAnimationFrame f = model.Clip.States[i];
				if (f.Key >= 0 && f.Key < counts.Length)
				{
					counts[f.Key].Add(i);
				}
			}
			for (int i2 = 0; i2 < view.KeyFrames.childCount; i2++)
			{
				if (i2 < counts.Length)
				{
					((KeyFrameRow)view.KeyFrames[i2]).SetKeyFrameRefs(counts[i2]);
				}
			}
		}

		private void RemoveKeyFrame(int index)
		{
			VisualElement c = view.KeyFrames;
			if (index >= c.childCount)
			{
				return;
			}
			KeyFrameRow row = (KeyFrameRow)c[index];
			if (row.Refs.Count == 0)
			{
				DispatcherService.InvokeLater(delegate
				{
					model.RemoveKeyFrame(index);
				});
			}
			else
			{
				Modal.MessageError(string.Format("Frame is still referenced by {0}", string.Join(",", row.Refs)));
			}
		}

		public void ApplyKeyFrame(int index, float fadin = 1f)
		{
			BoneConfiguration frame = model.Clip.Frames[index];
			SingleFrameClip.Frames[0] = frame;
			SingleFrameClip.States[0].FadeIn = fadin;
			SingleFrameClip.States[0].MinDuration = 0f;
			SingleFrameClip.States[0].MaxDuration = 0f;
			SingleFrameClip.PlayerState = model.Clip.PlayerState;
			SingleFrameClip.EffectorData = model.Clip.EffectorData;
			InteractionManager.AnimationController.AddClip(SingleFrameClip);
			InteractionManager.AnimationController.StartAnimation(SingleFrameClip, fadin);
		}

		private void ApplyBoneTransform(BoneConfiguration data)
		{
			SingleFrameClip.Frames[0] = data;
			InteractionManager.AnimationController.AddClip(SingleFrameClip);
			InteractionManager.AnimationController.StartAnimation(SingleFrameClip);
		}

		public BoneConfiguration TakeSnapshot()
		{
			return InteractionManager.TakeSnapshot(InteractionManager.AnimationController.PostureOffset);
		}

		private void AddKeyFrameBtn_OnClick()
		{
			BoneConfiguration snapshot = TakeSnapshot();
			if (snapshot != null)
			{
				logger.Error("snapshot taken");
				int frame = model.AddKeyFrame(snapshot);
				if (model.SelectedFrame != null)
				{
					model.SelectedFrame.Key = frame;
					Model_SelectedFrameChanged();
					UpdateKeyFrameRefsCount();
				}
				OnSave.Invoke(model.Clip);
			}
			else
			{
				logger.Error("no snapshot taken");
			}
		}

		internal void DoUpdate()
		{
			if (!playing)
			{
				return;
			}
			if (model.SelectedFrame == null)
			{
				PlayBtn_OnClick();
			}
			else if (duration > length)
			{
				if (model.SelectedFrame.Next.Count == 0)
				{
					PlayBtn_OnClick();
					return;
				}
				List<PoseAnimationFrame> nexts = model.SelectedFrame.Next;
				int index = UnityEngine.Random.Range(0, nexts.Count);
				model.SetSelectedState(nexts[index]);
				if (!playing)
				{
					PlayBtn_OnClick();
				}
			}
			else
			{
				duration += Time.deltaTime;
				view.RemainingLabel.text = (length - duration).ToString();
			}
		}
	}

	public class AnimationHelperWindow : PopupWindow
	{
		private const int WND_VSIZE = 250;

		private const int WND_HSIZE = 900;

		private const int KEYFRAME_HSIZE = 200;

		private TimelineComponent timeline;

		private List<VisualElement> customEditors = new List<VisualElement>();

		private List<string> customEditorNames = new List<string>();

		public Button AddAnimFrameBtn { get; private set; }

		public Button AddKeyFrameBtn { get; set; }

		public Button AddKeyFromGallery { get; private set; }

		public VisualElement KeyFrames { get; set; }

		public VisualElement Labels { get; private set; }

		public TextField FrameMinDuration { get; private set; }

		public TextField FrameMaxDuration { get; private set; }

		public TextField FrameFadeIn { get; private set; }

		public TextField FrameKeyFrame { get; private set; }

		public TextField FrameNext { get; private set; }

		public DropdownBox ComponentMode { get; private set; }

		public Button FrameSave { get; private set; }

		public Button InterpolateBtn { get; private set; }

		public Button InsertBefore { get; private set; }

		public Button InsertAfter { get; private set; }

		public Button RemoveFrame { get; private set; }

		public Button PlayBtn { get; private set; }

		public Label RemainingLabel { get; private set; }

		public Button SaveCheckpoint { get; private set; }

		public VisualElement FrameDataGrid { get; private set; }

		public Button SaveClip { get; private set; }

		public Button AbandonClip { get; private set; }

		public Button ToolsBtn { get; private set; }

		public TimelineComponent Timeline => timeline;

		public VisualElement ComponentContainer { get; private set; }

		public TextField FrameLabel { get; private set; }

		public DropdownBox ShortcutMode { get; private set; }

		public AnimationHelperWindow()
		{
			text = "Animator Tool";
			base.style.position = Position.Absolute;
			base.style.width = 900f;
			base.style.height = 250f;
			VisualElement hl = UIBuilder.HLayout((VisualElement)this);
			hl.Add(CreateKeyFramePane());
			hl.Add(CreateFramePane());
			UIBuilder.Recursive<VisualElement>((VisualElement)this, (Action<VisualElement>)Style);
			hl[1].style.marginLeft = 10f;
		}

		private VisualElement CreateKeyFramePane()
		{
			ScrollView scroll = new ScrollView();
			VisualElement seq = UIBuilder.VLayout((VisualElement)scroll);
			VisualElement row0 = UIBuilder.HLayout(seq);
			SaveClip = UIBuilder.Button(row0, "Save Clip");
			AbandonClip = UIBuilder.Button(row0, "Abandon");
			ToolsBtn = UIBuilder.Button(row0, "Tools");
			VisualElement row1 = UIBuilder.HLayout(seq);
			ShortcutMode = UIBuilder.AddElement<DropdownBox>(row1, new DropdownBox(new string[2] { "Keyframes", "Labels" }));
			ShortcutMode.SelectedIndexChanged.Add(OnSelectedShortcutModeChanged);
			AddKeyFrameBtn = UIBuilder.Button(row1, "Snap+");
			AddKeyFromGallery = UIBuilder.Button(row1, "Gallery+");
			KeyFrames = UIBuilder.VLayout(seq);
			Labels = UIBuilder.VLayout(seq);
			OnSelectedShortcutModeChanged(0);
			return scroll;
		}

		private void OnSelectedShortcutModeChanged(int obj)
		{
			UIBuilder.SetVisible(KeyFrames, obj == 0);
			UIBuilder.SetVisible(Labels, obj == 1);
		}

		private VisualElement CreateFramePane()
		{
			ScrollView scroll = new ScrollView();
			scroll.style.flexGrow = 1f;
			scroll.style.marginLeft = 10f;
			VisualElement seq = UIBuilder.VLayout((VisualElement)scroll);
			VisualElement row = UIBuilder.HLayout(seq);
			UIBuilder.Label(row, "Frames:");
			timeline = new TimelineComponent();
			row.Add(timeline);
			AddAnimFrameBtn = UIBuilder.Button(row, "+");
			AddAnimFrameBtn.style.marginLeft = 10f;
			FrameDataGrid = UIBuilder.VLayout(seq);
			VisualElement toolbar = UIBuilder.HLayout(FrameDataGrid);
			toolbar.style.marginTop = 10f;
			ComponentMode = UIBuilder.AddElement<DropdownBox>(toolbar, new DropdownBox(new string[3] { "Timeline", "Expressions", "Inverse Kinematics" }));
			ComponentMode.SelectedIndexChanged.Add(OnSelectedComponentModeChanged);
			FrameSave = UIBuilder.Button(toolbar, "Save");
			FrameSave.style.marginLeft = 10f;
			InterpolateBtn = UIBuilder.Button(toolbar, "Lerp");
			InsertBefore = UIBuilder.Button(toolbar, "I.Before");
			InsertAfter = UIBuilder.Button(toolbar, "I.After");
			RemoveFrame = UIBuilder.Button(toolbar, "Remove");
			PlayBtn = UIBuilder.Button(toolbar, "Play");
			RemainingLabel = UIBuilder.Label(toolbar, "");
			RemainingLabel.style.marginLeft = 10f;
			SaveCheckpoint = UIBuilder.Button((VisualElement)this, "[Save clip snapshot]");
			UIBuilder.WithTooltip<Button>(SaveCheckpoint, "Save current snapshot to disk");
			UIBuilder.Hide((VisualElement)SaveCheckpoint);
			SaveCheckpoint.style.position = Position.Absolute;
			SaveCheckpoint.style.top = 2f;
			SaveCheckpoint.style.right = 2f;
			ComponentContainer = UIBuilder.VLayout(FrameDataGrid);
			VisualElement grid = UIBuilder.VLayout(ComponentContainer);
			TableBuilder val = UIBuilder.Row(grid, (Action<VisualElement>)FrameDataStyle);
			try
			{
				UIBuilder.Label(grid, "Min. Duration");
				FrameMinDuration = UIBuilder.TextBox(grid, "0");
				UIBuilder.Label(grid, "Max. Duration");
				FrameMaxDuration = UIBuilder.TextBox(grid, "0");
				UIBuilder.Label(grid, "Fade In");
				FrameFadeIn = UIBuilder.TextBox(grid, "0");
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row(grid, (Action<VisualElement>)FrameDataStyle);
			try
			{
				UIBuilder.Label(grid, "Key Frame");
				FrameKeyFrame = UIBuilder.TextBox(grid, "0");
				UIBuilder.Label(grid, "Next state");
				FrameNext = UIBuilder.TextBox(grid, "0");
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row(grid, (Action<VisualElement>)FrameDataStyle);
			try
			{
				UIBuilder.Label(grid, "Label");
				FrameLabel = UIBuilder.TextBox(grid, "");
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			AddTool("Timeline", grid, null);
			return scroll;
		}

		private static void FrameDataStyle(VisualElement e)
		{
			Style(e);
			if (e is Label l)
			{
				l.style.width = 100f;
				l.style.marginLeft = 1f;
			}
			else if (e is TextField t)
			{
				t.style.width = 50f;
			}
		}

		public static void Style(VisualElement e)
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
			if (e is Button)
			{
				e.style.marginLeft = 1f;
				e.style.marginRight = 1f;
				e.style.marginTop = 1f;
				e.style.marginBottom = 1f;
			}
		}

		internal void AddTool(string name, VisualElement window, ScopeSupport scope)
		{
			customEditors.Add(window);
			customEditorNames.Add(name);
			if (scope != null)
			{
				scope.OnDispose += delegate
				{
					customEditors.Remove(window);
					customEditorNames.Remove(name);
					ComponentMode.Items = customEditorNames;
				};
			}
			ComponentMode.Items = customEditorNames;
		}

		private void OnSelectedComponentModeChanged(int mode)
		{
			if (mode >= 0 && mode < customEditors.Count)
			{
				VisualElement c = customEditors[mode];
				ComponentContainer.Clear();
				ComponentContainer.Add(c);
			}
		}
	}

	public class KeyFrameRow : VisualElement
	{
		public Button ApplyBtn { get; }

		public Button RemoveBtn { get; }

		public List<int> Refs { get; private set; }

		public KeyFrameRow(string name)
		{
			base.style.flexDirection = FlexDirection.Row;
			UIBuilder.Label((VisualElement)this, name);
			ApplyBtn = UIBuilder.Button((VisualElement)this, "run");
			RemoveBtn = UIBuilder.Button((VisualElement)this, "del");
			UIBuilder.Recursive<VisualElement>((VisualElement)this, (Action<VisualElement>)AnimationHelperWindow.Style);
		}

		internal void SetKeyFrameRefs(List<int> list)
		{
			Refs = list;
			RemoveBtn.text = "del [" + Refs.Count + "]";
		}
	}

	public class LabelRow : VisualElement
	{
		public Button ApplyBtn { get; }

		public LabelRow(string name)
		{
			base.style.flexDirection = FlexDirection.Row;
			UIBuilder.Label((VisualElement)this, name);
			ApplyBtn = UIBuilder.Button((VisualElement)this, "run");
			UIBuilder.Recursive<VisualElement>((VisualElement)this, (Action<VisualElement>)AnimationHelperWindow.Style);
		}
	}

	public class PoseAnimationClipModel
	{
		private PoseAnimationClip clip;

		public PoseAnimationClip Clip => clip;

		public PoseAnimationFrame SelectedFrame { get; private set; }

		public event Action KeyframeDataChanged = delegate
		{
		};

		public event Action SelectedFrameChanged = delegate
		{
		};

		public event Action FrameDataChanged = delegate
		{
		};

		public PoseAnimationClipModel(PoseAnimationClip clip)
		{
			this.clip = clip;
		}

		public PoseAnimationFrame AddAnimationFrame(int index)
		{
			PoseAnimationFrame f = new PoseAnimationFrame();
			clip.States.Insert(index, f);
			if (index != 0 && clip.States[index - 1].Next.Count == 0)
			{
				clip.States[index - 1].Next.Add(f);
			}
			SetSelectedState(f);
			this.FrameDataChanged();
			return f;
		}

		internal int AddKeyFrame(BoneConfiguration snapshot)
		{
			int index = clip.Frames.Count;
			clip.AddFrameData(clip.Frames.Count, snapshot);
			this.KeyframeDataChanged();
			Logger.Global.Error("add key frame");
			return index;
		}

		internal void RemoveKeyFrame(int index)
		{
			int end = clip.Frames.Count - 1;
			clip.Frames.RemoveAt(index);
			for (int i = index + 1; i <= end; i++)
			{
				RewriteKeyFrameRefs(i, i - 1);
			}
			this.KeyframeDataChanged();
			this.SelectedFrameChanged();
		}

		private void RewriteKeyFrameRefs(int source, int target)
		{
			Logger.Global.Error("Rewrite KF {0}->{1}", source, target);
			foreach (PoseAnimationFrame s in clip.States)
			{
				if (s.Key == source)
				{
					s.Key = target;
				}
			}
		}

		public HashSet<int> GatherFramePredecessors()
		{
			HashSet<int> frames = new HashSet<int>();
			foreach (PoseAnimationFrame f in clip.States)
			{
				if (f.Next.Contains(SelectedFrame))
				{
					frames.Add(f.Key);
				}
			}
			return frames;
		}

		public HashSet<int> GatherFrameSuccessors()
		{
			HashSet<int> frames = new HashSet<int>();
			if (SelectedFrame != null)
			{
				SelectedFrame.Next.ForEach(delegate(PoseAnimationFrame x)
				{
					frames.Add(x.Key);
				});
			}
			return frames;
		}

		internal void SetSelectedState(PoseAnimationFrame animatedPoseFrame)
		{
			if (!clip.States.Contains(animatedPoseFrame))
			{
				animatedPoseFrame = null;
			}
			SelectedFrame = animatedPoseFrame;
			this.SelectedFrameChanged();
		}

		internal void RedirectState(PoseAnimationFrame source, PoseAnimationFrame target)
		{
			foreach (PoseAnimationFrame s in Clip.States)
			{
				if (s.Next.Remove(source))
				{
					s.Next.Add(target);
				}
			}
		}

		internal void InvalidateAll()
		{
			this.KeyframeDataChanged();
			this.FrameDataChanged();
		}
	}

	public class TimelineComponent : VisualElement
	{
		private Button pageLeft;

		private Button shiftLeft;

		private Button shiftRight;

		private VisualElement buttons;

		private Button pageRight;

		private Button[] frameBtns = new Button[15];

		private int totalCount;

		private RangeInt currentRange;

		private int currentSelection = -1;

		public event Action<int> SelectedFrameChanged = delegate
		{
		};

		public TimelineComponent()
		{
			base.style.flexDirection = FlexDirection.Row;
			VisualElement preBtns = UIBuilder.HLayout((VisualElement)this);
			buttons = UIBuilder.HLayout((VisualElement)this);
			VisualElement postBtns = UIBuilder.HLayout((VisualElement)this);
			pageLeft = UIBuilder.Button(preBtns, "<<");
			shiftLeft = UIBuilder.Button(preBtns, "<");
			shiftRight = UIBuilder.Button(postBtns, ">");
			pageRight = UIBuilder.Button(postBtns, ">>");
			for (int i = 0; i < frameBtns.Length; i++)
			{
				int number = i;
				frameBtns[i] = UIBuilder.Button(buttons, "[" + i + "]");
				frameBtns[i].clicked += delegate
				{
					OnClicked(number);
				};
				frameBtns[i].style.minWidth = 30f;
			}
			shiftLeft.clicked += delegate
			{
				OnClicked(currentSelection - 1);
			};
			shiftRight.clicked += delegate
			{
				OnClicked(currentSelection + 1);
			};
			pageLeft.clicked += delegate
			{
				this.SelectedFrameChanged(0);
			};
			pageRight.clicked += delegate
			{
				this.SelectedFrameChanged(totalCount - 1);
			};
			UIBuilder.Hide((VisualElement)shiftLeft);
			UIBuilder.Hide((VisualElement)shiftRight);
		}

		private void OnClicked(int i)
		{
			this.SelectedFrameChanged(currentRange.start + i);
		}

		internal void SetSelectedFrame(int totalCount, int selectedIndex)
		{
			this.totalCount = totalCount;
			if (totalCount < frameBtns.Length)
			{
				for (int i = 0; i < frameBtns.Length; i++)
				{
					if (i >= totalCount)
					{
						UIBuilder.Hide((VisualElement)frameBtns[i]);
					}
					else
					{
						UIBuilder.Show((VisualElement)frameBtns[i]);
					}
				}
				UIBuilder.Hide((VisualElement)pageLeft);
				UIBuilder.Hide((VisualElement)pageRight);
			}
			else
			{
				UIBuilder.Show((VisualElement)pageLeft);
				UIBuilder.Show((VisualElement)pageRight);
				for (int j = 0; j < frameBtns.Length; j++)
				{
					UIBuilder.Show((VisualElement)frameBtns[j]);
				}
			}
			if (totalCount == 0)
			{
				return;
			}
			int center = frameBtns.Length / 2;
			int a = selectedIndex - center;
			int b = selectedIndex + frameBtns.Length - center;
			if (a < 0)
			{
				b -= a;
				a = 0;
			}
			if (b > totalCount)
			{
				a -= b - totalCount;
				b = totalCount;
				a = Math.Max(0, a);
			}
			for (int k = a; k < b; k++)
			{
				int index = k - a;
				Button btn = frameBtns[index];
				if (k == selectedIndex)
				{
					btn.text = ">" + k + "<";
					continue;
				}
				string number = k.ToString();
				if (k > 99 && index != 0 && index != frameBtns.Length - 1)
				{
					number = ".." + number.Substring(number.Length - 2);
				}
				btn.text = "[" + number + "]";
			}
			currentRange = new RangeInt(a, b - a);
			currentSelection = selectedIndex;
		}
	}

	private AnimationHelperController controller;

	private POIManager waypointManager;

	private DispatcherService dispatcherService;

	private PoseManager poseManager;

	private InteractionManager interactionManager;

	private PoseAnimationClip singleFrameClip;

	private Repository<PoseAnimationClipData> repository = new Repository<PoseAnimationClipData>("epac", "editor", "EPACs");

	private AnimatorScriptRegistry animatorScriptRegistry = new AnimatorScriptRegistry();

	private List<AbstractEditorPlugin> editorPlugins = new List<AbstractEditorPlugin>();

	public AnimationHelperWindow Window { get; private set; }

	public PoseAnimationClipModel Model { get; set; }

	public bool IsEditingClip => controller != null;

	public Observable OnClipOpened { get; } = new Observable();

	public Observable OnClipSaving { get; } = new Observable();

	public Observable OnClipClosed { get; } = new Observable();

	public override void OnInit()
	{
		base.OnInit();
		base.AsyncHandles.Add(repository.InitAsync(base.Story.VFS));
	}

	public BoneConfiguration TakeSnapshot()
	{
		return controller.TakeSnapshot();
	}

	public override void OnStart()
	{
		base.OnStart();
		Window = new AnimationHelperWindow();
		Window.style.top = new Length(100f, LengthUnit.Percent);
		Window.style.left = new Length(0f, LengthUnit.Percent);
		Window.style.marginTop = -250f;
		UIBuilder.Hide((VisualElement)Window);
		UIBuilder.EnableWindowDrag((PopupWindow)Window);
		Lookup<CustomSceneFeature>().EditorUiPanel.GameView.Add(Window);
		waypointManager = Lookup<POIManager>();
		dispatcherService = Lookup<DispatcherService>();
		poseManager = Lookup<PoseManager>();
		interactionManager = Lookup<InteractionManager>();
		interactionManager.OnCurrentPostureChanged.Add(TryRestoreState, base.Scope);
		Lookup<ExtendedPoseEditor>().OnStateChanged.Add(SetSkeleton, base.Scope);
		dispatcherService.DoUpdate.Add(OnUpdate, base.Scope);
		animatorScriptRegistry.Service = this;
		editorPlugins.Add(new RelIK2EditorPlugin());
		editorPlugins.Add(new PlayerStateEditorPlugin());
		foreach (AbstractEditorPlugin plugin in editorPlugins)
		{
			base.Scope.AddChild(plugin.Scope);
		}
	}

	internal void UpdateCurrentPose()
	{
		if (Model != null && Model.SelectedFrame != null && controller != null)
		{
			controller.ApplyKeyFrame(Model.SelectedFrame.Key, Model.SelectedFrame.FadeIn);
		}
	}

	private void OnUpdate()
	{
		if (controller != null)
		{
			controller.DoUpdate();
		}
	}

	public void RegisterTool(string name, VisualElement window, ScopeSupport scope)
	{
		Window.AddTool(name, window, scope);
	}

	public void EditClip(PoseAnimationClip clip, bool fromRestore = false)
	{
		logger.Error("editclip ed {0}", clip.EffectorData);
		if (controller != null)
		{
			base.Session.Modal.MessageError("Close current clip to continue");
			return;
		}
		logger.Info("Opening clip {0}", clip.UniqueName);
		UpdateWindowTitle(clip);
		Model = new PoseAnimationClipModel(clip);
		controller = new AnimationHelperController(Window, Model);
		controller.InteractionManager = base.Scope.Lookup<InteractionManager>();
		controller.DispatcherService = dispatcherService;
		controller.Modal = base.Session.Modal;
		controller.AnimatorScriptRegistry = animatorScriptRegistry;
		singleFrameClip = new PoseAnimationClip(clip.Posture, "Dummy", "0");
		singleFrameClip.NoCache = true;
		singleFrameClip.UniqueName = "Dummy";
		singleFrameClip.FullName = "Dummy";
		singleFrameClip.Name = "Dummy";
		singleFrameClip.Frames.Add(null);
		singleFrameClip.States.Add(new PoseAnimationFrame());
		singleFrameClip.IsGenerated = true;
		controller.SingleFrameClip = singleFrameClip;
		UIBuilder.Show((VisualElement)Window);
		OnClipOpened.Invoke();
		controller.OnSave.Add(delegate(PoseAnimationClip aClip)
		{
			if (aClip != null)
			{
				if (aClip.Frames.Count <= 100 && aClip.States.Count <= 100)
				{
					PersistWorkspace(aClip.Posture.Id, aClip);
				}
				UpdateWindowTitle(aClip);
			}
		}, base.Scope);
		controller.OnSaveManual.Add(delegate
		{
			PersistWorkspace(Model.Clip.Posture.Id, Model.Clip);
		});
		if (clip.Frames.Count <= 100 && clip.States.Count <= 100)
		{
			PersistWorkspace(clip.Posture.Id, clip);
		}
		controller.OnExit.Add(delegate(bool save)
		{
			if (save)
			{
				PoseAnimationClip currentClip = Model.Clip;
				base.Session.Modal.RequestInput("Save pose as...", currentClip.UniqueName).OnResult += delegate(string result)
				{
					if (result != null)
					{
						if (result != currentClip.UniqueName)
						{
							string[] array = result.Split(new char[1] { '.' });
							if (array.Length != 3)
							{
								base.Session.Modal.MessageError("Wrong name format");
								return;
							}
							currentClip.Name = array[1];
							currentClip.FullName = array[1] + "." + array[2];
							currentClip.UniqueName = result;
						}
						poseManager.WritePose(currentClip);
						DiscardWorkspace(Model.Clip.Posture);
						DisposeController();
						Model = null;
					}
				};
			}
			else
			{
				base.Session.Modal.MessageBoxYesNo("Abandon this clip? All changes will be lost").OnResult += delegate(bool result)
				{
					if (result)
					{
						DiscardWorkspace(Model.Clip.Posture);
						DisposeController();
						Model = null;
					}
				};
			}
		});
	}

	private void UpdateWindowTitle(PoseAnimationClip aClip)
	{
		if (aClip.Frames.Count > 100 || aClip.States.Count > 100)
		{
			if (!UIBuilder.IsVisible((VisualElement)Window.SaveCheckpoint))
			{
				SetTitle(aClip.UniqueName + " - LARGE CLIP - NO AUTOSAVE");
				UIBuilder.Show((VisualElement)Window.SaveCheckpoint);
			}
		}
		else if (UIBuilder.IsVisible((VisualElement)Window.SaveCheckpoint))
		{
			SetTitle(aClip.UniqueName);
			UIBuilder.Hide((VisualElement)Window.SaveCheckpoint);
		}
	}

	internal void TryImportKeyFrames(PoseAnimationClip obj)
	{
		if (controller == null)
		{
			return;
		}
		if (obj.Frames.Count == 0)
		{
			base.Session.Modal.MessageError("Selected clip has no frames to import");
			return;
		}
		base.Session.Modal.MessageBoxYesNo("You are going to import all {0} key frames. Continue?", obj.Frames.Count).OnResult += delegate(bool result)
		{
			if (result)
			{
				foreach (BoneConfiguration current in obj.Frames)
				{
					Model.AddKeyFrame(current);
				}
			}
		};
	}

	private void SetTitle(string uniqueName)
	{
		Window.text = "Animator Tool - " + uniqueName;
	}

	private void DisposeController()
	{
		if (controller != null)
		{
			OnClipClosed.Invoke();
			controller.Dispose();
			controller = null;
		}
	}

	internal void DiscardWorkspace(Posture posture)
	{
		string name = "animator." + posture.Id;
		PoseAnimationClipData d = new PoseAnimationClipData();
		d.Id = name;
		repository.Delete(d);
	}

	public PoseAnimationClip GetPostureWorkspaceClip(Posture posture)
	{
		string name = "animator." + posture.Id;
		PoseAnimationClipData data = repository.Get(name);
		if (data == null)
		{
			return null;
		}
		try
		{
			PoseAnimationClip result = data.ToClip(data.UniqueName, poseManager);
			if (result == null)
			{
				logger.Error("Unable to restore file {0}", name);
			}
			return result;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Unable to restore clip");
			return null;
		}
	}

	private void PersistWorkspace(string id, PoseAnimationClip clip)
	{
		OnClipSaving.Invoke();
		string name = "animator." + id;
		PoseAnimationClipData data = new PoseAnimationClipData(clip);
		data.Id = name;
		repository.Save(data);
	}

	public void TryRestoreState()
	{
		logger.Debug("Trying to restore state");
		if (interactionManager.IsEditorActive && controller != null)
		{
			if (Model.Clip != null)
			{
				PersistWorkspace(Model.Clip.Posture.Id, Model.Clip);
			}
			controller.Dispose();
		}
		if (controller == null && interactionManager.IsEditorActive && interactionManager.CurrentPosture != null)
		{
			logger.Debug("Editor active {0}", interactionManager.IsEditorActive);
			PoseAnimationClip clip = GetPostureWorkspaceClip(interactionManager.CurrentPosture.Poses.Posture);
			if (clip != null)
			{
				EditClip(clip);
			}
		}
		else
		{
			logger.Debug("Unable to restore state {0} {1} {2}", controller == null, interactionManager.IsEditorActive, interactionManager.CurrentPosture != null);
		}
	}

	private void SetSkeleton(SkeletonEditorMode obj)
	{
		if (obj == null)
		{
			UIBuilder.Hide((VisualElement)Window);
			DisposeController();
			if (Model != null && Model.Clip != null)
			{
				PersistWorkspace(Model.Clip.Posture.Id, Model.Clip);
			}
		}
		else
		{
			TryRestoreState();
		}
	}
}
