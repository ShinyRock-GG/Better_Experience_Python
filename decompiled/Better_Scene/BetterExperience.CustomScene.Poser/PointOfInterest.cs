using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.UI;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PointOfInterest
{
	private PointOfInterestData data;

	private PointOfInterestDescriptor desc;

	private Transform transform;

	public string Id => data.Id;

	public Transform Transform => transform;

	public INombrableLocalizado Nombrable { get; private set; }

	public PointOfInterestData Data => data;

	public PointOfInterestDescriptor Desc => desc;

	public string Name
	{
		get
		{
			if (desc == null || desc.DisplayName == null)
			{
				return Id;
			}
			return desc.DisplayName;
		}
	}

	public string ParentPoiId
	{
		get
		{
			if (desc == null)
			{
				return null;
			}
			return desc.ParentPoi;
		}
	}

	public PointOfInterest(PointOfInterestData data, PointOfInterestDescriptor desc, Transform transform)
	{
		this.data = data;
		this.desc = desc;
		this.transform = transform;
		data.Apply(transform);
		Nombrable = CreateNombrable(Name);
	}

	public PointOfInterest(string id, INombrableLocalizado name, Transform t, PointOfInterestDescriptor descriptor)
	{
		data = new PointOfInterestData();
		data.Id = id;
		desc = descriptor;
		transform = t;
		if (desc == null || desc.DisplayName == null)
		{
			Nombrable = name;
		}
		else
		{
			Nombrable = CreateNombrable(Name);
		}
	}

	private Nombrable CreateNombrable(string name)
	{
		Nombrable nombrable = new Nombrable();
		List<Nombrable.Item> data = Traverse.Create((object)nombrable).Field<List<Nombrable.Item>>("m_Localizados").Value;
		Nombrable.Item item = new Nombrable.Item();
		Traverse.Create((object)item).Field<string>("m_text").Value = name;
		data.Add(item);
		return nombrable;
	}

	public override string ToString()
	{
		return "POI[ id=" + Id + " ]";
	}
}
