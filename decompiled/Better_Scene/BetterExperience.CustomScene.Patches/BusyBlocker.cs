using Assets;
using Assets._ReusableScripts.UI.Interacciones.Donas;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Patches;

internal class BusyBlocker : CustomUpdatedMonobehaviourBase, ICheckerIsGreyOut
{
	public bool isGreyOut => HasActiveInteraction();

	private InteractionManager InteractionManager { get; set; }

	private bool HasActiveInteraction()
	{
		return InteractionManager.HasActiveInteraction;
	}

	public void InitComponent(ScopeSupport scope)
	{
		InteractionManager = scope.Lookup<InteractionManager>();
	}
}
