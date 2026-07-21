using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BetterExperience.Features;

public static class NavMeshUtils
{
	private static Vector3[] PATH = new Vector3[100];

	public static void ReadPathInto(NavMeshPath navPath, List<Vector3> path)
	{
		int len = navPath.GetCornersNonAlloc(PATH);
		for (int i = 0; i < len; i++)
		{
			path.Add(PATH[i]);
		}
	}
}
