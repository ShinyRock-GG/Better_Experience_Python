using System;
using System.Collections.Generic;
using Assets.Base.Bones.Gizmos.Runtime;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Battlehub.RTCommon;
using Battlehub.RTHandles;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using BetterExperience.Utils;
using RootMotion;
using RootMotion.FinalIK;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class BonePanelService : SessionService
{
	private class BoneEditorHelperWindow : VisualElement
	{
		private const int TEXTBOX_SIZE = 200;

		public TextField BoneName { get; }

		public TextField PositionX { get; }

		public TextField PositionY { get; }

		public TextField PositionZ { get; }

		public TextField RotationX { get; }

		public TextField RotationZ { get; }

		public TextField RotationY { get; }

		public Button FwdUpdateRotation { get; }

		public Button BwdUpdateRotation { get; }

		public TextField RotationUpdate { get; }

		public Button SyncRotationBtn { get; }

		public Button MirrorRotationBtn { get; }

		public Button ToOppositeBtn { get; }

		public Button ToAscending { get; }

		public Button ToDescending { get; }

		public Button SyncChildrenRotationBtn { get; }

		public Button MirrorChildrenRotationBtn { get; }

		public bool UnlockHips { get; private set; }

		public Button CopyTreeBtn { get; }

		public Button PasteTreeBtn { get; }

		public Button PinEffector { get; }

		public BoneEditorHelperWindow()
		{
			BoneEditorHelperWindow boneEditorHelperWindow = this;
			style.flexDirection = FlexDirection.Column;
			TableBuilder val = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "Bone");
				BoneName = UIBuilder.TextBox((VisualElement)this, "");
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "Navigation");
				VisualElement seq = UIBuilder.HLayout((VisualElement)this);
				ToOppositeBtn = UIBuilder.Button(seq, "L<>R");
				ToAscending = UIBuilder.Button(seq, "ASC");
				ToDescending = UIBuilder.Button(seq, "DSC");
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "Pos. XYZ RGB:");
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			TableBuilder val4 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				PositionX = UIBuilder.TextBox((VisualElement)this, "");
				PositionY = UIBuilder.TextBox((VisualElement)this, "");
				PositionZ = UIBuilder.TextBox((VisualElement)this, "");
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			TableBuilder val5 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "Rot. XYZ RGB:");
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
			TableBuilder val6 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				RotationX = UIBuilder.TextBox((VisualElement)this, "");
				RotationY = UIBuilder.TextBox((VisualElement)this, "");
				RotationZ = UIBuilder.TextBox((VisualElement)this, "");
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
			TableBuilder val7 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "Rel.Rot.:");
				RotationUpdate = UIBuilder.TextBox((VisualElement)this, "0 0 0");
			}
			finally
			{
				((IDisposable)val7)?.Dispose();
			}
			TableBuilder val8 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				VisualElement tmp = UIBuilder.HLayout((VisualElement)this);
				FwdUpdateRotation = UIBuilder.Button(tmp, "FWD");
				BwdUpdateRotation = UIBuilder.Button(tmp, "BWD");
				UIBuilder.Label((VisualElement)this, "Relative rotation");
			}
			finally
			{
				((IDisposable)val8)?.Dispose();
			}
			TableBuilder val9 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				VisualElement tmp2 = UIBuilder.HLayout((VisualElement)this);
				SyncRotationBtn = UIBuilder.Button(tmp2, "SYN");
				MirrorRotationBtn = UIBuilder.Button(tmp2, "MIR");
				UIBuilder.Label((VisualElement)this, "Synchronize or\nmirror opposite bone");
			}
			finally
			{
				((IDisposable)val9)?.Dispose();
			}
			TableBuilder val10 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				VisualElement tmp3 = UIBuilder.HLayout((VisualElement)this);
				SyncChildrenRotationBtn = UIBuilder.Button(tmp3, "TSYN");
				MirrorChildrenRotationBtn = UIBuilder.Button(tmp3, "TMIR");
				UIBuilder.Label((VisualElement)this, "Synchronize or\nmirror subtree");
			}
			finally
			{
				((IDisposable)val10)?.Dispose();
			}
			TableBuilder val11 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				VisualElement tmp4 = UIBuilder.HLayout((VisualElement)this);
				CopyTreeBtn = UIBuilder.Button(tmp4, "TCPY");
				PasteTreeBtn = UIBuilder.Button(tmp4, "TPST");
				UIBuilder.Label((VisualElement)this, "Copy/paste subtree");
			}
			finally
			{
				((IDisposable)val11)?.Dispose();
			}
			TableBuilder val12 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label((VisualElement)this, "");
				Button btn = UIBuilder.Button((VisualElement)this, "Hips Locked");
				btn.clicked += delegate
				{
					boneEditorHelperWindow.UnlockHips = !boneEditorHelperWindow.UnlockHips;
					btn.text = (boneEditorHelperWindow.UnlockHips ? "Hips Unlocked" : "Hips Locked");
				};
			}
			finally
			{
				((IDisposable)val12)?.Dispose();
			}
			TableBuilder val13 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
			try
			{
				PinEffector = UIBuilder.Button((VisualElement)this, "PIN");
				UIBuilder.Label((VisualElement)this, "Pin/Unpin IK effector");
			}
			finally
			{
				((IDisposable)val13)?.Dispose();
			}
			foreach (VisualElement c in Children())
			{
				if (c.childCount == 2)
				{
					c[0].style.width = 75f;
					c[1].style.flexGrow = 1f;
					continue;
				}
				foreach (VisualElement cc in c.Children())
				{
					cc.style.flexGrow = 1f;
				}
			}
			UIBuilder.Recursive<VisualElement>((VisualElement)this, (Action<VisualElement>)delegate(VisualElement e)
			{
				float num = 0f;
				e.style.marginBottom = 0f;
				e.style.marginTop = num;
				e.style.marginLeft = num;
				e.style.marginRight = 0f;
				e.style.paddingBottom = 0f;
				e.style.paddingTop = num;
				e.style.paddingLeft = num;
				e.style.paddingRight = 0f;
				e.style.fontSize = 12f;
				if (e is Label)
				{
					e.style.whiteSpace = WhiteSpace.Normal;
				}
			});
		}
	}

	private class HipIKHelper
	{
		private BonePanelService service;

		private bool recording;

		public HipIKHelper(BonePanelService service)
		{
			this.service = service;
		}

		public void Subscribe()
		{
			PositionHandle handle = service.skeletonEditor.runtimeSceneComponent.PositionHandle;
			handle.draggingPosition += PositionHandle_draggingPosition;
		}

		private void Handle_recordedTransformBegin(BaseHandle.BaseHandleRecordingEvent arg1, BaseHandle arg2)
		{
			recording = false;
			if (arg1.targets.Count <= 0 || !(arg1.targets[0].name != "CC_Base_Hip"))
			{
				recording = true;
				Record(arg1, "Leg.R");
				Record(arg1, "Leg.L");
			}
		}

		private void Handle_recordingTransformEnd(BaseHandle.BaseHandleRecordingEvent arg1, BaseHandle arg2)
		{
			if (recording)
			{
				Record(arg1, "Leg.R");
				Record(arg1, "Leg.L");
			}
		}

		private void Record(BaseHandle.BaseHandleRecordingEvent arg1, string v)
		{
			Transform t = service.skeletonEditor.skeletonActivos[0].transform;
			LimbIK legL = t.FindDeepChild(v).GetComponentInChildren<LimbIK>();
			arg1.Add(legL.solver.bone1.transform);
			arg1.Add(legL.solver.bone2.transform);
			arg1.Add(legL.solver.bone3.transform);
		}

		internal void Unsubscribe()
		{
			PositionHandle handle = service.skeletonEditor.runtimeSceneComponent.PositionHandle;
			handle.draggingPosition -= PositionHandle_draggingPosition;
		}

		private void PositionHandle_draggingPosition(Transform arg1, BaseHandle.BaseHandleTraslatingEvent arg2, BaseHandle arg3)
		{
			if (!(arg1.name != "CC_Base_Hip") && service.Window.UnlockHips)
			{
				arg2.Abort();
				UpdateLimbIK("Leg.L", "CC_Base_Knee.L", "CC_Base_Foot.L", arg2.offset, Quaternion.identity);
				UpdateLimbIK("Leg.R", "CC_Base_Knee.R", "CC_Base_Foot.R", arg2.offset, Quaternion.identity);
				arg1.position += arg2.offset;
			}
		}

		private void UpdateLimbIK(string libmikName, string bone1Name, string bone2Name, Vector3 offset, Quaternion rot)
		{
			Transform t = service.skeletonEditor.skeletonActivos[0].transform;
			LimbIK legL = t.FindDeepChild(libmikName).GetComponentInChildren<LimbIK>();
			Transform bone1 = service.rootBone.FindDeepChild(bone1Name);
			Transform bone2 = service.rootBone.FindDeepChild(bone2Name);
			legL.solver.bendGoal.SetPositionAndRotation(bone1.position - offset, bone1.rotation * Quaternion.Inverse(rot));
			legL.solver.target.SetPositionAndRotation(bone2.position - offset, bone2.rotation);
			((SolverManager)legL).FixTransformsTValle();
			((SolverManager)legL).UpdateSolverTValle();
		}
	}

	private BoneEditorHelperWindow Window;

	private RelIKTargeting ikTargeting;

	private SkeletonEditorMode skeletonEditor;

	private ScopeSupport editorScope;

	private ScopeSupport selectionScope;

	private bool restoreSelection;

	private Transform selected;

	private Transform rootBone;

	private Transform selectedOpposite;

	private Transform selectedAscending;

	private Transform selectedDescending;

	private HipIKHelper pelvisIK;

	private Tuple<string, Dictionary<string, (Vector3, Quaternion)>> subtreeCopy;

	public override void OnStart()
	{
		base.OnStart();
		pelvisIK = new HipIKHelper(this);
		Window = new BoneEditorHelperWindow();
		ikTargeting = Lookup<InteractionManager>().AnimationController.IKTargeting;
		Lookup<ExtendedPoseEditor>().AddTab("Bones", Window, base.Scope);
		skeletonEditor = Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales.Singleton<SkeletonEditorMode>.instance;
		skeletonEditor.onModoChanged += Instance_onModoChanged;
		Window.FwdUpdateRotation.clicked += delegate
		{
			UpdateRotation_OnClick(1f);
		};
		Window.BwdUpdateRotation.clicked += delegate
		{
			UpdateRotation_OnClick(-1f);
		};
		Window.SyncRotationBtn.clicked += SyncRotationBtn_OnClick;
		Window.MirrorRotationBtn.clicked += MirrorRotationBtn_OnClick;
		Window.SyncChildrenRotationBtn.clicked += SyncChildrenRotationBtn_OnClick;
		Window.MirrorChildrenRotationBtn.clicked += MirrorChildrenRotationBtn_OnClick;
		Window.CopyTreeBtn.clicked += CopyTreeBtn_OnClick;
		Window.PasteTreeBtn.clicked += PasteTreeBtn_OnClick;
		Window.ToAscending.clicked += ToAscending_OnClick;
		Window.ToDescending.clicked += ToDescending_OnClick;
		Window.ToOppositeBtn.clicked += ToOppositeBtn_OnClick;
		Window.PinEffector.clicked += PinEffector_OnClick;
	}

	private void PinEffector_OnClick()
	{
		if (!(selected != null))
		{
			return;
		}
		if (ikTargeting.GetIKTarget(selected) == null)
		{
			RelIKTargeting.IKTarget target = ikTargeting.GetImmediateIKTarget(selected);
			if (target == null)
			{
				base.Session.Modal.MessageError("Unable to resolve target");
				return;
			}
			ikTargeting.SetIKTarget(selected, target, 0f);
			Window.PinEffector.text = "UNPIN";
		}
		else
		{
			ikTargeting.SetIKTarget(selected, null, 0f);
			Window.PinEffector.text = "PIN";
		}
	}

	private void PasteTreeBtn_OnClick()
	{
		if (selected != null && selected.parent != null && subtreeCopy != null)
		{
			List<Transform> children = new List<Transform>();
			selected.ExecDeepChild(delegate(Transform t)
			{
				children.Add(t);
			});
			Dictionary<Transform, (Vector3, Quaternion)> translated = new Dictionary<Transform, (Vector3, Quaternion)>();
			Paste(children, selected.name.Contains("Root"));
		}
	}

	private void Paste(List<Transform> children, bool updateHipPos)
	{
		IRuntimeUndo undo = skeletonEditor.editor.Undo;
		undo.BeginRecord();
		try
		{
			foreach (Transform t in children)
			{
				if (subtreeCopy.Item2.TryGetValue(t.name, out var posrot))
				{
					if (t.name.Contains("Hip") && updateHipPos)
					{
						t.position = selected.parent.TransformPoint(posrot.Item1);
					}
					t.rotation = selected.parent.rotation * posrot.Item2;
				}
			}
		}
		finally
		{
			undo.EndRecord();
		}
	}

	private void CopyTreeBtn_OnClick()
	{
		if (!(selected != null) || !(selected.parent != null))
		{
			return;
		}
		List<Transform> children = new List<Transform>();
		selected.ExecDeepChild(delegate(Transform t)
		{
			children.Add(t);
		});
		subtreeCopy = new Tuple<string, Dictionary<string, (Vector3, Quaternion)>>(selected.parent.name, new Dictionary<string, (Vector3, Quaternion)>());
		foreach (Transform x in children)
		{
			subtreeCopy.Item2[x.name] = (selected.parent.InverseTransformPoint(x.position), Quaternion.Inverse(selected.parent.rotation) * x.rotation);
		}
	}

	private void ToDescending_OnClick()
	{
		if (selected != null && selectedDescending != null)
		{
			skeletonEditor.editor.Selection.activeGameObject = selectedDescending.gameObject;
		}
	}

	private void ToAscending_OnClick()
	{
		if (selected != null && selectedAscending != null)
		{
			skeletonEditor.editor.Selection.activeGameObject = selectedAscending.gameObject;
		}
	}

	private void MirrorChildrenRotationBtn_OnClick()
	{
		DeepUndoableOperationOnOpposite(delegate(Transform t, Transform ot)
		{
			Vector3 localEulerAngles = t.localEulerAngles;
			localEulerAngles.y = 0f - localEulerAngles.y;
			localEulerAngles.z = 0f - localEulerAngles.z;
			PerformUndoable(ot, ot.localEulerAngles, localEulerAngles, delegate(Transform x, Vector3 y)
			{
				x.transform.localEulerAngles = y;
			});
		});
	}

	private void SyncChildrenRotationBtn_OnClick()
	{
		DeepUndoableOperationOnOpposite(delegate(Transform t, Transform ot)
		{
			PerformUndoable(ot, ot.rotation, t.rotation, delegate(Transform x, Quaternion y)
			{
				x.rotation = y;
			});
		});
	}

	private void DeepUndoableOperationOnOpposite(Action<Transform, Transform> operation)
	{
		if (!(selected != null) || !(selectedOpposite != null))
		{
			return;
		}
		List<Transform> children = new List<Transform>();
		selected.ExecDeepChild(delegate(Transform item)
		{
			children.Add(item);
		});
		if (children.Count == 0)
		{
			return;
		}
		IRuntimeUndo undo = skeletonEditor.editor.Undo;
		undo.BeginRecord();
		try
		{
			foreach (Transform t in children)
			{
				Transform ot = GetOppositeTransform(t);
				if (!(ot == null))
				{
					operation(t, ot);
				}
			}
		}
		finally
		{
			undo.EndRecord();
		}
	}

	private void ToOppositeBtn_OnClick()
	{
		if (selectedOpposite != null)
		{
			skeletonEditor.editor.Selection.activeGameObject = selectedOpposite.gameObject;
		}
	}

	private void MirrorRotationBtn_OnClick()
	{
		if (selected != null)
		{
			if (selectedOpposite != null)
			{
				Vector3 eu = selected.localEulerAngles;
				eu.y = 0f - eu.y;
				eu.z = 0f - eu.z;
				PerformUndoable(selectedOpposite, selectedOpposite.localEulerAngles, eu, delegate(Transform x, Vector3 y)
				{
					x.transform.localEulerAngles = y;
				});
			}
			else
			{
				logger.Error("No opposite bone found");
			}
		}
		else
		{
			logger.Error("No bone selected");
		}
	}

	private void SyncRotationBtn_OnClick()
	{
		if (selected != null)
		{
			if (selectedOpposite != null)
			{
				PerformUndoable(selectedOpposite, selectedOpposite.rotation, selected.transform.rotation, delegate(Transform x, Quaternion y)
				{
					x.rotation = y;
				});
			}
			else
			{
				logger.Error("No opposite bone found");
			}
		}
		else
		{
			logger.Error("No bone selected");
		}
	}

	private Transform GetOppositeTransform()
	{
		return GetOppositeTransform(selected);
	}

	private Transform GetOppositeTransform(Transform t)
	{
		if (t == null)
		{
			return null;
		}
		string name = null;
		if (t.name.EndsWith(".R"))
		{
			name = t.name.Substring(0, t.name.Length - 2) + ".L";
		}
		else if (selected.name.EndsWith(".L"))
		{
			name = t.name.Substring(0, t.name.Length - 2) + ".R";
		}
		if (name == null)
		{
			return null;
		}
		if (rootBone == null)
		{
			return null;
		}
		return rootBone.FindDeepChild(name);
	}

	private void UpdateRotation_OnClick(float dir)
	{
		if (!(selected != null))
		{
			return;
		}
		ExposeToEditor node = selected.GetComponent<ExposeToEditor>();
		if (!(node != null))
		{
			return;
		}
		string text = Window.RotationUpdate.value;
		Vector3 vec = Parse(text) * dir;
		if (vec == Vector3.zero)
		{
			Window.RotationUpdate.value = "0 0 0";
			return;
		}
		Quaternion quat = Quaternion.Euler(vec);
		PerformUndoable(node.transform, node.transform.rotation, node.transform.rotation * quat, delegate(Transform t, Quaternion x)
		{
			t.rotation = x;
		});
		restoreSelection = true;
	}

	private void PerformUndoable<K, T>(K instance, T prev, T value, Action<K, T> action)
	{
		IRuntimeUndo undo = skeletonEditor.editor.Undo;
		undo.CreateRecord(delegate
		{
			action(instance, value);
			return true;
		}, delegate
		{
			action(instance, prev);
			return true;
		});
		action(instance, value);
	}

	private Vector3 Parse(string text)
	{
		string[] parts = text.Split(new char[1] { ' ' });
		if (parts.Length != 3)
		{
			return Vector3.zero;
		}
		if (!float.TryParse(parts[0], out var x))
		{
			return Vector3.zero;
		}
		if (!float.TryParse(parts[1], out var y))
		{
			return Vector3.zero;
		}
		if (!float.TryParse(parts[2], out var z))
		{
			return Vector3.zero;
		}
		return new Vector3(x, y, z);
	}

	private void Instance_onModoChanged(SkeletonEditorMode obj)
	{
		if (skeletonEditor.activado)
		{
			SetSelectedGo(null);
			editorScope = new ScopeSupport
			{
				Silent = true
			};
			editorScope.EventHandler(delegate(RuntimeSelectionChanged h)
			{
				skeletonEditor.editor.Selection.SelectionChanged += h;
			}, delegate(RuntimeSelectionChanged h)
			{
				skeletonEditor.editor.Selection.SelectionChanged -= h;
			}, OnSelectedBoneChanged);
			pelvisIK.Subscribe();
		}
		else if (editorScope != null)
		{
			editorScope.Dispose();
			editorScope = null;
			SetSelectedGo(null);
			pelvisIK.Unsubscribe();
		}
	}

	private void OnObjectTransformChanged(ExposeToEditor obj)
	{
		if (selected != null && obj.gameObject.transform == selected)
		{
			SetSelectedGo(selected);
		}
	}

	private void OnSelectedBoneChanged(UnityEngine.Object[] unselectedObjects)
	{
		IRuntimeSelection selection = skeletonEditor.editor.Selection;
		GameObject go = selection.activeGameObject;
		if (go == null && restoreSelection)
		{
			skeletonEditor.editor.Undo.Undo();
			restoreSelection = false;
			return;
		}
		if (selectionScope != null)
		{
			selectionScope.Dispose();
			selectionScope = null;
		}
		if (go != null)
		{
			selectionScope = new ScopeSupport
			{
				Silent = true
			};
			editorScope.AddChild(selectionScope);
			editorScope.EventHandler(delegate(ObjectEvent h)
			{
				skeletonEditor.editor.Object.TransformChanged += h;
			}, delegate(ObjectEvent h)
			{
				skeletonEditor.editor.Object.TransformChanged -= h;
			}, OnObjectTransformChanged);
			SetSelectedGo(go.transform);
		}
		else if (selected != null && skeletonEditor.editor.Undo.CanUndo)
		{
			skeletonEditor.editor.Undo.Undo();
		}
		else
		{
			SetSelectedGo(null);
		}
	}

	private Transform ResolveRoot()
	{
		if (selected != null)
		{
			return selected.FindDeepParent("CC_Base_BoneRoot");
		}
		return null;
	}

	private void SetSelectedGo(Transform go)
	{
		selected = go;
		rootBone = ResolveRoot();
		selectedOpposite = GetOppositeTransform();
		selectedAscending = null;
		if (selected != null && selected.parent != null)
		{
			ExposeToEditor pt = selected.parent.GetComponentInParent<ExposeToEditor>();
			if (pt != null)
			{
				selectedAscending = pt.transform;
			}
		}
		selectedDescending = null;
		if (selected != null)
		{
			List<Transform> ch = new List<Transform>();
			foreach (Transform t in selected)
			{
				if (t.GetComponent<ExposeToEditor>() != null)
				{
					ch.Add(t);
				}
			}
			if (ch.Count == 1)
			{
				selectedDescending = ch[0];
			}
		}
		if (go == null)
		{
			Window.BoneName.value = "<None>";
		}
		else
		{
			ExposeToEditor editable = go.GetComponent<ExposeToEditor>();
			Window.BoneName.value = go.name;
			Window.PositionX.value = go.position.x.ToString();
			Window.PositionY.value = go.position.y.ToString();
			Window.PositionZ.value = go.position.z.ToString();
			if (editable != null)
			{
				Vector3 rot = editable.transform.eulerAngles;
				UnityUtils.NormalizeEuler(ref rot);
				Window.RotationX.value = rot.x.ToString();
				Window.RotationY.value = rot.y.ToString();
				Window.RotationZ.value = rot.z.ToString();
			}
		}
		UIBuilder.SetVisible((VisualElement)Window.ToAscending, selectedAscending != null);
		UIBuilder.SetVisible((VisualElement)Window.ToDescending, selectedDescending != null);
		UIBuilder.SetVisible((VisualElement)Window.ToOppositeBtn, selectedOpposite != null);
		UIBuilder.SetVisible((VisualElement)Window.FwdUpdateRotation, selected != null);
		UIBuilder.SetVisible((VisualElement)Window.BwdUpdateRotation, selected != null);
		UIBuilder.SetVisible((VisualElement)Window.MirrorRotationBtn, selectedOpposite != null);
		UIBuilder.SetVisible((VisualElement)Window.SyncRotationBtn, selectedOpposite != null);
		UIBuilder.SetVisible((VisualElement)Window.MirrorChildrenRotationBtn, selectedOpposite != null);
		UIBuilder.SetVisible((VisualElement)Window.SyncChildrenRotationBtn, selectedOpposite != null);
		UIBuilder.SetVisible((VisualElement)Window.CopyTreeBtn, selected != null && selected.parent != null);
		UIBuilder.SetVisible((VisualElement)Window.PasteTreeBtn, UIBuilder.IsVisible((VisualElement)Window.CopyTreeBtn) && subtreeCopy != null && subtreeCopy.Item1 == selected.parent.name);
		UIBuilder.SetVisible((VisualElement)Window.PinEffector, selected != null && (selected.name == "CC_Base_Hand.L" || selected.name == "CC_Base_Hand.R"));
		if (UIBuilder.IsVisible((VisualElement)Window.PinEffector))
		{
			if (ikTargeting.GetIKTarget(selected) == null)
			{
				Window.PinEffector.text = "PIN";
			}
			else
			{
				Window.PinEffector.text = "UNPIN";
			}
		}
	}
}
