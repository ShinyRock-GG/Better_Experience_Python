using System.Collections.Generic;
using System.Linq;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.CustomScene.Poser;

public class PosturePoseCollection
{
	private Dictionary<PoseAnimationClip, List<PoseAnimationClip>> derivatives = new Dictionary<PoseAnimationClip, List<PoseAnimationClip>>();

	public Dictionary<string, PoseAnimationClip> AllClips { get; set; } = new Dictionary<string, PoseAnimationClip>();

	public List<PoseAnimationClip> InteractivePoses { get; set; } = new List<PoseAnimationClip>();

	public List<PoseAnimationClip> IdlePoses { get; set; } = new List<PoseAnimationClip>();

	public List<PoseAnimationClip> TransitionPoses { get; set; } = new List<PoseAnimationClip>();

	public PoseAnimationClip PostureClip { get; private set; }

	public Dictionary<PoseAnimationClip, InteractionDescriptor> ClipDescriptors { get; } = new Dictionary<PoseAnimationClip, InteractionDescriptor>();

	public Posture Posture { get; }

	public Dictionary<(string, string), List<PoseAnimationClip>> TransitionClips { get; private set; } = new Dictionary<(string, string), List<PoseAnimationClip>>();

	public PosturePoseCollection(Posture posture)
	{
		Posture = posture;
		PostureClip = GetPoseData("Binding", "default", InteractionDescriptor.BINDING);
		PostureClip.IsIdle = true;
		PostureClip.IsGenerated = true;
		BoneConfiguration copy = new BoneConfiguration(posture.Configuration)
		{
			RootRotation = Quaternion.identity,
			RootOffset = Vector3.zero
		};
		PostureClip.AddFrameData(0, copy);
		InteractivePoses.Add(PostureClip);
	}

	public PoseAnimationClip GetPoseData(string poseName, string variant, InteractionDescriptor descriptor)
	{
		string uname = poseName + ".";
		uname += variant;
		if (!AllClips.TryGetValue(uname, out var pose))
		{
			pose = new PoseAnimationClip(Posture, poseName, variant);
			pose.FullName = uname;
			pose.UniqueName = Posture.Name + "." + uname;
			AddClip(pose, descriptor);
		}
		return pose;
	}

	private void PurgeClip(PoseAnimationClip prevClip)
	{
		AllClips.Remove(prevClip.UniqueName);
		IdlePoses.Remove(prevClip);
		InteractivePoses.Remove(prevClip);
		ClipDescriptors.Remove(prevClip);
		foreach (List<PoseAnimationClip> tcs in TransitionClips.Values)
		{
			tcs.Remove(prevClip);
		}
	}

