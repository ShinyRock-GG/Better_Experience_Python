using System.Linq;
using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class SeqClip
{
	private float[] timeline;

	private AnimationKeyFrame[] armatureKeys;

	public PoseAnimationClip source { get; private set; }

	public int[] Indices { get; private set; }

	public bool Additive { get; private set; }

	public float loopTimeIndex { get; private set; }

	public float length => timeline.Last();

	public float fadeIn => timeline[0];

	public float[] Timeline => timeline;

	public string label { get; private set; }

	public bool cyclic { get; set; }

	public bool pingPongCycle { get; set; }

	public SeqClip(PoseAnimationClip source, bool additive, float[] timeline, AnimationKeyFrame[] armatureKeys, int[] indices, float loopTimeIndex, string label)
	{
		this.source = source;
		Additive = additive;
		this.timeline = timeline;
		this.armatureKeys = armatureKeys;
		Indices = indices;
		this.loopTimeIndex = loopTimeIndex;
		this.label = label;
	}

	internal (float t0, float t1, AnimationKeyFrame c0, AnimationKeyFrame c1) FindConfiguration(float seqTime)
	{
		int index = FindIndex(seqTime);
		if (index == 0)
		{
			return (t0: seqTime, t1: seqTime, c0: armatureKeys[0], c1: armatureKeys[0]);
		}
		return (t0: timeline[index - 1], t1: timeline[index], c0: armatureKeys[index - 1], c1: armatureKeys[index]);
	}

	public int FindIndex(float seqTime)
	{
		int index = 0;
		for (int i = 0; i < timeline.Length; i++)
		{
			index = i;
			if (seqTime < timeline[i])
			{
				break;
			}
		}
		return index;
	}
}
