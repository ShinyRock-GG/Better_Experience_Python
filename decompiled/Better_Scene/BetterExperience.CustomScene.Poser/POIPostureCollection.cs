using System.Collections.Generic;
using System.Linq;

namespace BetterExperience.CustomScene.Poser;

public class POIPostureCollection
{
	public Dictionary<string, POIPosture> ExactPostures { get; } = new Dictionary<string, POIPosture>();

	public POIPosture DefaultPosture
	{
		get
		{
			if (ExactPostures.Count > 0)
			{
				return ExactPostures.Values.Last();
			}
			return null;
		}
	}

	internal void Add(Posture main, POIPosture poiPosture)
	{
		ExactPostures[poiPosture.Id] = poiPosture;
		poiPosture.Poses = main.Poses;
	}

	public bool Contains(POIPosture posture)
	{
		return ExactPostures.Values.Contains(posture);
	}

	internal void Remove(POIPosture pp)
	{
		ExactPostures.Remove(pp.Id);
	}
}
