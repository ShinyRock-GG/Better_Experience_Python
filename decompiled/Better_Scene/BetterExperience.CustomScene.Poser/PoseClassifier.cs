using BetterExperience.CustomScene.Characters;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PoseClassifier
{
	private Transform animatorRoot;

	public Armature Armature { get; }

	public PoseClassifier(Armature armature, Transform animatorRootTransform)
	{
		Armature = armature;
		animatorRoot = animatorRootTransform;
	}

	public PoseState Encode()
	{
		return new PoseState
		{
			hip = GenData(Armature.Hip.transform),
			leftFoot = GenData(Armature.LeftFoot.transform),
			rightFoot = GenData(Armature.RightFoot.transform),
			leftHand = GenData(Armature.LeftHand.transform),
			rightHand = GenData(Armature.RightHand.transform),
			head = GenData(Armature.Head.transform)
		};
	}

	private SpartialData GenData(Transform t)
	{
		return new SpartialData
		{
			forward = ComputeFace(t),
			hip = ComputeD4(t, Armature.Hip.transform)
		};
	}

	private Direction4 ComputeD4(Transform a, Transform b, float threshold = 0.2f)
	{
		Vector3 vA = animatorRoot.InverseTransformPoint(a.position);
		Vector3 vB = animatorRoot.InverseTransformPoint(b.position);
		Vector3 d = (vA - vB).normalized;
		Direction4 result = default(Direction4);
		float w = 0f;
		if (Mathf.Abs(d.z) > threshold)
		{
			w = Mathf.Abs(d.z);
			if (d.z > 0f)
			{
				result.xyz = result.xyz.Add(Direction.forward);
			}
			else
			{
				result.xyz = result.xyz.Add(Direction.backward);
			}
			if (d.z > 0f)
			{
				result.main = Direction.forward;
			}
			else
			{
				result.main = Direction.backward;
			}
		}
		if (Mathf.Abs(d.x) > threshold)
		{
			if (Mathf.Abs(d.x) > w)
			{
				w = Mathf.Abs(d.x);
				if (d.x > 0f)
				{
					result.main = Direction.right;
				}
				else
				{
					result.main = Direction.left;
				}
			}
			if (d.x > 0f)
			{
				result.xyz = result.xyz.Add(Direction.right);
			}
			else
			{
				result.xyz = result.xyz.Add(Direction.left);
			}
		}
		float dy = Mathf.Abs(d.y);
		if (dy > threshold)
		{
			Direction dir = ((d.y > 0f) ? Direction.up : Direction.down);
			if (dy > w)
			{
				w = dy;
				result.main = dir;
			}
			result.xyz = result.xyz.Add(dir);
		}
		return result;
	}

	private Direction ComputeFace(Transform transform)
	{
		Transform root = animatorRoot;
		Vector3 fwd = transform.forward;
		float forward = AbsAngle(root.forward, fwd);
		float backward = AbsAngle(-root.forward, fwd);
		float right = AbsAngle(root.right, fwd);
		float left = AbsAngle(-root.right, fwd);
		float up = AbsAngle(root.up, fwd);
		float down = AbsAngle(-root.up, fwd);
		Direction dir = Direction.forward;
		float angle = forward;
		if (angle > backward)
		{
			dir = Direction.backward;
			angle = backward;
		}
		if (angle > right)
		{
			dir = Direction.right;
			angle = right;
		}
		if (angle > left)
		{
			dir = Direction.left;
			angle = left;
		}
		if (angle > up)
		{
			dir = Direction.up;
			angle = up;
		}
		if (angle > down)
		{
			dir = Direction.down;
			angle = right;
		}
		return dir;
	}

	private float AbsAngle(Vector3 a, Vector3 b)
	{
		return Mathf.Abs(Vector3.Angle(a, b));
	}
}
