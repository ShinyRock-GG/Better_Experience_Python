using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using BetterExperience.Utils;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class PosturePanelService : SessionService
{
	private class PostureTree : TreeView<Posture>
	{
		public class PostureModel : TreeViewModel<Posture>
		{
			private PoseManager poseManager;

			public Observable<Posture> ApplyPosture { get; } = new Observable<Posture>();

			public Observable<Posture> RemovePosture { get; } = new Observable<Posture>();

			public Observable<Posture> NewPoiPose { get; } = new Observable<Posture>();

			public Observable<POIPosture> RemovePoiPosture { get; } = new Observable<POIPosture>();

			public Observable<POIPosture> ApplyPoiPosture { get; } = new Observable<POIPosture>();

			public Observable<POIPosture> UpdatePoiPosture { get; } = new Observable<POIPosture>();

			public Observable<Posture> EditPostureDescriptor { get; } = new Observable<Posture>();

			public bool ShortNames { get; set; }

			public PostureModel(PoseManager poseManager, ScopeSupport scope)
			{
				this.poseManager = poseManager;
				poseManager.PosturesChanged.Add(ReloadTree, scope);
				poseManager.POIPosturesChanged.Add(ReloadTree, scope);
			}

			public override TreeViewItem CreateItem(Posture item)
			{
				TreeViewItem tvi = new TreeViewItem();
				AddPosture(tvi.Item, item);
				return tvi;
			}

			public override List<Posture> GetChildren(Posture parent)
			{
				if (parent == null)
				{
					return poseManager.Postures.Values.OrderBy((Posture x) => x.Id).ToList();
				}
				List<Posture> ps = new List<Posture>();
				poseManager.POIPostures.Values.ForEach(delegate(POIPostureCollection pps)
				{
					foreach (POIPosture current in pps.ExactPostures.Values)
					{
						if (current.Poses.Posture == parent)
						{
							ps.Add(current);
						}
					}
				});
				return ps.OrderBy((Posture x) => x.Id).ToList();
			}

			internal void AddPosture(VisualElement grid, Posture posture)
			{
				TableBuilder val = UIBuilder.Row(grid, (Action<VisualElement>)Style);
				try
				{
					Button btn = UIBuilder.Button(grid, "");
					btn.style.width = 13f;
					btn.style.height = 13f;
					btn.clicked += delegate
					{
						GenericDropdownMenu genericDropdownMenu = new GenericDropdownMenu();
						POIPosture pp = posture as POIPosture;
						if (pp != null)
						{
							genericDropdownMenu.AddItem("Apply posture", isChecked: false, delegate
							{
								ApplyPoiPosture.Invoke(pp);
							});
							genericDropdownMenu.AddItem("Update posture", isChecked: false, delegate
							{
								UpdatePoiPosture.Invoke(pp);
							});
							genericDropdownMenu.AddItem("Remove posture", isChecked: false, delegate
							{
								RemovePoiPosture.Invoke(pp);
							});
							genericDropdownMenu.AddSeparator("");
							genericDropdownMenu.AddItem("Edit descriptor", isChecked: false, delegate
							{
								EditPostureDescriptor.Invoke(posture);
							});
						}
						else
						{
							genericDropdownMenu.AddItem("Apply posture", isChecked: false, delegate
							{
								ApplyPosture.Invoke(posture);
							});
							genericDropdownMenu.AddItem("Implement posture", isChecked: false, delegate
							{
								NewPoiPose.Invoke(posture);
							});
							genericDropdownMenu.AddItem("Remove posture", isChecked: false, delegate
							{
								RemovePosture.Invoke(posture);
							});
							genericDropdownMenu.AddSeparator("");
							genericDropdownMenu.AddItem("Edit descriptor", isChecked: false, delegate
							{
								EditPostureDescriptor.Invoke(posture);
							});
						}
						genericDropdownMenu.DropDown(btn.worldBound, btn);
					};
					if (ShortNames && posture is POIPosture)
					{
						UIBuilder.Label(grid, posture.Id.Substring(posture.Id.IndexOf('.') + 1));
					}
					else
					{
						UIBuilder.Label(grid, posture.Id);
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
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

			public void Invalidate()
			{
				ReloadTree();
			}
		}

		public PostureTree(PostureModel model)
			: base((TreeViewModel<Posture>)model)
		{
		}
	}

	private class PosturePane : VisualElement
	{
		public Button NewPostureBtn { get; }

		public Button QuickSettings { get; }

		public PostureTree.PostureModel model { get; private set; }

		public Observable<Posture> ApplyPosture => model.ApplyPosture;

		public Observable<Posture> RemovePosture => model.RemovePosture;

		public Observable<Posture> NewPoiPose => model.NewPoiPose;

		public Observable<POIPosture> RemovePoiPosture => model.RemovePoiPosture;

		public Observable<POIPosture> ApplyPoiPosture => model.ApplyPoiPosture;

		public Observable<POIPosture> UpdatePoiPosture => model.UpdatePoiPosture;

		public Observable<Posture> EditPostureDescriptor => model.EditPostureDescriptor;

		public PosturePane(PostureTree.PostureModel model)
		{
			VisualElement seq = UIBuilder.HLayout((VisualElement)this);
			QuickSettings = UIBuilder.StylePadding<Button>(UIBuilder.StyleMargin<Button>(UIBuilder.StyleAlign<Button>(UIBuilder.StyleHeight<Button>(UIBuilder.StyleWidth<Button>(UIBuilder.Button(seq, ""), 15), 15), Align.Center), 0), 0);
			UIBuilder.Label(seq, "Posture");
			NewPostureBtn = UIBuilder.Button(seq, "New Posture");
			NewPostureBtn.style.fontSize = 12f;
			NewPostureBtn.style.paddingTop = 0f;
			NewPostureBtn.style.paddingBottom = 0f;
			this.model = model;
			PostureTree tree = new PostureTree(model);
			UIBuilder.AddElement<PostureTree>((VisualElement)UIBuilder.AddElement<ScrollView>((VisualElement)this, new ScrollView()), tree);
		}
	}

	private class PostureDescriptorEditor : VisualElement
	{
		private PostureDescriptor model;

		private TextField postureId;

		private TextField displayName;

		private TextField cancelDisplayName;

		private DropdownField orientation;

		private DropdownField parentPosture;

		public event Action Close = delegate
		{
		};

		public event Action Save = delegate
		{
		};

		public PostureDescriptorEditor()
		{
			VisualElement seq = UIBuilder.VLayout((VisualElement)this);
			TableBuilder val = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(seq, "Posture ID");
				postureId = UIBuilder.TextBox(seq, "");
				postureId.SetEnabled(value: false);
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(seq, "Display Name");
				displayName = UIBuilder.TextBox(seq, "");
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(seq, "Cancellation Name");
				cancelDisplayName = UIBuilder.TextBox(seq, "");
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			TableBuilder val4 = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(seq, "Orientation");
				seq.Add(orientation = new DropdownField());
				orientation.choices = new string[3] { "UNIVERSAL", "FRONT", "BACK" }.ToList();
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			TableBuilder val5 = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(seq, "Parent Posture");
				seq.Add(parentPosture = new DropdownField());
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
			UIBuilder.LayoutColSizes(seq, new int[2] { 150, 300 });
			TableBuilder val6 = UIBuilder.Row(seq, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Button(seq, "Save").clicked += PostureDescriptorEditor_Save_clicked;
				UIBuilder.Button(seq, "Cancel").clicked += delegate
				{
					this.Close();
				};
			}
			finally
			{
				((IDisposable)val6)?.Dispose();
			}
		}

		private void PostureDescriptorEditor_Save_clicked()
		{
			model.DisplayName = displayName.value.TrimToNull();
			model.CancelDisplayName = cancelDisplayName.value.TrimToNull();
			if (Enum.TryParse<PoseOrientation>(orientation.value, out var _orientation))
			{
				model.Orientation = _orientation;
			}
			if (parentPosture.index == 0)
			{
				model.ParentPosture = null;
			}
			else
			{
				model.ParentPosture = parentPosture.value;
			}
			this.Save();
		}

		public void SetPostures(List<Posture> postures)
		{
			List<string> pids = new List<string>();
			pids.Add("[None]");
			postures.ForEach(delegate(Posture p)
			{
				pids.Add(p.Id);
			});
			parentPosture.choices = pids;
		}

		public void SetModel(PostureDescriptor descriptor)
		{
			model = descriptor;
			AfterModelSet();
		}

		private void AfterModelSet()
		{
			postureId.value = model.Id;
			displayName.value = model.DisplayName;
			cancelDisplayName.value = model.CancelDisplayName;
			int i = orientation.choices.IndexOf(model.Orientation.ToString());
			if (i == -1)
			{
				orientation.index = 0;
			}
			else
			{
				orientation.index = i;
			}
			if (model.ParentPosture == null)
			{
				parentPosture.index = 0;
				return;
			}
			i = parentPosture.choices.IndexOf(model.ParentPosture);
			if (i == -1)
			{
				parentPosture.index = 0;
			}
			else
			{
				parentPosture.index = i;
			}
		}
	}

	private POIManager poiManager;

	private PoseManager poseManager;

	private InteractionManager interactionManager;

	private UITKManagedPanel rootUI;

	private ConfigEntry<bool> shortNames;

	private PosturePane Window { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		poseManager = Lookup<PoseManager>();
		interactionManager = Lookup<InteractionManager>();
		poiManager = Lookup<POIManager>();
		Window = new PosturePane(new PostureTree.PostureModel(poseManager, base.Scope));
		Window.NewPostureBtn.clicked += NewPostureBtn_OnClick;
		Window.QuickSettings.clicked += QuickSettings_clicked;
		Window.NewPoiPose.Add(OnPoiPose, base.Scope);
		Window.RemovePosture.Add(OnRemovePosture, base.Scope);
		Window.ApplyPosture.Add(OnLoadPosture, base.Scope);
		Window.ApplyPoiPosture.Add(OnApplyPoiPosture, base.Scope);
		Window.RemovePoiPosture.Add(OnRemovePoiPosture, base.Scope);
		Window.UpdatePoiPosture.Add(OnUpdatePoiPosture, base.Scope);
		Window.EditPostureDescriptor.Add(OnEditPostureDescriptor, base.Scope);
		Lookup<ExtendedPoseEditor>().AddTab("Postures", Window, base.Scope);
		CustomSceneFeature feature = Lookup<CustomSceneFeature>();
		rootUI = feature.EditorUiPanel;
		shortNames = feature.PluginConfig.Bind<bool>("Editor.Postures", "ShortNames", false, (ConfigDescription)null);
		shortNames.SettingChanged += delegate
		{
			Window.model.ShortNames = shortNames.Value;
			Window.model.Invalidate();
		};
		Window.model.ShortNames = shortNames.Value;
		Window.model.Invalidate();
	}

	private void QuickSettings_clicked()
	{
		GenericDropdownMenu gdm = new GenericDropdownMenu();
		gdm.AddItem("Short names", shortNames.Value, delegate
		{
			shortNames.Value = !shortNames.Value;
		});
		gdm.DropDown(Window.QuickSettings.worldBound, Window.QuickSettings);
	}

	private void OnUpdatePoiPosture(POIPosture posture)
	{
		base.Session.Modal.MessageBoxYesNo($"Are you sure you want to move {posture.Id}?").OnResult += delegate(bool result)
		{
			if (result)
			{
				interactionManager.MovePosture(posture);
			}
		};
	}

	private void OnRemovePoiPosture(POIPosture posture)
	{
		base.Session.Modal.MessageBoxYesNo($"Are you sure you want to delete {posture.Id}?").OnResult += delegate(bool result)
		{
			if (result)
			{
				poseManager.RemovePosture(posture);
			}
		};
	}

	private void OnApplyPoiPosture(POIPosture obj)
	{
		PointOfInterest poi = poiManager.FindPOI(obj.PoiId);
		if (poi == null)
		{
			logger.Error("Poi {0}/{1} not found", obj.PoiId, obj.Id);
		}
		else if (interactionManager.CurrentPlace.POI != poi)
		{
			base.Session.Modal.MessageBoxYesNo("You've reqested to goto " + poi.Id + ". Continue?").OnResult += delegate(bool result)
			{
				if (result)
				{
					interactionManager.GoTo(poi);
					interactionManager.SetPosture(obj);
					interactionManager.StartAnimation("Binding");
				}
			};
		}
		else
		{
			interactionManager.SetPosture(obj);
			interactionManager.StartAnimation("Binding");
		}
	}

	private void NewPostureBtn_OnClick()
	{
		base.Session.Modal.RequestInput("Type posture name", "Place").OnResult += delegate(string name)
		{
			if (name != null)
			{
				if (poseManager.Postures.ContainsKey(name))
				{
					base.Session.Modal.MessageError($"Posture named {name} already exists");
				}
				else
				{
					interactionManager.CreatePostureNow(name);
				}
			}
		};
	}

	private void OnRemovePosture(Posture posture)
	{
		base.Session.Modal.MessageBoxYesNo($"Are you sure you want to delete {posture.Id}?").OnResult += delegate(bool result)
		{
			if (result)
			{
				poseManager.RemovePosture(posture);
			}
		};
	}

	private void OnPoiPose(Posture posture)
	{
		PointOfInterest poi = interactionManager.CurrentPlace.POI;
		string poiPoseName = posture.Id + "." + poi.Id;
		base.Session.Modal.RequestInput("Type interaction name for POI posture " + poiPoseName + ".<>", "<" + posture.Id + " here>").OnResult += delegate(string name)
		{
			if (name != null)
			{
				if (poseManager.Postures.ContainsKey(name))
				{
					base.Session.Modal.MessageError($"Posture named {name} already exists");
				}
				else
				{
					interactionManager.CreatePOIPostureNow(poi, posture, name);
				}
			}
		};
	}

	private void OnEditPostureDescriptor(Posture obj)
	{
		PostureDescriptorEditor pde = new PostureDescriptorEditor();
		if (obj is POIPosture poip)
		{
			if (poseManager.POIPostures.TryGetValue(poip.PoiId, out var coll))
			{
				List<Posture> ps = coll.ExactPostures.Values.Where((POIPosture p) => p != obj).Select((Func<POIPosture, Posture>)((POIPosture p) => p)).ToList();
				pde.SetPostures(ps);
			}
			else
			{
				logger.Error("Unable to resolve poi postures at {0}", poip.PoiId);
				pde.SetPostures(new List<Posture>());
			}
		}
		else
		{
			List<Posture> ps2 = (from p in poseManager.Postures.Values
				where p != obj
				select (p)).ToList();
			pde.SetPostures(ps2);
		}
		pde.SetModel(obj.Descriptor);
		Action close = PopupWindowEx.ShowModal(pde, 500, 300, rootUI.GameView);
		pde.Close += close;
		pde.Save += delegate
		{
			poseManager.SaveDescriptor(obj.Descriptor);
			close();
		};
	}

	private void OnLoadPosture(Posture posture)
	{
		PoseAnimationClip postureclip = posture.Poses.PostureClip;
		interactionManager.AnimationController.StartAnimation(postureclip);
	}
}
