using System.Collections.Generic;
using System.Linq;

namespace BetterExperience.Features.Lexicon;

public class ADialogInfo : Dictionary<string, float>
{
	public float C => this.First().Value;

	public string T => this.First().Key;

	public ADialogInfo()
	{
	}

	public ADialogInfo(float c, string t)
	{
		base[t] = c;
	}
}
