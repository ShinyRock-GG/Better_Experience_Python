using System;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Poser;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using static BetterExperience.UI.UIBuilder;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.CustomScene.ConstructionKit;

internal class PointOfInterestPanelService : SessionService
{
	private class POITree : TreeView<PointOfInterest>
	{
		public class POITreeModel : TreeViewModel<PointOfInterest>
		{
			private POIManager manager;

			public Observable<string> GoTo { get; } = new Observable<string>();

			public Observable<string> Update { get; } = new Observable<string>();

			public Observable<string> RemovePOI { get; } = new Observable<string>();

			public Observable<string> EditDescriptor { get; } = new Observable<string>();

			public Observable<string> NewChildPoi { get; } = new Observable<string>();

			public POITreeModel(POIManager manager, ScopeSupport scope)
			{
				this.manager = manager;
				manager.RegisteredPlacesChanged.Add(ReloadTree, scope);
			}

			public override TreeViewItem CreateItem(PointOfInterest item)
			{
				TreeViewItem viewItem = new TreeViewItem();
				AddPoint(viewItem.Item, item.Id);
				return viewItem;
			}

			public override List<PointOfInterest> GetChildren(PointOfInterest parent)
			{
				if (parent == null)
				{
					return (from x in manager.Points
						where x.Desc == null || x.Desc.ParentPoi == null
						orderby x.Id
						select x).ToList();
				}
				return (from x in manager.Points
					where x.Desc != null && x.Desc.ParentPoi == parent.Id
					orderby x.Id
					select x).ToList();
			}

			internal void AddPoint(VisualElement grid, string name)
			{
				TableBuilder val = UIBuilder.Row(grid, (Action<VisualElement>)Style);
				try
				{
					Button btn = UIBuilder.Button(grid, "");
					btn.style.width = 13f;
					btn.style.height = 13f;
					Action action = delegate
					{
						GenericDropdownMenu genericDropdownMenu = new GenericDropdownMenu();
						genericDropdownMenu.AddItem("Go to", isChecked: false, delegate
						{
							GoTo.Invoke(name);
						});
						genericDropdownMenu.AddItem("Update", isChecked: false, delegate
						{
							Update.Invoke(name);
						});
						genericDropdownMenu.AddItem("Remove", isChecked: false, delegate
						{
							RemovePOI.Invoke(name);
						});
						genericDropdownMenu.AddSeparator("");
						genericDropdownMenu.AddItem("Edit descriptor", isChecked: false, delegate
						{
							EditDescriptor.Invoke(name);
						});
						genericDropdownMenu.AddSeparator("");
						genericDropdownMenu.AddItem("Create child POI", isChecked: false, delegate
						{
							NewChildPoi.Invoke(name);
						});
						genericDropdownMenu.DropDown(btn.worldBound, btn);
					};
					btn.clicked += action;
					Label label = UIBuilder.Label(grid, name);
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
		}

		public POITree(POITreeModel model)
			: base((TreeViewModel<PointOfInterest>)model)
		{
		}
	}

	private class PointOfInterestPane : VisualElement
	{
		private POITree.POITreeModel model;

		public Button NewPOIBtn { get; }

		public Observable<string> GoTo => model.GoTo;

		public Observable<string> RemovePOI => model.RemovePOI;

		public Observable<string> Update => model.Update;

		public Observable<string> EditDescriptor => model.EditDescriptor;

		public Observable<string> NewChildPoi => model.NewChildPoi;

		public PointOfInterestPane(POIManager poiManager, ScopeSupport scope)
		{
			VisualElement seq = UIBuilder.HLayout((VisualElement)this);
			UIBuilder.Label(seq, "Points of Interest");
			NewPOIBtn = UIBuilder.Button(seq, "New POI here");
			NewPOIBtn.style.fontSize = 12f;
			NewPOIBtn.style.paddingTop = 0f;
			NewPOIBtn.style.paddingBottom = 0f;
			model = new POITree.POITreeModel(poiManager, scope);
			UIBuilder.AddElement<POITree>((VisualElement)UIBuilder.AddElement<ScrollView>((VisualElement)this, new ScrollView()), new POITree(model));
		}
	}

