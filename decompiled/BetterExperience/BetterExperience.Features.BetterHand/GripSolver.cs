using BetterExperience.Wrappers.Physics;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class GripSolver
{
	private Logger logger = new Logger
	{
		Prefix = "GripSolver"
	};

	private PhysicalPuppet Puppet;

	private FingerAnimator[] fingers;

	public GripTarget GripTarget { get; private set; }

	public int FingerCount => fingers.Length;

	public GripSolver(PhysicalPuppet puppet)
	{
		Puppet = puppet;
		Finger[] fingers = new Finger[5]
		{
			new Finger(Puppet, HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal),
			new Finger(Puppet, HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal),
			new Finger(Puppet, HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal),
			new Finger(Puppet, HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal),
			new Finger(Puppet, HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbProximal)
		};
		this.fingers = new FingerAnimator[fingers.Length];
		GripTarget = new GripTarget(fingers.Length);
		for (int i = 0; i < fingers.Length; i++)
		{
			GripTarget.InitFinger(i, fingers[i].Transforms);
			this.fingers[i] = new FingerAnimator(fingers[i], GripTarget.TargetPose[i], GripTarget.TargetCompletion[i]);
		}
	}

	internal void RequestPose(int fingerIdx, float[] forwardAngles, float sideAngle, bool checkCollision)
	{
		fingers[fingerIdx].RequestPose(forwardAngles, sideAngle, checkCollision);
	}

	internal void CaptureBindingPose()
	{
		for (int i = 0; i < fingers.Length; i++)
		{
			fingers[i].ResetBindingPoseNow();
			GripTarget.Bind(i);
		}
	}

	internal bool Update(float dt)
	{
		for (int j = fingers[0].Finger.Transforms.Length - 1; j >= 0; j--)
		{
			for (int i = 0; i < fingers.Length; i++)
			{
				if (GripTarget.IsPartCompleted(j + 1))
				{
					fingers[i].Update(j, dt);
				}
			}
		}
		GripTarget.Update(dt);
		bool complete = true;
		for (int k = 0; k < GripTarget.TargetCompletion.Length; k++)
		{
			for (int l = 0; l < GripTarget.TargetCompletion[k].Length; l++)
			{
				GripTarget.TargetCompletion[k][l] &= !fingers[k].IsAnimatorActive(l);
				complete &= GripTarget.TargetCompletion[k][l];
			}
		}
		return complete;
	}

	internal void Apply()
	{
		GripTarget.Apply();
	}

	internal void SetContactMatrix(bool[][] contactMatrix)
	{
		for (int i = 0; i < contactMatrix.Length; i++)
		{
			FingerAnimator finger = fingers[i];
			bool[] vector = contactMatrix[i];
			finger.Finger.colliderContacts = vector;
		}
	}

	internal bool SetBindingPoseFrom(Transform root)
	{
		for (int i = 0; i < fingers.Length; i++)
		{
			FingerAnimator finger = fingers[i];
			Transform[] transforms = finger.Finger.Transforms;
			foreach (Transform t in transforms)
			{
				Transform pose = root.transform.FindDeepChild(t.name);
				if (pose == null)
				{
					logger.Error("Failed to SetBindingPoseFrom: missing {0} transform", t.name);
					return false;
				}
				t.localRotation = pose.localRotation;
			}
		}
		CaptureBindingPose();
		return true;
	}
}
