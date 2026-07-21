using BetterExperience.GameScopes;

namespace BetterExperience.Features.Overlay;

public interface IGuiContainer<T>
{
	K Add<K>(K value, ScopeSupport scope = null) where K : T;
}
