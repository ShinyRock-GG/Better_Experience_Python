using System.Collections.Generic;
using System.Linq;
using BetterExperience.Wrappers.Physics;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class Finger
{
	private CapsuleCollider[] colliders;

	private PhysicalPuppet puppet;

	private Vector3[] capsuleOrientations = new Vector3[3]
	{
		new Vector3(1f, 0f, 0f),
		new Vector3(0f, 1f, 0f),
		new Vector3(0f, 0f, 1f)
	};

	public Transform[] Transforms { get; private set; }

	public Quaternion[] startRotations { get; private set; }

	public bool[] colliderContacts { get; set; } = new bool[3];

	public Finger(PhysicalPuppet puppet, params HumanBodyBones[] bones)
	{
		this.puppet = puppet;
		Transforms = new Transform[bones.Length];
		colliders = new CapsuleCollider[bones.Length];
		startRotations = new Quaternion[bones.Length];
		for (int i = 0; i < bones.Length; i++)
		{
			Transforms[i] = puppet.GetBoneTransform(bones[i]);
			List<Collider> list = puppet.ColliderByName(Transforms[i].name);
			foreach (Collider c in list)
			{
				CapsuleCollider cc = c as CapsuleCollider;
				if (cc != null)
				{
					colliders[i] = cc;
					break;
				}
			}
			startRotations[i] = Transforms[i].localRotation;
		}
	}

	public float RotateLocal(int index, Vector3 axis, float maxAngle, bool ignoreCollision, Quaternion additionalRotation)
	{
		Transform t = Transforms[index];
		if (t.localRotation != startRotations[index])
		{
			t.localRotation = startRotations[index];
		}
		if (maxAngle == 0f)
		{
			return 0f;
		}
		Quaternion rot = t.localRotation * additionalRotation;
		float resultRotation = 0f;
		if (TryRotateLocal(index, axis, maxAngle) || ignoreCollision)
		{
			return maxAngle;
		}
		t.localRotation = rot;
		if (!TryRotateLocal(index, axis, 0f))
		{
			return 0f;
		}
		while (Mathf.Abs(maxAngle) > 0.1f)
		{
			float half = maxAngle / 2f;
			if (TryRotateLocal(index, axis, half))
			{
				rot = t.localRotation;
				maxAngle -= half;
				resultRotation += half;
				continue;
			}
			if ((double)half <= 0.1)
			{
				break;
			}
			maxAngle = half;
			t.localRotation = rot;
		}
		return resultRotation;
	}

	public bool TryRotateLocal(int index, Vector3 axis, float maxAngle)
	{
		Transform transform = Transforms[index];
		transform.Rotate(axis, maxAngle, Space.Self);
		if (TestCollision(index))
		{
			return false;
		}
		return true;
	}

	public bool TestCollision(int index)
	{
		for (int i = 0; i <= index; i++)
		{
			if (colliderContacts[i] || TestCollision(colliders[i]))
			{
				return true;
			}
		}
		return false;
	}

	public bool TestCollision(CapsuleCollider capsule)
	{
		Vector3 direction = capsuleOrientations[capsule.direction];
		float offset = capsule.height / 2f - capsule.radius;
		Vector3 a = capsule.center + direction * offset;
		Vector3 b = capsule.center - direction * offset;
		a = capsule.transform.TransformPoint(a);
		b = capsule.transform.TransformPoint(b);
		Vector3 r = capsule.transform.TransformVector(capsule.radius, capsule.radius, capsule.radius);
		float radius = (from xyz in Enumerable.Range(0, 3)
			select (xyz != capsule.direction) ? r[xyz] : 0f).Select(Mathf.Abs).Max();
		Collider[] colliders = Physics.OverlapCapsule(a, b, radius);
		if (colliders.Length != 0)
		{
			foreach (Collider col in colliders)
			{
				if (col is MeshCollider && !puppet.ContainsCollider(col))
				{
					return true;
				}
			}
		}
		return false;
	}
}
