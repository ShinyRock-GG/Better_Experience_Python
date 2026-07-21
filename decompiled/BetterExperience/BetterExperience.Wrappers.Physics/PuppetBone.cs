using System.Collections.Generic;
using UnityEngine;

namespace BetterExperience.Wrappers.Physics;

internal class PuppetBone
{
	public Transform Transform { get; set; }

	public MeshCollider MeshCollider { get; set; }

	public CapsuleCollider CapsuleCollider { get; set; }

	public BoxCollider BoxCollider { get; set; }

	public SphereCollider SphereCollider { get; set; }

	public List<Collider> Colliders { get; private set; } = new List<Collider>();

	public PuppetBone(Transform transform, List<Collider> colliders)
	{
		Transform = transform;
		foreach (Collider c in colliders)
		{
			if (c is MeshCollider)
			{
				MeshCollider = (MeshCollider)c;
			}
			else if (c is CapsuleCollider)
			{
				CapsuleCollider = (CapsuleCollider)c;
			}
			else if (c is BoxCollider)
			{
				BoxCollider = (BoxCollider)c;
			}
			else if (c is SphereCollider)
			{
				SphereCollider = (SphereCollider)c;
			}
			Colliders.Add(c);
		}
	}
}
