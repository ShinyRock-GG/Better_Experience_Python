using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BetterExperience.CustomScene.Packaging;
using UnityEngine;

namespace BetterExperience.PyStory.Scripting;

public class PythonScriptRepository
{
	private string prefix = Path.DirectorySeparatorChar + "scripts";

	private List<string> autoImportScripts = new List<string>();

	private Dictionary<string, Func<string>> scripts = new Dictionary<string, Func<string>>();

	private Hash128 lastHash = default(Hash128);

	public ICollection<string> Scripts => scripts.Keys;

	public ICollection<string> AutoimportScripts => autoImportScripts;

	public void Init(VirtIO vFS)
	{
		scripts.Clear();
		foreach (VirtIOEntry e in vFS.Enumerate())
		{
			if (Matches(e.Path) && e.Name.EndsWith(".py"))
			{
				string module = Path.Combine(e.Path.Substring(prefix.Length), e.Name);
				string text = module;
				char directorySeparatorChar = Path.DirectorySeparatorChar;
				if (text.StartsWith(directorySeparatorChar.ToString()))
				{
					module = module.Substring(1);
				}
				scripts[module] = () => Encoding.UTF8.GetString(e.Accessors.Last().Read());
				if (!e.Packages.Last().Manifest.options.ContainsKey("pycs.stdlib"))
				{
					autoImportScripts.Add(module);
				}
			}
		}
		autoImportScripts.Sort();
	}

	private bool Matches(string name)
	{
		return name.ToLower().StartsWith(prefix);
	}

	public string GetScript(string path)
	{
		if (scripts.TryGetValue(path, out var a))
		{
			return a();
		}
		return null;
	}

	public bool IsChanged()
	{
		Hash128 hash = default(Hash128);
		foreach (Func<string> code in scripts.Values)
		{
			hash.Append(code());
		}
		bool changed = hash.CompareTo(lastHash) != 0;
		lastHash = hash;
		return changed;
	}
}
