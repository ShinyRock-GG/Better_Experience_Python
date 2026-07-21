using System.Collections.Generic;

namespace BetterExperience.CustomScene.Characters;

public class MultitrackAninmationClip
{
	public List<string> Plugins { get; set; } = new List<string>();

	public float[] Timeline { get; set; }

	public Dictionary<string, AnimationTrack> Tracks { get; set; }
}
