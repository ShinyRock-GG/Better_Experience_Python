using System;

namespace BetterExperience.CustomScene.Poser;

public class MuscleConfig : IEquatable<MuscleConfig>
{
	public float Pin { get; set; }

	public float Spring { get; set; }

	public float Damper { get; set; }

	public bool IsSupport { get; set; }

	public bool CanInteract { get; set; }

	public bool CanReact { get; set; }

	public override bool Equals(object obj)
	{
		return Equals(obj as MuscleConfig);
	}

	public bool Equals(MuscleConfig other)
	{
		if (other != null && Pin == other.Pin && Spring == other.Spring && Damper == other.Damper && IsSupport == other.IsSupport && CanInteract == other.CanInteract)
		{
			return CanReact == other.CanReact;
		}
		return false;
	}

	public override int GetHashCode()
	{
		int hashCode = 988482875;
		hashCode = hashCode * -1521134295 + Pin.GetHashCode();
		hashCode = hashCode * -1521134295 + Spring.GetHashCode();
		hashCode = hashCode * -1521134295 + Damper.GetHashCode();
		hashCode = hashCode * -1521134295 + IsSupport.GetHashCode();
		hashCode = hashCode * -1521134295 + CanInteract.GetHashCode();
		return hashCode * -1521134295 + CanReact.GetHashCode();
	}
}
