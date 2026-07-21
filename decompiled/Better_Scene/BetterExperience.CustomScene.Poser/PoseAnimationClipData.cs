using System.Collections.Generic;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PoseAnimationClipData : Stored
{
	public List<BoneDispositionData> keyFrames = new List<BoneDispositionData>();

	public string Name { get; set; }

	public string UniqueName { get; set; }

	public string FullName { get; set; }

	public List<PoseKeyFrameData> frames { get; set; } = new List<PoseKeyFrameData>();

	public IK2Data ik2 { get; set; }

	public PlayerStateData PlayerState { get; set; }

	public PoseAnimationClipData()
	{
	}

	public PoseAnimationClipData(PoseAnimationClip clip)
	{
		Name = clip.Name;
		UniqueName = clip.UniqueName;
		FullName = clip.FullName;
		foreach (BoneConfiguration f in clip.Frames)
		{
			keyFrames.Add(new BoneDispositionData(f));
		}
		for (int i = 1; i < keyFrames.Count; i++)
		{
			keyFrames[i].Bones = null;
		}
		foreach (PoseAnimationFrame f2 in clip.States)
		{
			frames.Add(new PoseKeyFrameData(clip, f2));
		}
		if (clip.EffectorData != null)
		{
			ik2 = new IK2Data(clip.EffectorData);
		}
		PlayerState = clip.PlayerState;
	}

	internal PoseAnimationClip ToClip(string id, PoseManager poseManager)
	{
		for (int i = 1; i < keyFrames.Count; i++)
		{
			if (keyFrames[i].Bones == null)
			{
				keyFrames[i].Bones = keyFrames[0].Bones;
			}
		}
		string[] idParts = id.Split(new char[1] { '.' });
		string postureId = idParts[0];
		if (poseManager.Postures.TryGetValue(postureId, out var posture))
		{
			PoseAnimationClip clip = new PoseAnimationClip(posture, idParts[1], idParts[2]);
			foreach (BoneDispositionData k in keyFrames)
			{
				clip.AddFrameData(clip.Frames.Count, k.AsBoneTranform());
			}
			WriteInto(clip);
			if (ik2 != null)
			{
				clip.EffectorData = ik2.AsEffectorData();
			}
			clip.PlayerState = PlayerState;
			return clip;
		}
		Logger.Global.Error("Unable to find posture {0}", postureId);
		return null;
	}

	internal void WriteInto(PoseAnimationClip clip)
	{
		if (frames == null)
		{
			return;
		}
		List<BoneConfiguration> Frames = clip.Frames;
		PoseAnimationFrame[] framesFdw = new PoseAnimationFrame[frames.Count].Fill(() => new PoseAnimationFrame());
		int counter = 0;
		foreach (PoseKeyFrameData f in frames)
		{
			PoseAnimationFrame frameData = framesFdw[counter++];
			if (f.frame >= Frames.Count || Frames[f.frame] == null)
			{
				frameData.Key = -1;
				Logger.Global.Error("No such frame {0}", f.frame);
			}
			else
			{
				frameData.Key = f.frame;
			}
			frameData.Label = f.label;
			if (f.fadein < 0f)
			{
				f.fadein = 0f;
			}
			frameData.FadeIn = f.fadein;
			if (f.duration != null)
			{
				float d;
				if (f.duration.Contains(":"))
				{
					string[] parts = f.duration.Split(new char[1] { ':' });
					if (parts.Length > 1 && float.TryParse(parts[0], out var a) && float.TryParse(parts[1], out var b))
					{
						frameData.MinDuration = Mathf.Max(Mathf.Min(a, b), 0f);
						frameData.MaxDuration = Mathf.Max(a, b, 0f);
					}
				}
				else if (float.TryParse(f.duration, out d))
				{
					d = (frameData.MaxDuration = (frameData.MinDuration = Mathf.Max(d, 0f)));
				}
			}
			if (f.next != null)
			{
				int i2;
				if (f.next.Contains(","))
				{
					string[] parts2 = f.next.Split(new char[1] { ',' });
					string[] array = parts2;
					foreach (string p in array)
					{
						if (int.TryParse(p, out var i) && i >= 0 && i < frames.Count)
						{
							frameData.Next.Add(framesFdw[i]);
							continue;
						}
						Logger.Global.Error("Bad frame num {0} at {1}", p, f.next);
					}
				}
				else if (int.TryParse(f.next, out i2) && i2 >= 0 && i2 < frames.Count)
				{
					frameData.Next.Add(framesFdw[i2]);
				}
			}
			clip.States.Add(frameData);
			if (frameData.Label != null && !clip.Labels.ContainsKey(frameData.Label))
			{
				clip.Labels[frameData.Label] = frameData;
				clip.LabelOrder.Add(frameData.Label);
			}
		}
	}
}
