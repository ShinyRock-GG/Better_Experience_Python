using System.Collections.Generic;
using System.IO;
using BetterExperience.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser.Formats;

internal class SimpleVAMExtractor
{
	private Logger logger = new Logger(typeof(SimpleVAMExtractor));

	private JObject root;

	private Dictionary<string, string> bonesMap = new Dictionary<string, string>();

	private PoseAnimationClipData clipData = new PoseAnimationClipData();

	public PoseAnimationClipData Data => clipData;

	public SimpleVAMExtractor(string file)
	{
		bonesMap["head"] = "CC_Base_Head";
		bonesMap["neck"] = "CC_Base_NeckTwist02";
		bonesMap["pelvis"] = "CC_Base_Hip";
		bonesMap["hip"] = "CC_Base_Pelvis";
		bonesMap["lThigh"] = "CC_Base_Thigh.L";
		bonesMap["rThigh"] = "CC_Base_Thigh.R";
		bonesMap["lFoot"] = "CC_Base_Foot.L";
		bonesMap["rFoot"] = "CC_Base_Foot.R";
		bonesMap["rShin"] = "CC_Base_Calf.R";
		bonesMap["lShin"] = "CC_Base_Calf.L";
		bonesMap["rKnee"] = "CC_Base_Calf.R";
		bonesMap["lKnee"] = "CC_Base_Calf.L";
		bonesMap["rHand"] = "CC_Base_Hand.R";
		bonesMap["lHand"] = "CC_Base_Hand.L";
		bonesMap["rElbow"] = "CC_Base_Elbow.R";
		bonesMap["lElbow"] = "CC_Base_Elbow.L";
		bonesMap["abdomen2"] = "CC_Base_Spine02";
		bonesMap["abdomen"] = "CC_Base_Spine01";
		bonesMap["rShoulder"] = "CC_Base_Clavicle.R";
		bonesMap["lShoulder"] = "CC_Base_Clavicle.L";
		bonesMap["lArm"] = "CC_Base_Upperarm.L";
		bonesMap["rArm"] = "CC_Base_Upperarm.R";
		bonesMap["rElbow"] = "CC_Base_Forearm.R";
		bonesMap["lElbow"] = "CC_Base_Forearm.L";
		root = JObject.Parse(File.ReadAllText(file));
		ParseBlob();
	}

	private void ParseBlob()
	{
		JArray atoms = root.Value<JArray>("atoms");
		if (atoms.HasValues)
		{
			LoadAtoms(atoms);
		}
	}

	private void LoadAtoms(JArray atoms)
	{
		foreach (JObject atom in atoms)
		{
			string type = atom.Value<string>("type");
			if (type == "Person")
			{
				JArray storables = atom.Value<JArray>("storables");
				if (storables.HasValues)
				{
					LoadPerson(storables);
				}
			}
		}
	}

	private void LoadPerson(JArray storables)
	{
		InitPose(storables);
		foreach (JObject obj in storables)
		{
			string id = obj.Value<string>("id");
			if (id != null && id.EndsWith("Animation"))
			{
				string bone = id.Substring(0, id.Length - "Animation".Length);
				if (bonesMap.TryGetValue(bone, out var smaBone))
				{
					GenerateBoneAnim(smaBone, obj.Value<JArray>("steps"));
				}
			}
		}
	}

	private void InitPose(JArray storables)
	{
		InitFrame(0, 0f);
		BoneDispositionData frame = clipData.keyFrames[0];
		foreach (JObject obj in storables)
		{
			string id = obj.Value<string>("id");
			if (id.EndsWith("Control"))
			{
				id = id.Substring(0, id.Length - "Control".Length);
				if (id != null && bonesMap.TryGetValue(id, out var smaBone))
				{
					Vector3 rotation = ReadVector3(obj.Value<JObject>("localRotation"));
					frame.AddRotationData(smaBone, Quaternion.Euler(rotation).AsFloatArray());
					Vector3 position = ReadVector3(obj.Value<JObject>("localPosition"));
					frame.AddPositionData(smaBone, position.AsFloatArray());
				}
				else
				{
					logger.Error("Missing bone mapping {0}", id);
				}
			}
		}
	}

	private Vector3 ReadVector3(JObject obj)
	{
		return new Vector3(obj.Value<float>("x"), obj.Value<float>("y"), obj.Value<float>("z"));
	}

	private Quaternion ReadQuaternion(JObject obj)
	{
		return new Quaternion(obj.Value<float>("x"), obj.Value<float>("y"), obj.Value<float>("z"), obj.Value<float>("w"));
	}

	private void GenerateBoneAnim(string smaBone, JArray steps)
	{
		float lastTime = 0f;
		for (int i = 0; i < steps.Count; i++)
		{
			JObject step = steps.Value<JObject>(i);
			float time = step.Value<float>("timeStep");
			Quaternion rot = ReadQuaternion(step.Value<JObject>("rotation"));
			Vector3 pos = ReadVector3(step.Value<JObject>("position"));
			EmitBoneFrame(i + 1, smaBone, time - lastTime, rot.AsFloatArray(), pos.AsFloatArray());
			lastTime = time;
		}
	}

	private void InitFrame(int frame, float time)
	{
		if (clipData.frames.Count != frame)
		{
			return;
		}
		PoseKeyFrameData data = new PoseKeyFrameData();
		clipData.frames.Add(data);
		BoneDispositionData disposition = new BoneDispositionData();
		clipData.keyFrames.Add(disposition);
		if (clipData.frames.Count > 1)
		{
			clipData.frames[clipData.frames.Count - 2].next = (clipData.frames.Count - 1).ToString();
		}
		data.fadein = time;
		data.frame = frame;
		if (frame != 0)
		{
			BoneDispositionData initial = clipData.keyFrames[0];
			disposition.Bones = new List<string>();
			disposition.Bones.AddRange(initial.Bones);
			disposition.Positions = new List<float[]>();
			disposition.Positions.AddRange(initial.Positions);
			for (int i = 0; i < disposition.Positions.Count; i++)
			{
				disposition.Positions[i] = null;
			}
			disposition.Rotations = new List<float[]>();
			disposition.Rotations.AddRange(initial.Rotations);
			for (int j = 0; j < disposition.Rotations.Count; j++)
			{
				disposition.Rotations[j] = null;
			}
		}
	}

	private void EmitBoneFrame(int frame, string smaBone, float time, float[] rot, float[] pos)
	{
		InitFrame(frame, time);
		BoneDispositionData key = clipData.keyFrames[frame];
		key.AddRotationData(smaBone, rot);
		key.AddPositionData(smaBone, pos);
	}
}
