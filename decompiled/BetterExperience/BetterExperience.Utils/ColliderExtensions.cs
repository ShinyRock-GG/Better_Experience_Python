using System.Linq;
using UnityEngine;

namespace BetterExperience.Utils;

public static class ColliderExtensions
{
	private static readonly Vector3[] capsuleOrientations = new Vector3[3]
	{
		new Vector3(1f, 0f, 0f),
		new Vector3(0f, 1f, 0f),
		new Vector3(0f, 0f, 1f)
	};

	public static bool QueryOverlaps(this CapsuleCollider capsule, Transform transform, out Collider[] result, int layerMask)
	{
		Vector3 direction = capsuleOrientations[capsule.direction];
		float offset = capsule.height / 2f - capsule.radius;
		Vector3 a = capsule.center + direction * offset;
		Vector3 b = capsule.center - direction * offset;
		a = transform.TransformPoint(a);
		b = transform.TransformPoint(b);
		Vector3 r = transform.TransformVector(capsule.radius, capsule.radius, capsule.radius);
		float radius = (from xyz in Enumerable.Range(0, 3)
			select (xyz != capsule.direction) ? r[xyz] : 0f).Select(Mathf.Abs).Max();
		result = Physics.OverlapCapsule(a, b, radius, layerMask);
		return result.Length != 0;
	}

	public static bool QueryOverlaps(this BoxCollider box, Transform transform, out Collider[] result, int layerMask)
	{
		Vector3 center = transform.TransformPoint(box.center);
		Vector3 extents = transform.TransformVector(box.size) / 2f;
		result = Physics.OverlapBox(center, extents, transform.rotation, layerMask);
		return result.Length != 0;
	}

	public static bool QueryOverlaps(this BoxCollider box, Matrix4x4 transform, out Collider[] result, int layerMask)
	{
		Vector3 center = transform.MultiplyPoint(box.center);
		Vector3 extents = transform.MultiplyVector(box.size) / 2f;
		result = Physics.OverlapBox(center, extents, transform.rotation, layerMask);
		return result.Length != 0;
	}

	public static bool QueryRaycast(this BoxCollider box, Transform transform, Vector3 direction, out RaycastHit result, int layerMask, float distance)
	{
		Vector3 center = transform.TransformPoint(box.center);
		Vector3 size = transform.TransformVector(box.size);
		size = UnityUtils.Abs(size);
		box.enabled = false;
		try
		{
			return Physics.BoxCast(center, size / 2f, direction, out result, transform.rotation, distance, layerMask);
		}
		finally
		{
			box.enabled = true;
		}
	}

	public static bool ComputePenetration(this Collider c1, Vector3 pos1, Quaternion rot2, Collider c2, out Vector3 direction, out float distance)
	{
		return Physics.ComputePenetration(c1, pos1, rot2, c2, c2.transform.position, c2.transform.rotation, out direction, out distance);
	}
}
