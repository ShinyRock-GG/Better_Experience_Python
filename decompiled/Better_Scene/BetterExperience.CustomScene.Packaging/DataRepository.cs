using System.Collections.Generic;
using System.Linq;

namespace BetterExperience.CustomScene.Packaging;

public class DataRepository
{
	private Dictionary<string, VirtIOAccessor> data = new Dictionary<string, VirtIOAccessor>();

	private string defaultDir;

	private string ext;

	private VirtIO root;

	public DataRepository(string extension, string defaultDir)
	{
		this.defaultDir = defaultDir;
		ext = "." + extension;
	}

	public void Init(VirtIO root)
	{
		this.root = root;
		foreach (VirtIOEntry e in root.Enumerate())
		{
			if (e.Name.EndsWith(ext) && e.Accessors.Count > 0)
			{
				data.Add(e.Name.Substring(0, e.Name.Length - ext.Length), e.Accessors.Last());
			}
		}
	}

	public byte[] Get(string name)
	{
		if (data.TryGetValue(name, out var vfs))
		{
			return vfs.Read();
		}
		return null;
	}
}
