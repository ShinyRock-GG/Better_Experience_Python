using System.Collections.Generic;
using System.Linq;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers.Interacciones;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene;

public class POIManager : StoryService
{
	private Repository<PointOfInterestData> repository = new Repository<PointOfInterestData>("poi", "waypoints", "POIs");

	private Repository<PointOfInterestDescriptor> descriporRepository = new Repository<PointOfInterestDescriptor>("poid", "waypoints", "POI descriptors");

	private List<PointOfInterest> points = new List<PointOfInterest>();

	public IReadOnlyList<PointOfInterest> Points => points;

	private GoToScenaManager Manager => Singleton<GoToScenaManager>.instance;

	public Observable RegisteredPlacesChanged { get; } = new Observable();

	public Observable<PointOfInterest> BeforePOIRemove { get; } = new Observable<PointOfInterest>();

	internal void RemovePoint(PointOfInterest poi)
	{
		if (!Points.Contains(poi))
		{
			return;
		}
		points.Remove(poi);
		BeforePOIRemove.Invoke(poi);
		Transform go = poi.Transform;
		if (go != null)
		{
			GoToScenaManager.GoTo goTo = Manager.Obtener(go.transform);
			if (goTo != null)
			{
				Manager.Remove(goTo);
			}
			Object.DestroyImmediate(go);
		}
		RegisteredPlacesChanged.Invoke();
		repository.Delete(poi.Data);
		if (poi.Desc != null)
		{
			descriporRepository.Delete(poi.Desc);
		}
	}

	public PointOfInterest FindPOI(string id)
	{
		foreach (PointOfInterest wp in points)
		{
			if (wp.Id == id)
			{
				return wp;
			}
		}
		return null;
	}

	public override void OnInit()
	{
		base.OnInit();
		base.AsyncHandles.Add(repository.InitAsync(base.Story.VFS));
		base.AsyncHandles.Add(descriporRepository.InitAsync(base.Story.VFS));
	}

	public override void OnStart()
	{
		base.OnStart();
		LoadPointsOfInterest();
	}

	internal void SavePOID(PointOfInterestDescriptor descriptor)
	{
		descriporRepository.Save(descriptor);
	}

	private void LoadPointsOfInterest()
	{
		foreach (GoToScenaManager.GoTo gt in Manager.registrados)
		{
			PointOfInterestDescriptor desc = descriporRepository.Get(gt.Id);
			if (desc == null)
			{
				desc = new PointOfInterestDescriptor();
				desc.Id = gt.Id;
			}
			PointOfInterest wp = new PointOfInterest(gt.Id, gt.nombrable, gt.transform, desc);
			logger.Info("Loaded POI {0}", wp.Id);
			points.Add(wp);
			PointOfInterestData poiOverride = repository.Get(gt.Id);
			if (poiOverride != null)
			{
				logger.Info("Overriding builtin POI {0}", gt.Id);
				wp.Data.Position = poiOverride.Position;
				wp.Data.Rotation = poiOverride.Rotation;
				wp.Data.Apply(wp.Transform);
			}
		}
		foreach (PointOfInterestData wp2 in repository.All())
		{
			PointOfInterest existing = FindPOI(wp2.Id);
			if (existing == null)
			{
				Transform t = UnityUtils.NewTransform("Waypoint_" + wp2.Id);
				PointOfInterestDescriptor desc2 = descriporRepository.Get(wp2.Id);
				if (desc2 == null)
				{
					desc2 = new PointOfInterestDescriptor();
					desc2.Id = wp2.Id;
				}
				PointOfInterest poi = new PointOfInterest(wp2, desc2, t);
				RegisterPOI(poi);
			}
		}
	}

	private Transform GetCurrentRootBone()
	{
		IInteraccionesDeCharacter i = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<IInteraccionesDeCharacter>();
		if (i != null)
		{
			InteraccionDeCharacter primary = i.ObtenerFirstEjecutandosePrimaria();
			if (primary != null)
			{
				InteraccionPrimariaBase iBase = primary.instancia as InteraccionPrimariaBase;
				if (iBase != null)
				{
					return iBase.interactionRootBone;
				}
			}
		}
		return null;
	}

	internal void MovePoi(PointOfInterest poi)
	{
		Transform rootmotion = GetCurrentRootBone();
		if (rootmotion == null)
		{
			logger.Error("Unable to create waypoint: cannot resolve poser rootmotion transform");
			return;
		}
		Vector3 pos = rootmotion.position;
		Quaternion rot = rootmotion.rotation;
		rot *= Quaternion.AngleAxis(90f, Vector3.right);
		poi.Data.Position = pos.AsFloatArray();
		poi.Data.Rotation = rot.AsFloatArray();
		repository.Save(poi.Data);
		poi.Data.Apply(poi.Transform);
	}

	public PointOfInterest CreatePointOfInterestNow(string name)
	{
		Transform rootmotion = GetCurrentRootBone();
		if (rootmotion == null)
		{
			logger.Error("Unable to create waypoint: cannot resolve poser rootmotion transform");
			return null;
		}
		Vector3 pos = rootmotion.position;
		Quaternion rot = rootmotion.rotation;
		rot *= Quaternion.AngleAxis(90f, Vector3.right);
		PointOfInterest existing = FindPOI(name);
		if (existing == null)
		{
			PointOfInterestData poiData = new PointOfInterestData();
			poiData.Id = name;
			poiData.Position = pos.AsFloatArray();
			poiData.Rotation = rot.AsFloatArray();
			repository.Save(poiData);
			PointOfInterestDescriptor desc = descriporRepository.Get(poiData.Id);
			if (desc == null)
			{
				desc = new PointOfInterestDescriptor();
				desc.Id = poiData.Id;
				descriporRepository.Save(desc);
			}
			PointOfInterest wp = new PointOfInterest(poiData, desc, UnityUtils.NewTransform("Waypoint_" + poiData.Id));
			RegisterPOI(wp);
			RegisteredPlacesChanged.Invoke();
			rootmotion.localPosition = Vector3.zero;
			rootmotion.localRotation = Quaternion.AngleAxis(-90f, Vector3.right);
			return wp;
		}
		return null;
	}

	private void RegisterPOI(PointOfInterest poi)
	{
		GoToScenaManager gtm = Singleton<GoToScenaManager>.instance;
		if (gtm.TryAdd(new GoToScenaManager.GoTo(poi.Id, poi.Transform, poi.Nombrable, true, null)))
		{
			logger.Info("Created new waypoint {0}", poi.Id);
			points.Add(poi);
		}
		else
		{
			logger.Error("Failed to create waypoint {0}", poi.Id);
		}
	}
}
