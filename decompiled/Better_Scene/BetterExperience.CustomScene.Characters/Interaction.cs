using System.Collections.Generic;
using System.Text;
using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Characters;

public class Interaction
{
	private List<BasicOperation> operations = new List<BasicOperation>();

	public string DisplayName { get; set; }

	public POIPosture TargetPosture { get; set; }

	public POIPosture SourcePosture { get; set; }

	public List<BasicOperation> Sequence => operations;

	public void Enqueue(BasicOperation basicOp)
	{
		operations.Add(basicOp);
	}

	public IEnumerator<BasicOperation> CreateSequence()
	{
		return operations.GetEnumerator();
	}

	public override string ToString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append("Interaction: ").AppendLine(DisplayName);
		sb.Append("  SourcePosture: ").AppendLine(SourcePosture.Id);
		sb.Append("  TargetPosture: ").AppendLine(TargetPosture.Id);
		sb.AppendLine("  Ops:");
		foreach (BasicOperation op in operations)
		{
			sb.Append("    ").AppendLine(op.ToString());
		}
		return sb.ToString();
	}
}
