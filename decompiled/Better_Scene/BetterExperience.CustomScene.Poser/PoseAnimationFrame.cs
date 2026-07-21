using System.Collections.Generic;

namespace BetterExperience.CustomScene.Poser;

public class PoseAnimationFrame
{
	public static readonly PoseAnimationFrame DEFAULT = new PoseAnimationFrame();

	public int Key { get; set; }

	public float MinDuration { get; set; }

	public float MaxDuration { get; set; }

	public float FadeIn { get; set; } = 1f;

	public string Label { get; set; }

	public List<PoseAnimationFrame> Next { get; set; } = new List<PoseAnimationFrame>();

	public PoseAnimationFrame()
	{
	}

	public PoseAnimationFrame(PoseAnimationFrame copy)
	{
		Key = copy.Key;
		MinDuration = copy.MinDuration;
		MaxDuration = copy.MaxDuration;
		FadeIn = copy.FadeIn;
		Label = copy.Label;
		Next.AddRange(copy.Next);
	}
}
