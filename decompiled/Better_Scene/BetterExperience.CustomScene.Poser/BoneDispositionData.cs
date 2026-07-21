using System.Collections.Generic;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class BoneDispositionData : Stored
{
	public List<string> Bones { get; set; }

	public List<float[]> Rotations { get; set; }

	public List<float[]> Positions { get; set; }

	public Dictionary<string, MuscleConfig> Muscles { get; set; }

	public Dictionary<string, IKTargetingData> IKTargeting { get; set; }

	public float[] HipsOffset { get; set; }

	public float[] RootOffset { get; set; }

	public float[] RootRotation { get; set; }

	public GesturesData Gestures { get; set; }

	public BoneDispositionData()
	{
	}

	public BoneDispositionData(BoneConfiguration f)
	{
		ReadFrom(f);
	}

	protected void ReadFrom(BoneConfiguration data)
	{
		Bones = new List<string>();
		Rotations = new List<float[]>();
		if (data.Positions.Count > 0)
		{
			Positions = new List<float[]>();
		}
		foreach (KeyValuePair<string, Quaternion> kv in data.Rotations)
		{
			Bones.Add(kv.Key);
			Quaternion rot = kv.Value;
			Rotations.Add(new float[4] { rot.x, rot.y, rot.z, rot.w });
			if (data.Positions.Count > 0)
			{
				Positions.Add(data.Positions[kv.Key].AsFloatArray());
			}
		}
		Muscles = new Dictionary<string, MuscleConfig>();
		data.Muscles.ForEach(delegate(KeyValuePair<string, MuscleConfig> keyValuePair)
		{
			Muscles.Add(keyValuePair.Key, keyValuePair.Value);
		});
		HipsOffset = data.HipOffset.AsFloatArray();
		RootOffset = data.RootOffset.AsFloatArray();
		RootRotation = data.RootRotation.AsFloatArray();
		if (data.IKTargets != null)
		{
			IKTargeting = new Dictionary<string, IKTargetingData>();
			foreach (KeyValuePair<string, DynamicIKTarget> kv2 in data.IKTargets)
			{
				IKTargeting[kv2.Key] = new IKTargetingData(kv2.Value);
			}
		}
		if (data.Gestures != null)
		{
			Gestures = data.Gestures;
		}
	}

	protected void WriteInto(BoneConfiguration cfg)
	{
		Logger l = new Logger();
		BoneRotationData rotations = cfg.Rotations;
		for (int i = 0; i < Bones.Count; i++)
		{
			string key = Bones[i];
			float[] rot = Rotations[i];
			if (rot != null)
			{
				rotations[key] = new Quaternion(rot[0], rot[1], rot[2], rot[3]);
			}
			if (Positions != null && Positions[i] != null)
			{
				cfg.Positions[key] = Positions[i].AsVector3();
			}
		}
		if (Muscles != null)
		{
			Muscles.ForEach(delegate(KeyValuePair<string, MuscleConfig> keyValuePair)
			{
				cfg.Muscles.Add(keyValuePair.Key, keyValuePair.Value);
			});
		}
		if (HipsOffset != null)
		{
			cfg.HipOffset = HipsOffset.AsVector3();
		}
		if (RootOffset != null)
		{
			cfg.RootOffset = RootOffset.AsVector3();
		}
		if (RootRotation != null)
		{
			cfg.RootRotation = new Quaternion(RootRotation[0], RootRotation[1], RootRotation[2], RootRotation[3]);
		}
		if (IKTargeting != null)
		{
			foreach (KeyValuePair<string, IKTargetingData> kv in IKTargeting)
			{
				cfg.IKTargets[kv.Key] = kv.Value.Convert();
			}
		}
		if (Gestures != null)
		{
			cfg.Gestures = Gestures;
		}
	}

	internal BoneConfiguration AsBoneTranform()
	{
		BoneConfiguration result = new BoneConfiguration();
		WriteInto(result);
		return result;
	}

	public void AddRotationData(string boneName, float[] vs)
	{
		if (Bones == null)
		{
			Bones = new List<string>();
		}
		if (Rotations == null)
		{
			Rotations = new List<float[]>();
		}
		int index = Bones.IndexOf(boneName);
		if (index == -1)
		{
			Bones.Add(boneName);
			Rotations.Add(vs);
		}
		else
		{
			Rotations[index] = vs;
		}
	}

	public void AddPositionData(string boneName, float[] value)
	{
		if (Positions == null)
		{
			Positions = new List<float[]>();
		}
		int index = Bones.IndexOf(boneName);
		if (index == -1 || index == Rotations.Count - 1)
		{
			Positions.Add(value);
		}
		else
		{
			Positions[index] = value;
		}
	}
}