	private class POIDescriptorEditor : VisualElement
	{
		private Label poiId;

		private TextField displayName;

		private IReadOnlyList<PointOfInterest> points;

		private DropdownField parentPois;

		private TextField localDisplayName;

		public PointOfInterestDescriptor Model { get; private set; }

		public event Action OnClose = delegate
		{
		};

		public event Action OnSave = delegate
		{
		};

		public POIDescriptorEditor()
		{
			VisualElement layout = UIBuilder.VLayout((VisualElement)this);
			TableBuilder val = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(layout, "Point of interest ID:");
				poiId = UIBuilder.Label(layout, "");
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
			TableBuilder val2 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(layout, "Parent Poi");
				layout.Add(parentPois = new DropdownField());
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
			TableBuilder val3 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(layout, "Display Name");
				displayName = UIBuilder.TextBox(layout, "");
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			TableBuilder val4 = UIBuilder.Row(layout, (Action<VisualElement>)null);
			try
			{
				UIBuilder.Label(layout, "Local Display Name");
				localDisplayName = UIBuilder.TextBox(layout, "");
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
			UIBuilder.LayoutColSizes(layout, new int[2] { 150, 200 });
			VisualElement seq = UIBuilder.HLayout(layout);
			UIBuilder.Button(seq, "Save").clicked += POIDescriptorEditor_clicked;
			UIBuilder.Button(seq, "Close").clicked += delegate
			{
				this.OnClose();
			};
			layout.style.width = new Length(100f, LengthUnit.Percent);
			layout.style.height = new Length(100f, LengthUnit.Percent);
		}

		private void POIDescriptorEditor_clicked()
		{
			if (displayName.value != "")
			{
				Model.DisplayName = displayName.value;
			}
			else
			{
				Model.DisplayName = null;
			}
			if (localDisplayName.value != "")
			{
				Model.LocalDisplayName = localDisplayName.value;
			}
			else
			{
				Model.LocalDisplayName = null;
			}
			if (parentPois.index == 0)
			{
				Model.ParentPoi = null;
			}
			else
			{
				Model.ParentPoi = parentPois.value;
			}
			this.OnSave();
		}

		public void SetModel(PointOfInterestDescriptor value)
		{
			Model = value;
			AfterModelSet();
		}

		private void AfterModelSet()
		{
			poiId.text = Model.Id;
			displayName.value = Model.DisplayName;
			localDisplayName.value = Model.LocalDisplayName;
			List<string> choices = new List<string>();
			choices.Add("[None]");
			foreach (PointOfInterest poi in points)
			{
				if (!(poi.Id == Model.Id) && (poi.Desc == null || !(poi.Desc.ParentPoi == Model.Id)))
				{
					choices.Add(poi.Id);
				}
			}
			parentPois.choices = choices;
			if (Model.ParentPoi == null || Model.ParentPoi.Trim() == "")
			{
				parentPois.index = 0;
				return;
			}
			int idx = choices.IndexOf(Model.ParentPoi);
			if (idx == -1)
			{
				Logger.Global.Error("Unresolved parent POI {0} at {1}", Model.ParentPoi, Model.Id);
				idx = 0;
			}
			parentPois.index = idx;
		}

		internal void SetPOIs(IReadOnlyList<PointOfInterest> points)
		{
			this.points = points;
		}
	}

	private POIManager poiManager;

	private PositionManager positionManager;

	private InteractionManager interactionManager;

	private PoseManager poseManager;

