namespace BetterExperience.Wrappers.Characters;

public class StringWeightArray
{
	private float[] weights;

	private string[] keys;

	public float this[int n]
	{
		get
		{
			return weights[n];
		}
		set
		{
			weights[n] = value;
		}
	}

	public float this[string n]
	{
		get
		{
			return weights[keys.IndexOf(n)];
		}
		set
		{
			weights[keys.IndexOf(n)] = value;
		}
	}

	public string[] Keys => keys;

	public StringWeightArray(string[] index)
	{
		keys = index;
		weights = new float[keys.Length];
	}

	public void Clear()
	{
		for (int i = 0; i < weights.Length; i++)
		{
			weights[i] = 0f;
		}
	}
}