	public void AddClip(PoseAnimationClip clip, InteractionDescriptor descriptor)
	{
		if (AllClips.TryGetValue(clip.UniqueName, out var prevClip))
		{
			Logger plogger = new Logger
			{
				Prefix = "[Posture#" + clip.Posture.Id + "] "
			};
			plogger.Info("Unloading clip {0}", prevClip.FullName);
			PurgeClip(prevClip);
			if (derivatives.TryGetValue(prevClip, out var deriven))
			{
				derivatives.Remove(prevClip);
				foreach (PoseAnimationClip d in deriven)
				{
					plogger.Info("Unloading dervied clip {0}", d.FullName);
					PurgeClip(d);
				}
			}
		}
		if (descriptor == null)
		{
			Logger.Global.Error("Clip {0} has no descriptor", clip.UniqueName);
			descriptor = new InteractionDescriptor();
		}
		AllClips[clip.UniqueName] = clip;
		Logger logger = new Logger
		{
			Prefix = "[ClipLoader#" + clip.UniqueName + "] "
		};
		if (clip.Name == "Idle")
		{
			IdlePoses.Add(clip);
			clip.IsIdle = true;
		}
		else
		{
			InteractivePoses.Add(clip);
		}
		clip.RootMotionType = descriptor.RootMotionType;
		ClipDescriptors[clip] = descriptor;
		clip.Descriptor = descriptor;
		if (descriptor.MuscleOverride != null)
		{
			foreach (BoneConfiguration frame in clip.Frames)
			{
				if (frame.Muscles != null)
				{
					descriptor.MuscleOverride.ForEach(delegate(KeyValuePair<string, MuscleConfig> kv)
					{
						frame.Muscles[kv.Key] = kv.Value;
					});
				}
			}
		}
		if (descriptor.Type == InteractionType.transition)
		{
			if (descriptor.SupportsTransitions == null)
			{
				return;
			}
			int i = 0;
			{
				foreach (InteractionDescriptor.InteractionTransition t in descriptor.SupportsTransitions)
				{
					i++;
					TransitionClips.GetValueOrAdd((t.From, t.To), () => new List<PoseAnimationClip>()).Add(clip);
					if (t.Reversible)
					{
						InteractionDescriptor revDesc = new InteractionDescriptor(descriptor);
						revDesc.DisplayName = descriptor.CancelDisplayName;
						revDesc.CancelDisplayName = descriptor.DisplayName;
						revDesc.SupportsTransitions.Clear();
						revDesc.SupportsTransitions.Add(new InteractionDescriptor.InteractionTransition
						{
							From = t.To,
							To = t.From
						});
						PoseAnimationClip reverseClip = new PoseAnimationClip(Posture, clip.Name + "_Rev" + i, clip.Variant);
						reverseClip.IsGenerated = true;
						if (clip.ReverseInto(reverseClip))
						{
							Logger.Global.Info("Created reverse clip {0} for {1}", reverseClip.FullName, clip.FullName);
							AddClip(reverseClip, revDesc);
						}
						else
						{
							Logger.Global.Error("Unable to create reverse clip {0} for {1}", reverseClip.Frames, clip.FullName);
						}
						derivatives.GetValueOrAdd(clip, () => new List<PoseAnimationClip>()).Add(reverseClip);
					}
				}
				return;
			}
		}
		if (descriptor.Type != InteractionType.subpose)
		{
			return;
		}
		logger.Info("Loading subpose");
		if (descriptor.SupportsTransitions == null)
		{
			return;
		}
		foreach (InteractionDescriptor.InteractionTransition tr in descriptor.SupportsTransitions)
		{
			if (tr.From == null)
			{
				logger.Error("Missing mandatory transition field From");
				continue;
			}
			if (tr.As == null)
			{
				logger.Error("Missing mandatory transition field As");
				continue;
			}
			if (tr.As.DisplayName != null)
			{
				PoseAnimationClip transitionClip = new PoseAnimationClip(clip.Posture, "TrGen_" + tr.From + "_" + clip.Name, "generated");
				transitionClip.IsGenerated = true;
				transitionClip.Frames.Add(clip.Frames.First());
				InteractionDescriptor transitionDesc = new InteractionDescriptor();
				transitionDesc.Type = InteractionType.transition;
				transitionDesc.DisplayName = tr.As.DisplayName;
				transitionDesc.SupportsTransitions.Add(new InteractionDescriptor.InteractionTransition
				{
					From = tr.From,
					To = clip.Name
				});
				AddClip(transitionClip, transitionDesc);
				derivatives.GetValueOrAdd(clip, () => new List<PoseAnimationClip>()).Add(transitionClip);
			}
			if (tr.As.CancelDisplayName != null)
			{
				PoseAnimationClip transitionClip2 = new PoseAnimationClip(clip.Posture, "TrGen_" + clip.Name + "_" + tr.From, "generated");
				transitionClip2.IsGenerated = true;
				transitionClip2.Frames.Add(clip.Frames.First());
				InteractionDescriptor transitionDesc2 = new InteractionDescriptor();
				transitionDesc2.Type = InteractionType.transition;
				transitionDesc2.DisplayName = tr.As.CancelDisplayName;
				transitionDesc2.SupportsTransitions.Add(new InteractionDescriptor.InteractionTransition
				{
					From = clip.Name,
					To = tr.From
				});
				AddClip(transitionClip2, transitionDesc2);
				derivatives.GetValueOrAdd(clip, () => new List<PoseAnimationClip>()).Add(transitionClip2);
			}
		}
	}

	public List<PoseAnimationClip> FindClips(string name)
	{
		return AllClips.Values.Where((PoseAnimationClip c) => c.Name == name || c.UniqueName == name || c.FullName == name).ToList();
	}

	internal IEnumerable<PoseAnimationClip> EnumerateTransitions(PoseAnimationClip animatedPose)
	{
		if (animatedPose.IsIdle)
		{
			foreach (PoseAnimationClip clip in InteractivePoses)
			{
				if (clip.IsIdle || clip.IsGenerated)
				{
					continue;
				}
				if (ClipDescriptors.TryGetValue(clip, out var desc))
				{
					if (desc.Type != InteractionType.pose)
					{
						continue;
					}
				}
				else
				{
					Logger.Global.Error("Missing desc at {0}", clip.UniqueName);
				}
				yield return clip;
			}
			yield break;
		}
		InteractionDescriptor currentDesc = ClipDescriptors.GetValueOrDefault(animatedPose, () => new InteractionDescriptor());
		foreach (PoseAnimationClip clip2 in InteractivePoses)
		{
			if (ClipDescriptors.TryGetValue(clip2, out var _) && TransitionClips.TryGetValue((animatedPose.Name, clip2.Name), out var transitions))
			{
				FixTransitionKeyframeData(clip2, transitions);
				yield return clip2;
			}
		}
		if (currentDesc.Type == InteractionType.subpose)
		{
			yield break;
		}
		foreach (PoseAnimationClip idlePose in IdlePoses)
		{
			yield return idlePose;
		}
		if (IdlePoses.Count == 0)
		{
			yield return PostureClip;
		}
	}

	private void FixTransitionKeyframeData(PoseAnimationClip target, List<PoseAnimationClip> transitions)
	{
		foreach (PoseAnimationClip c in transitions)
		{
			if (c.Frames.Count == 0)
			{
				BoneConfiguration bc = ((target.States.Count > 0) ? target.Frames[target.States[0].Key] : target.Frames[0]);
				c.Frames.Add(bc);
			}
		}
	}
}
