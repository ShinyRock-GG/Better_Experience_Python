using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Features.BetterHand;

internal class FingerAnimator
{
	private const float FORWARD_DURATION = 0.2f;

	private FloatAnimator[] forwardAnimators;

	private FloatAnimator horizontalAnimator;

	private Quaternion[] targetRotation;

	private bool checkCollision = true;

	public bool[] targetCompletion;

	public Finger Finger { get; private set; }

	public FingerAnimator(Finger finger, Quaternion[] rotationTarget, bool[] completed)
	{
		Finger = finger;
		forwardAnimators = new FloatAnimator[finger.Transforms.Length];
		for (int i = 0; i < forwardAnimators.Length; i++)
		{
			forwardAnimators[i] = new FloatAnimator(0f, 0f, 0.2f);
		}
		horizontalAnimator = new FloatAnimator(0f, 0f, 1f);
		targetRotation = rotationTarget;
		targetCompletion = completed;
	}

	public bool IsAnimatorActive(int part)
	{
		if (part == 2)
		{
			if (!forwardAnimators[part].Active)
			{
				return horizontalAnimator.Active;
			}
			return true;
		}
		return forwardAnimators[part].Active;
	}

	public void Update(int partIndex, float dt)
	{
		bool ignoreCollisions = !checkCollision;
		Quaternion hRotation = Quaternion.identity;
		if (partIndex == 2)
		{
			horizontalAnimator.Update(dt);
			Finger.RotateLocal(2, Vector3.right, horizontalAnimator.Value, ignoreCollisions, Quaternion.identity);
			hRotation = Finger.startRotations[partIndex] * Quaternion.Inverse(Finger.Transforms[partIndex].localRotation);
		}
		if (!targetCompletion[partIndex])
		{
			bool doupdate = forwardAnimators[partIndex].Active;
			forwardAnimators[partIndex].Update(dt);
			if (doupdate)
			{
				float angle = Finger.RotateLocal(partIndex, Vector3.forward, forwardAnimators[partIndex].Value, ignoreCollisions, hRotation);
				if (angle == 0f && forwardAnimators[partIndex].Value != 0f)
				{
					forwardAnimators[partIndex].Update(0f - dt);
					forwardAnimators[partIndex].Stop();
				}
				else
				{
					targetRotation[partIndex] = Finger.Transforms[partIndex].localRotation;
				}
			}
		}
		Finger.Transforms[partIndex].localRotation = targetRotation[partIndex];
	}

	public void RequestForwardRotation(float[] angles)
	{
		for (int i = 0; i < angles.Length; i++)
		{
			float from = forwardAnimators[i].To;
			forwardAnimators[i] = new FloatAnimator(from, angles[i], 0.2f);
		}
		targetCompletion.Fill(value: false);
	}

	public void RequestSideRotation(float angle)
	{
		float from = horizontalAnimator.To;
		horizontalAnimator = new FloatAnimator(from, angle, 0.1f);
		targetCompletion.Fill(value: false);
	}

	public void RequestPose(float[] forward, float side, bool resistable)
	{
		RequestForwardRotation(forward);
		RequestSideRotation(side);
		checkCollision = resistable;
	}

	public void ResetBindingPoseNow()
	{
		for (int i = 0; i < Finger.Transforms.Length; i++)
		{
			Finger.startRotations[i] = Finger.Transforms[i].localRotation;
			if (i == 0)
			{
				Finger.startRotations[i] = Quaternion.identity;
			}
		}
	}
}
