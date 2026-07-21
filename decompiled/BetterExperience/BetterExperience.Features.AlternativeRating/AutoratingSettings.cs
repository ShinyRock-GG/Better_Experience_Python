using System.Collections.Generic;

namespace BetterExperience.Features.AlternativeRating;

internal class AutoratingSettings
{
	public float MaxError { get; set; } = 3f;

	public Dictionary<string, float> SpecificMaxError { get; set; } = new Dictionary<string, float>();

	public AutoratingSettings()
	{
		float pr = MaxError;
		SpecificMaxError["summarizing"] = pr;
		SpecificMaxError["angerManagement"] = pr;
		SpecificMaxError["servicing"] = pr;
		SpecificMaxError["slutness"] = pr;
		SpecificMaxError["painTolerance"] = pr;
		SpecificMaxError["exhibitionism"] = pr;
	}
}
