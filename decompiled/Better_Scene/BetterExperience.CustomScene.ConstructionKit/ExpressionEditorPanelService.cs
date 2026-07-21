using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Base.Bones.Gizmos.Runtime;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using BetterExperience.Wrappers.Characters;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class ExpressionEditorPanelService : SessionService
{
	private class GesturesHelperWindow : VisualElement
	{
		private Toggle eyeExpressions;

		private GesturesWeights model;

		private List<Action> readers = new List<Action>();

		private List<Action> writers = new List<Action>();

		public bool enabled;

		public GesturesHelperWindow(GesturesWeights model, bool shapeKeys)
		{
			//IL_0227: Unknown result type (might be due to invalid IL or missing references)
			//IL_0231: Expected O, but got Unknown
			this.model = model;
			style.flexDirection = FlexDirection.Column;
			int toggleW = 100;
			int dropdownW = 200;
			if (!shapeKeys)
			{
				TableBuilder val = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
				try
				{
					eyeExpressions = UIBuilder.Toggle((VisualElement)this, "", false);
					UIBuilder.StyleAlign<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, "Eyes"), toggleW), Align.Center);
					CreateMapping(eyeExpressions, () => model.EyesOverride, delegate(bool value)
					{
						model.EyesOverride = value;
					});
					CreateEnumWeightTable2<EyeExpressionType>((VisualElement)this, model.EyeExpressions, dropdownW);
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
				TableBuilder val2 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
				try
				{
					Toggle mouthGestures = UIBuilder.Toggle((VisualElement)this, "", false);
					UIBuilder.StyleAlign<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, "Mouth"), toggleW), Align.Center);
					CreateMapping(mouthGestures, () => model.MouthOverride, delegate(bool value)
					{
						model.MouthOverride = value;
					});
					CreateEnumWeightTable2<MouthGesture>((VisualElement)this, model.MouthExpression, dropdownW);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				TableBuilder val3 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
				try
				{
					Toggle faceGestures = UIBuilder.Toggle((VisualElement)this, "", false);
					UIBuilder.StyleAlign<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, "Face"), toggleW), Align.Center);
					CreateMapping(faceGestures, () => model.FaceOverride, delegate(bool value)
					{
						model.FaceOverride = value;
					});
					CreateEnumWeightTable2<FaceExpressionType>((VisualElement)this, model.FaceExpression, dropdownW);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			else
			{
				ObservableValue<string> filter = new ObservableValue<string>("");
				ObservableValue<bool> usedOnly = new ObservableValue<bool>(false);
				TableBuilder val4 = UIBuilder.Row((VisualElement)this, (Action<VisualElement>)null);
				try
				{
					Toggle faceBlendShapes = UIBuilder.Toggle((VisualElement)this, "", false);
					UIBuilder.StyleAlign<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)this, "Blend Shapes"), toggleW), Align.Center);
					CreateMapping(faceBlendShapes, () => model.FaceBlendShapesOverride, delegate(bool value)
					{
						model.FaceBlendShapesOverride = value;
					});
					FilterTextField nameFilter = UIBuilder.StyleWidth<FilterTextField>(UIBuilder.AddElement<FilterTextField>((VisualElement)this, new FilterTextField()), 200);
					nameFilter.ValueChanged.Add(delegate(string newFilter)
					{
						filter.Value = newFilter;
					});
					CheckBox usedOnlyToggle = UIBuilder.AddElement<CheckBox>((VisualElement)this, new CheckBox("Used-only"));
					usedOnlyToggle.RegisterValueChangedCallback(delegate(ChangeEvent<bool> value)
					{
						usedOnly.Value = value.newValue;
					});
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				CreateBlendShapes(this, filter, usedOnly);
			}
			UIBuilder.Recursive<VisualElement>((VisualElement)this, (Action<VisualElement>)delegate(VisualElement e)
			{
				float num = 1f;
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
			model.Changed.Add(ReadModel);
		}

		private void CreateBlendShapes(GesturesHelperWindow layout, ObservableValue<string> filter, ObservableValue<bool> usedOnly)
		{
			List<string[]> sortedIndex = new List<string[]>();
			Dictionary<string, int> keyIndex = new Dictionary<string, int>();
			for (int i = 0; i < model.FaceBlendShapes.Keys.Length; i++)
			{
				string key = model.FaceBlendShapes.Keys[i];
				keyIndex[key] = i;
				string title = key;
				title = title.Split(new string[1] { "$$" }, StringSplitOptions.None)[0];
				sortedIndex.Add(new string[2] { title, key });
			}
			sortedIndex = sortedIndex.OrderBy((string[] x) => x[0]).ToList();
			for (int i2 = 0; i2 < model.FaceBlendShapes.Keys.Length; i2++)
			{
				string[] kt = sortedIndex[i2];
				string title2 = kt[0];
				string strKey = kt[1];
				int key2 = keyIndex[strKey];
				TableBuilder val = UIBuilder.Row((VisualElement)layout, (Action<VisualElement>)null);
				try
				{
					UIBuilder.StyleWidth<Label>(UIBuilder.Label((VisualElement)layout, title2), 320);
					Slider slider = UIBuilder.StyleFlexGrow<Slider>(UIBuilder.Slider((VisualElement)layout, 0f, 1f), 1f);
					if (strKey == "Tongue_Out$$RL_11$$")
					{
						slider.highValue = 10f;
					}
					slider.showInputField = true;
					Action writer = delegate
					{
						float value = slider.value;
						model.FaceBlendShapes[key2] = value;
						model.Dirty = true;
					};
					writers.Add(writer);
					slider.RegisterValueChangedCallback(delegate
					{
						writer();
					});
					slider.Q<TextField>().RegisterValueChangedCallback(delegate
					{
						writer();
					});
					Action reader = delegate
					{
						float num = model.FaceBlendShapes[key2];
						slider.SetValueWithoutNotify(num);
						bool flag = strKey.ToLower().Contains(filter.Value.ToLower());
						flag &= !usedOnly.Value || num > 0f;
						UIBuilder.SetVisible(slider.parent, flag);
					};
					readers.Add(reader);
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			((BaseObservable<Action<string>>)(object)filter).Add((Action<string>)delegate
			{
				ReadModel();
			});
			((BaseObservable<Action<bool>>)(object)usedOnly).Add((Action<bool>)delegate
			{
				ReadModel();
			});
		}

		private void CreateEnumWeightTable2<T>(VisualElement layout, EnumWeightArray<T> eyeExpr, int dropdownW) where T : Enum, IConvertible
		{
			DropdownBox choices = UIBuilder.AddElement<DropdownBox>(layout, new DropdownBox(eyeExpr.Keys.Select((T x) => x.ToString()).ToArray()));
			UIBuilder.StyleWidth<DropdownBox>(choices, dropdownW);
			Slider slider = UIBuilder.StyleFlexGrow<Slider>(UIBuilder.Slider(layout, 0f, 1f), 1f);
			slider.showInputField = true;
			Label label = UIBuilder.StyleAlign<Label>(UIBuilder.StyleWidth<Label>(UIBuilder.Label(layout, "0"), 50), Align.Center);
			UIBuilder.SetVisible((VisualElement)label, false);
			Action writer = delegate
			{
				int num = choices.Items.IndexOf(choices.SelectedItem);
				float value = slider.value;
				if (num > -1)
				{
					for (int i = 0; i < eyeExpr.Keys.Length; i++)
					{
						if (i == num)
						{
							eyeExpr[i] = value;
						}
						else
						{
							eyeExpr[i] = 0f;
						}
					}
				}
				label.text = value.ToString();
				model.Dirty = true;
			};
			writers.Add(writer);
			slider.RegisterValueChangedCallback(delegate
			{
				writer();
			});
			slider.Q<TextField>().RegisterValueChangedCallback(delegate
			{
				writer();
			});
			choices.SelectedIndexChanged.Add(delegate
			{
				writer();
			});
			Action reader = delegate
			{
				T val = eyeExpr.MaxKey();
				float num = eyeExpr[val];
				int index = Array.IndexOf(eyeExpr.Keys, val);
				string valueWithoutNotify = choices.Items[index];
				if (num > 0f)
				{
					choices.SetValueWithoutNotify(valueWithoutNotify);
				}
				slider.SetValueWithoutNotify(num);
				label.text = num.ToString();
			};
			readers.Add(reader);
		}

		private void CreateMapping(Toggle t, Func<bool> reader, Action<bool> writer)
		{
			Action<bool> wrapper = delegate(bool x)
			{
				writer(x);
				model.Dirty = true;
			};
			t.RegisterValueChangedCallback(delegate
			{
				wrapper(t.value);
			});
			readers.Add(delegate
			{
				t.SetValueWithoutNotify(reader());
			});
			writers.Add(delegate
			{
				wrapper(t.value);
			});
		}

		public void WriteModel()
		{
			foreach (Action w in writers)
			{
				w();
			}
		}

		public void ReadModel()
		{
			if (!enabled)
			{
				return;
			}
			foreach (Action r in readers)
			{
				r();
			}
		}
	}

	private GesturesHelperWindow Window;

	private GesturesHelperWindow BlendShapesWindow;

	public override void OnStart()
	{
		base.OnStart();
		GesturesWeights gestures = Lookup<InteractionManager>().AnimationController.Gestures;
		Window = new GesturesHelperWindow(gestures, shapeKeys: false);
		BlendShapesWindow = new GesturesHelperWindow(gestures, shapeKeys: true);
		ClipEditorPanelService clipEditorService = Lookup<ClipEditorPanelService>();
		clipEditorService.RegisterTool("Expressions", Window, base.Scope);
		clipEditorService.RegisterTool("Face Blend Shapes", BlendShapesWindow, base.Scope);
		Lookup<ExtendedPoseEditor>().OnStateChanged.Add(delegate(SkeletonEditorMode mode)
		{
			Window.enabled = mode != null;
		}, base.Scope);
	}
}
