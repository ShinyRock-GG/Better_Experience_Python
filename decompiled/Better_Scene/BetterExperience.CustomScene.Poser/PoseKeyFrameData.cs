using System.Linq;

namespace BetterExperience.CustomScene.Poser;

public class PoseKeyFrameData
{
	public int frame { get; set; }

	public string duration { get; set; }

	public float fadein { get; set; }

	public string next { get; set; }

	public string label { get; set; }

	public PoseKeyFrameData()
	{
	}

	public PoseKeyFrameData(PoseAnimationClip clip, PoseAnimationFrame data)
	{
		frame = data.Key;
		if (data.MinDuration == data.MaxDuration)
		{
			duration = data.MinDuration.ToString();
		}
		else
		{
			duration = $"{data.MinDuration}:{data.MaxDuration}";
		}
		fadein = data.FadeIn;
		if (data.Next.Count > 0)
		{
			next = string.Join(",", data.Next.Select((PoseAnimationFrame x) => clip.States.IndexOf(x)).ToArray());
		}
		label = data.Label;
	}
}
