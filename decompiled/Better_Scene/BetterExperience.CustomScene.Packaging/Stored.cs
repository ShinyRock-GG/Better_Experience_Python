using Newtonsoft.Json;

namespace BetterExperience.CustomScene.Packaging;

public class Stored
{
	[JsonIgnore]
	public string Id { get; set; }

	public bool Deleted { get; set; }
}
