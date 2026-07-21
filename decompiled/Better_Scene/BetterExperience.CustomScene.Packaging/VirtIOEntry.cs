using System.Collections.Generic;
using System.Linq;

namespace BetterExperience.CustomScene.Packaging;

public class VirtIOEntry
{
	public string Path { get; set; }

	public string Name { get; set; }

	public List<VirtIOAccessor> Accessors { get; set; } = new List<VirtIOAccessor>();

	public VirtIOAccessor WriteAccessor { get; set; }

	public IReadOnlyList<Package> Packages => Accessors.Select((VirtIOAccessor x) => x.Package).ToList();
}
