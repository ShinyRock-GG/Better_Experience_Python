namespace BetterExperience.GameScopes;

public interface IInputHandle
{
	bool Up { get; }

	bool Down { get; }

	bool IsHold { get; }

	float Duration { get; }
}
