using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BetterExperience.CustomScene.Characters;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser.Formats;

internal class SimpleVamImporter2
{
	private Logger logger = Logger.Create<SimpleVamImporter2>();

	private Armature armature;

	private List<Transform> boneSequence = new List<Transform>();

	private ArmatureBone RootBone => armature.RootBone;

	public BoneConfiguration BindingPose { get; internal set; }

	public bool FixHeels { get; set; } = true;

	public bool FixHipRootMotion { get; set; }

	public Quaternion GlobalRotation { get; set; } = Quaternion.identity;

	public Vector3 GlobalOffset { get; set; } = new Vector3(0f, 0f, 0.2f);

	public bool StandardOrientation { get; set; }

	public SimpleVamImporter2(Armature temporaryArmature)
	{
		armature = temporaryArmature;
		Queue<Transform> fixRotation = new Queue<Transform>();
		fixRotation.Enqueue(armature.RootBone.transform);
		while (fixRotation.Count > 0)
		{
			Transform t = fixRotation.Dequeue();
			boneSequence.Add(t);
			for (int i = 0; i < t.childCount; i++)
			{
				fixRotation.Enqueue(t.GetChild(i));
			}
		}
	}

	public void ReadFrom(PoseAnimationClip clip)
	{
		RootBone.Armature.StartCoroutine(Play(clip));
	}

	private IEnumerator Play(PoseAnimationClip clip)
	{
		int i = 0;
		while (true)
		{
			if (i >= clip.Frames.Count)
			{
				i = 0;
			}
			yield return new WaitForSeconds(clip.States[i].FadeIn);
			ReadFrom(clip.Frames[i]);
		}
	}

	private void ToVamBindingPose()
	{
		LoadTPose();
		SetRotationLocal(RootBone.transform, Quaternion.LookRotation(Vector3.forward, Vector3.up));
	}

	public void ReadFrom(BoneConfiguration bc)
	{
		ToVamBindingPose();
		Dictionary<Transform, Quaternion> tposeLocals = new Dictionary<Transform, Quaternion>();
		Dictionary<Transform, float> boneLength = new Dictionary<Transform, float>();
		foreach (Transform t in boneSequence)
		{
			tposeLocals[t] = t.localRotation;
			if (t.childCount == 1)
			{
				float length = Vector3.Distance(t.position, t.GetChild(0).position);
				boneLength[t] = length;
			}
		}
		bool readPositions = true;
		bool readRotations = true;
		Dictionary<Transform, Vector3> positions = new Dictionary<Transform, Vector3>();
		if (readPositions)
		{
			foreach (Transform t2 in boneSequence)
			{
				if (bc.Positions.TryGetValue(t2.name, out var pos))
				{
					Vector3 point = RootBone.transform.TransformPoint(pos);
					positions[t2] = point;
				}
			}
			foreach (Transform t3 in boneSequence)
			{
				if (positions.TryGetValue(t3, out var pos2))
				{
					t3.position = pos2;
				}
			}
		}
		if (!readRotations)
		{
			return;
		}
		Dictionary<Transform, Quaternion> rotations = new Dictionary<Transform, Quaternion>();
		List<Transform> nonRotated = new List<Transform>();
		foreach (Transform t4 in Enumerable.Reverse(boneSequence))
		{
			if (bc.Rotations.TryGetValue(t4.name, out var rot))
			{
				Quaternion bindingRotation = Quaternion.Inverse(RootBone.transform.rotation) * t4.rotation;
				Quaternion targetRotation = RootBone.transform.rotation * rot * bindingRotation;
				rotations[t4] = targetRotation;
			}
			else
			{
				nonRotated.Add(t4);
			}
		}
		foreach (Transform t5 in boneSequence)
		{
			if (rotations.TryGetValue(t5, out var rot2))
			{
				t5.rotation = rot2;
			}
		}
		foreach (Transform t6 in Enumerable.Reverse(boneSequence))
		{
			if (positions.TryGetValue(t6, out var pos3))
			{
				t6.position = pos3;
			}
		}
	}

	private void ToSmaBindingPose()
	{
	}

	private void FixBoneLength(Dictionary<Transform, float> boneLength)
	{
		foreach (Transform t in boneSequence)
		{
			if (t.childCount == 1 && boneLength.TryGetValue(t, out var length))
			{
				Transform c = t.GetChild(0);
				c.position = t.position + t.forward * length;
			}
		}
	}

	private void LoadTPose()
	{
		SetRotationLocal(RootBone.transform, Quaternion.LookRotation(Vector3.up, -Vector3.forward));
		foreach (KeyValuePair<string, Quaternion> kv in BindingPose.Rotations)
		{
			Transform t = RootBone.transform.FindDeepChild(kv.Key);
			if (t != null)
			{
				t.rotation = RootBone.transform.rotation * kv.Value;
			}
		}
		foreach (KeyValuePair<string, Vector3> kv2 in BindingPose.Positions)
		{
			Transform t2 = RootBone.transform.FindDeepChild(kv2.Key);
			if (t2 != null)
			{
				t2.position = RootBone.transform.TransformPoint(kv2.Value);
			}
		}
	}

	private void SetPositionLocal(Transform t, Vector3 at)
	{
		TransformPreservingChildren(t, delegate
		{
			t.position = at;
		});
	}

	private void SetRotationLocal(Transform t, Quaternion at)
	{
		TransformPreservingChildren(t, delegate
		{
			t.rotation = at;
		});
	}

	private void TransformPreservingChildren(Transform t, Action x)
	{
		TransformDisposition[] dispositions = new TransformDisposition[t.childCount];
		for (int i = 0; i < t.childCount; i++)
		{
			dispositions[i] = new TransformDisposition(t.GetChild(i));
		}
		x();
		for (int j = 0; j < t.childCount; j++)
		{
			dispositions[j].Apply(t.GetChild(j));
		}
	}
}
