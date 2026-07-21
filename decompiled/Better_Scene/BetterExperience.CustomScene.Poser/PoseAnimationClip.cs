using System.Collections.Generic;
using BetterExperience.Utils;

namespace BetterExperience.CustomScene.Poser;

public class PoseAnimationClip
{
	public Posture Posture { get; }

	public string FullName { get; set; }

	public string Name { get; set; }

	public string UniqueName { get; set; }

	public string Variant { get; set; }

	public List<BoneConfiguration> Frames { get; set; } = new List<BoneConfiguration>();

	public List<PoseAnimationFrame> States { get; set; } = new List<PoseAnimationFrame>();

	public bool IsIdle { get; set; }

	public bool IsGenerated { get; set; }

	public bool IsNativeIdle { get; set; }

	public RootMotionType RootMotionType { get; set; }

	public bool NoCache { get; set; }

	public bool Cyclic
	{
		get
		{
			if (States.Count <= 0)
			{
				return false;
			}
			return States[States.Count - 1].Next.Count != 0;
		}
	}

	public InteractionDescriptor Descriptor { get; set; }

	public Dictionary<string, PoseAnimationFrame> Labels { get; set; } = new Dictionary<string, PoseAnimationFrame>();

	public List<string> LabelOrder { get; } = new List<string>();

	public EffectorData EffectorData { get; set; }

	public PlayerStateData PlayerState { get; set; }

	public PoseAnimationClip(Posture posture, string name, string variant)
	{
		Posture = posture;
		Name = name;
		FullName = name + "." + variant;
		UniqueName = posture.Id + "." + FullName;
		Variant = variant;
	}

	public PoseAnimationClip(PoseAnimationClip x)
	{
		Posture = x.Posture;
		FullName = x.FullName;
		Name = x.Name;
		UniqueName = x.UniqueName;
		IsIdle = x.IsIdle;
		Variant = x.Variant;
		RootMotionType = x.RootMotionType;
		x.States.ForEach(States.Add);
		x.Frames.ForEach(Frames.Add);
		x.Labels.ForEach(Labels.Add);
		x.LabelOrder.AddRange(LabelOrder);
		if (x.EffectorData != null)
		{
			EffectorData = new EffectorData(x.EffectorData);
		}
	}

	public void AddFrameData(int iFrame, BoneConfiguration poseData)
	{
		while (Frames.Count <= iFrame)
		{
			Frames.Add(null);
		}
		Frames[iFrame] = poseData;
	}

	internal bool ReverseInto(PoseAnimationClip target)
	{
		Logger logger = new Logger
		{
			Prefix = "[PoseAnimationClip.Reverse#" + UniqueName + "] "
		};
		if (Frames.Count == 0)
		{
			logger.Error("Unable to create reverse {0}: empty clip", FullName);
			return false;
		}
		if (States.Count == 0)
		{
			target.Frames.Add(Frames[0]);
			return true;
		}
		HashSet<PoseAnimationFrame> closedSet = new HashSet<PoseAnimationFrame>();
		List<PoseAnimationFrame> path = new List<PoseAnimationFrame>();
		PoseAnimationFrame state = States[0];
		while (state.Next.Count != 0)
		{
			if (!closedSet.Add(state))
			{
				logger.Error("Unable to create reverse: loop detected");
				return false;
			}
			PoseAnimationFrame targetState = new PoseAnimationFrame(state);
			path.Insert(0, targetState);
			targetState.Next.Clear();
			if (path.Count > 1)
			{
				targetState.Next.Add(path[1]);
			}
			state = state.Next[0];
			targetState.FadeIn = state.FadeIn;
		}
		PoseAnimationFrame finalState = new PoseAnimationFrame(state);
		finalState.FadeIn = 0f;
		finalState.Next.Clear();
		if (path.Count > 0)
		{
			finalState.Next.Add(path[0]);
		}
		path.Insert(0, finalState);
		target.Frames.AddRange(Frames);
		target.States.AddRange(path);
		return true;
	}
}
