namespace BetterExperience.CustomScene.Characters;

public class SeqFragment<T>
{
	private T[] keys;

	public SeqClip Parent { get; private set; }

	internal (float t0, float t1, T c0, T c1) FindConfiguration(float seqTime)
	{
		int index = 0;
		for (int i = 0; i < Parent.Timeline.Length; i++)
		{
			index = i;
			if (seqTime < Parent.Timeline[i])
			{
				break;
			}
		}
		if (index == 0)
		{
			return (t0: seqTime, t1: seqTime, c0: keys[0], c1: keys[0]);
		}
		return (t0: Parent.Timeline[index - 1], t1: Parent.Timeline[index], c0: keys[index - 1], c1: keys[index]);
	}
}
