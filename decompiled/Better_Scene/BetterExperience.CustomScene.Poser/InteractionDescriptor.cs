using System.Collections.Generic;
using BetterExperience.CustomScene.Packaging;

namespace BetterExperience.CustomScene.Poser;

public class InteractionDescriptor : Stored
{
	public class InteractionTransition
	{
		public string From { get; set; }

		public string To { get; set; }

		public bool Reversible { get; set; }

		public InteractionDescriptor As { get; set; }
	}

	public static InteractionDescriptor BINDING => new InteractionDescriptor
	{
		Type = InteractionType.idle
	};

	public InteractionType Type { get; set; } = InteractionType.pose;

	public List<InteractionTransition> SupportsTransitions { get; set; } = new List<InteractionTransition>();

	public Dictionary<string, MuscleConfig> MuscleOverride { get; set; }

	public string DisplayName { get; set; }

	public string CancelDisplayName { get; set; }

	public RootMotionType RootMotionType { get; set; }

	public List<string> Tags { get; set; }

	public InteractionDescriptor()
	{
	}

	public InteractionDescriptor(InteractionDescriptor descriptor)
	{
		Type = descriptor.Type;
		SupportsTransitions.AddRange(descriptor.SupportsTransitions);
		DisplayName = descriptor.DisplayName;
		CancelDisplayName = descriptor.CancelDisplayName;
		MuscleOverride = descriptor.MuscleOverride;
		RootMotionType = descriptor.RootMotionType;
	}
}
