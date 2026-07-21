using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using Assets._ReusableScripts.Miscellaneous;

namespace BetterExperience.PyStory.Scripting;

public class SimpleCloth
{
	public MapaDeRopa.RopaData _impl { get; private set; }

	public string id { get; }

	public string name { get; }

	public List<string> slots { get; } = new List<string>();

	public string layer { get; }

	public SimpleCloth(MapaDeRopa.RopaData ropa)
	{
		id = ((BaseGlobalUserData)ropa).stringId;
		name = ((BaseGlobalUserData)ropa).nombreCorto;
		foreach (ClothSlot slot in Enum.GetValues(typeof(ClothSlot)))
		{
			if (((uint)slot & (uint)ropa.cubreFlag) != 0)
			{
				slots.Add(slot.ToString());
			}
		}
		ClothLayer clothLayer = (ClothLayer)ropa.layer;
		layer = clothLayer.ToString();
		_impl = ropa;
	}
}
