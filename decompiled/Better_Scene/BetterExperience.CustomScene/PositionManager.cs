using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using PixelCrushers.DialogueSystem;
using RootMotion.Dynamics;
using UnityEngine;

namespace BetterExperience.CustomScene;

internal class PositionManager : StoryService
{
	private Vector3 lastPosition;

	private Quaternion lastRotation;

	private bool fixGoto = true;

	private POIManager poiManager;

	public Observable<CurrentPlace> PlaceChanged { get; } = new Observable<CurrentPlace>();

	public Observable OnAnimatorTeleported { get; } = new Observable();

	public CurrentPlace CurrentPlace { get; private set; }

	private GoToScenaManager Manager => Singleton<GoToScenaManager>.instance;

	private Transform RootMotion => ((Character)(object)base.Session.Guest.Impl).animatorRootMotionTransform;

	private bool TransitionBlocked
	{
		get
		{
			if (!DialogueManager.IsConversationActive && base.Session.Guest.Puppet.PuppetMaster.mode != PuppetMaster.Mode.Disabled)
			{
				return AutoPoiBlocked;
			}
			return true;
		}
	}

	public bool AutoPoiBlocked { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		poiManager = Lookup<POIManager>();
		Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
		poiManager.BeforePOIRemove.Add(BeforePoiRemove, base.Scope);
	}

	private void BeforePoiRemove(PointOfInterest poi)
	{
		if (CurrentPlace != null && CurrentPlace.POI == poi && poiManager.Points.Count > 0)
		{
			GoTo(poiManager.Points[0], CurrentPlace.Orientation);
		}
	}

	public void GoTo(PointOfInterest pointOfInterest, PoseOrientation orientation = PoseOrientation.UNIVERSAL)
	{
		if (orientation == PoseOrientation.UNIVERSAL && CurrentPlace != null)
		{
			orientation = CurrentPlace.Orientation;
		}
		if (base.Session.Guest != null && (Object)(object)base.Session.Guest.Impl != null)
		{
			GoToScenaManager.GoTo goTo = Manager.Obtener(pointOfInterest.Id);
			if (goTo != null)
			{
				if (logger.EnableDebug)
				{
					logger.Debug("Goto {0} -> {1}", pointOfInterest.Id, goTo.Id);
				}
				Manager.Apply(((Character)(object)base.Session.Guest.Impl).SetPositionAndRotation, orientation == PoseOrientation.BACK, goTo);
				OnAnimatorTeleported.Invoke();
			}
			else
			{
				logger.Error("Goto not found {0}", pointOfInterest.Id);
			}
		}
		bool before = AutoPoiBlocked;
		AutoPoiBlocked = false;
		OnUpdate();
		AutoPoiBlocked = before;
	}

	private void OnUpdate()
	{
		if (base.Session.Guest != null && (Object)(object)base.Session.Guest.Impl != null)
		{
			if (!TransitionBlocked)
			{
				Transform rm = RootMotion;
				if (lastPosition != rm.position || lastRotation != rm.rotation)
				{
					UpdateCurrentPlace();
					lastPosition = rm.position;
					lastRotation = rm.rotation;
				}
			}
		}
		else if (CurrentPlace != null)
		{
			SetCurrentPlace(null);
		}
	}

	internal void ResetFixGoto()
	{
		fixGoto = true;
	}

	private void UpdateCurrentPlace()
	{
		bool isTurnedAround;
		GoToScenaManager.GoTo gt = Manager.CurrentGoTo(RootMotion, out isTurnedAround, 0.1f, 10f);
		if (gt == null)
		{
			if (fixGoto)
			{
				PointOfInterest ogt = poiManager.FindPOI("Original_GoTo");
				if (ogt != null)
				{
					GoTo(ogt, PoseOrientation.FRONT);
				}
				else
				{
					logger.Error("Unable to esolve original goto");
				}
				fixGoto = false;
			}
			return;
		}
		if (fixGoto)
		{
			if (isTurnedAround)
			{
				isTurnedAround = false;
				Manager.Apply(((Character)(object)base.Session.Guest.Impl).SetPositionAndRotation, isTurnedAround: false, gt);
			}
			fixGoto = false;
		}
		PointOfInterest wp = poiManager.FindPOI(gt.Id);
		if (wp == null)
		{
			logger.Error("No POI for {0}", gt.Id);
		}
		else
		{
			CurrentPlace place = new CurrentPlace(wp, isTurnedAround ? PoseOrientation.BACK : PoseOrientation.FRONT, gt);
			SetCurrentPlace(place);
		}
	}

	private void SetCurrentPlace(CurrentPlace place)
	{
		if (CurrentPlace != place)
		{
			CurrentPlace = place;
			PlaceChanged.Invoke(place);
		}
		logger.Info("Set current place {0}", (place != null) ? place.POI.Id : "<NULL>");
	}
}
