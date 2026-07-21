using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.GoTo.UI;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.Interacciones.UI;
using Assets.TValle.IU.Runtime.Interacciones.THS.Donas;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;
using BetterExperience.GameScopes;

namespace BetterExperience.CustomScene.Patches;

internal class PointOfInterestLoader : OpcionesDeTHSDonaDeInteraccionesDisponibles
{
	private Logger logger = Logger.Create<PointOfInterestLoader>();

	private List<Interaction> interactions = new List<Interaction>();

	public InteractionManager InteractionManager { get; private set; }

	public POIManager POIManager { get; private set; }

	public PoseManager PoseManager { get; private set; }

	public void InitComponent(ScopeSupport scope)
	{
		InteractionManager = scope.Lookup<InteractionManager>();
		POIManager = scope.Lookup<POIManager>();
		PoseManager = scope.Lookup<PoseManager>();
	}

	protected override void OnEnableUnityEvent()
	{
		base.OnEnableUnityEvent();
		OpcionesDeTHSDonaDeGoToDisponiblesQueIniciaDialogo newComponent = base.transform.GetComponent<OpcionesDeTHSDonaDeGoToDisponiblesQueIniciaDialogo>();
		if (newComponent != null)
		{
			newComponent.enabled = false;
		}
		OpcionesDeDonaDeGoToDisponiblesQueIniciaDialogo oldComponent = base.transform.GetComponent<OpcionesDeDonaDeGoToDisponiblesQueIniciaDialogo>();
		if (oldComponent != null)
		{
			oldComponent.enabled = false;
		}
	}

	protected override void LoadKeys(HashSetList<int> resultado)
	{
		interactions = new List<Interaction>();
		CurrentPlace place = InteractionManager.CurrentPlace;
		if (place == null)
		{
			logger.Error("Current place is unresolved");
			return;
		}
		interactions.AddRange(InteractionManager.EnumerateGotos());
		for (int i = 0; i < interactions.Count; i++)
		{
			resultado.Add(i);
		}
	}

	protected override string TextDeKey(int key)
	{
		return interactions[key].DisplayName;
	}

	protected override void OnItemClicked(THSDonaController.CurrentUserData currentUserData, THSDonaController dona, THSDonaController.RadialItemData sender)
	{
		base.OnItemClicked(currentUserData, dona, sender);
		int key = base.selectedKeys.Last();
		if (key < interactions.Count)
		{
			Interaction set = interactions[key];
			InteractionManager.StartInteraction(set);
		}
		dona.StopDrawing();
	}

	protected override void OnLoadedItems(LoaderDeTHSDona caller)
	{
	}

	protected override void OnUserAceptar(THSDonaController.CurrentUserData currentUserData, THSDonaController sender)
	{
	}

	protected override void OnUserGoBack(THSDonaController.CurrentUserData currentUserData, THSDonaController sender)
	{
	}

	protected override bool LoadOnlyEjecutandose()
	{
		return false;
	}
}