	private PointOfInterestPane Window { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		poiManager = Lookup<POIManager>();
		positionManager = Lookup<PositionManager>();
		interactionManager = Lookup<InteractionManager>();
		poseManager = Lookup<PoseManager>();
		Window = new PointOfInterestPane(poiManager, base.Scope);
		Lookup<ExtendedPoseEditor>().AddTab("Points of interest", Window, base.Scope);
		Window.NewPOIBtn.clicked += NewPOIBtn_OnClick;
		Window.Update.Add(OnUpdatePoi, base.Scope);
		Window.RemovePOI.Add(OnRemovePoi, base.Scope);
		Window.GoTo.Add(OnGoToPoi, base.Scope);
		Window.EditDescriptor.Add(OnEditDescriptor, base.Scope);
		Window.NewChildPoi.Add(NewPoi, base.Scope);
	}

	private void NewPOIBtn_OnClick()
	{
		NewPoi(null);
	}

	private void NewPoi(string parent)
	{
		base.Session.Modal.RequestInput("Type new POI ID", "Place").OnResult += delegate(string name)
		{
			if (name != null)
			{
				if (poiManager.FindPOI(name) != null)
				{
					base.Session.Modal.MessageError($"Point of interest named {name} already exists");
				}
				else
				{
					Transform transform = positionManager.CurrentPlace.NativeGoto.transform;
					PointOfInterest pointOfInterest = poiManager.CreatePointOfInterestNow(name);
					if (pointOfInterest != null && parent != null && poiManager.FindPOI(parent) != null)
					{
						PointOfInterestDescriptor pointOfInterestDescriptor = pointOfInterest.Desc;
						if (pointOfInterestDescriptor == null)
						{
							pointOfInterestDescriptor = new PointOfInterestDescriptor();
						}
						pointOfInterestDescriptor.ParentPoi = parent;
						poiManager.SavePOID(pointOfInterestDescriptor);
						poiManager.RegisteredPlacesChanged.Invoke();
						positionManager.GoTo(pointOfInterest);
					}
				}
			}
		};
	}

	private void OnRemovePoi(string obj)
	{
		PointOfInterest poi = poiManager.FindPOI(obj);
		if (poi == null)
		{
			return;
		}
		base.Session.Modal.MessageBoxYesNo($"Are you sure you want to delete {obj}").OnResult += delegate(bool result)
		{
			if (result)
			{
				poiManager.RemovePoint(poi);
			}
		};
	}

	private void OnUpdatePoi(string obj)
	{
		PointOfInterest poi = poiManager.FindPOI(obj);
		if (poi == null)
		{
			return;
		}
		base.Session.Modal.MessageBoxYesNo($"Are you sure you want to move {obj}").OnResult += delegate(bool result)
		{
			if (result)
			{
				poiManager.MovePoi(poi);
			}
		};
	}

	private void OnGoToPoi(string obj)
	{
		PointOfInterest poi = poiManager.FindPOI(obj);
		if (poi != null)
		{
			positionManager.GoTo(poi);
			interactionManager.SetPosture(poseManager.StandingPostureAt(poi));
			interactionManager.StartAnimation("Binding");
		}
	}

	private void OnEditDescriptor(string poiId)
	{
		PointOfInterest poi = poiManager.FindPOI(poiId);
		if (poi == null)
		{
			return;
		}
		PointOfInterestDescriptor descriptor = poi.Desc;
		if (descriptor == null)
		{
			logger.Error("Null descriptor {0}", poiId);
			descriptor = new PointOfInterestDescriptor();
			descriptor.Id = poiId;
		}
		string originalParentPoi = descriptor.ParentPoi;
		UITKManagedPanel ui = Lookup<CustomSceneFeature>().EditorUiPanel;
		POIDescriptorEditor pde = new POIDescriptorEditor();
		PopupWindowEx pwe = new PopupWindowEx(pde);
		pde.SetPOIs(poiManager.Points);
		pde.SetModel(descriptor);
		pwe.SetCenterScreen(400, 250);
		Action close = pwe.ShowModal(ui.GameView);
		pde.OnClose += close;
		pde.OnSave += delegate
		{
			poiManager.SavePOID(descriptor);
			if (originalParentPoi != descriptor.ParentPoi)
			{
				poiManager.RegisteredPlacesChanged.Invoke();
			}
			close();
		};
	}
}
