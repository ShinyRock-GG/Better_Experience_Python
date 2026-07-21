using System.Collections.Generic;
using System.Linq;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.DialogueSystem.Interacciones.UI;
using Assets.TValle.IU.Runtime.Interacciones.THS.Donas;
using BetterExperience.CustomScene.Characters;
using BetterExperience.CustomScene.Poser;

namespace BetterExperience.CustomScene.Patches;

internal class ContextualActivitiesLoader : OpcionesDeTHSDonaDeInteraccionesDisponibles
{
	private Logger logger = new Logger
	{
		Prefix = "[ContextualActivitiesLoader] "
	};

	private List<Interaction> currentPoseSet;

	private OpcionesDeDonaDeInteraccionesDisponiblesQueIniciaDialogo oldComponent;

	private OpcionesDeTHSDonaDeInteraccionesDisponiblesQueIniciaDialogo newComponent;

	public PoseManager PoseManager { get; set; }

	public InteractionManager InteractionManager { get; set; }

	public POIManager POIManager { get; set; }

	protected override void AwakeUnityEvent()
	{
		base.AwakeUnityEvent();
		oldComponent = GetComponent<OpcionesDeDonaDeInteraccionesDisponiblesQueIniciaDialogo>();
		newComponent = GetComponent<OpcionesDeTHSDonaDeInteraccionesDisponiblesQueIniciaDialogo>();
		SetStandardInteractionsAvailable(value: false);
	}

	public void Init()
	{
		if (InteractionManager != null)
		{
			InteractionManager.OnCurrentInteractionChanged.Add(OnInteractionChanged);
		}
	}

	protected override void OnEnableUnityEvent()
	{
		base.OnEnableUnityEvent();
		Init();
	}

	protected override void OnDisableUnityEvent(bool quitting)
	{
		base.OnDisableUnityEvent(quitting);
		if (InteractionManager != null)
		{
			InteractionManager.OnCurrentInteractionChanged.Remove(OnInteractionChanged);
		}
	}

	private void OnInteractionChanged()
	{
		SetStandardInteractionsAvailable(value: false);
	}

	public void SetStandardInteractionsAvailable(bool value)
	{
		if (oldComponent != null)
		{
			oldComponent.enabled = value;
		}
		if (newComponent != null)
		{
			newComponent.enabled = value;
		}
	}

	protected override void LoadKeys(HashSetList<int> resultado)
	{
		currentPoseSet = new List<Interaction>();
		CurrentPlace place = InteractionManager.CurrentPlace;
		if (place == null)
		{
			return;
		}
		foreach (Interaction i in InteractionManager.EnumerateTransitions())
		{
			currentPoseSet.Add(i);
		}
		for (int j = 0; j < currentPoseSet.Count; j++)
		{
			resultado.Add(j);
		}
	}

	protected override string TextDeKey(int key)
	{
		if (key < currentPoseSet.Count)
		{
			return currentPoseSet[key].DisplayName;
		}
		return "???";
	}

	protected override void OnItemClicked(THSDonaController.CurrentUserData currentUserData, THSDonaController dona, THSDonaController.RadialItemData sender)
	{
		base.OnItemClicked(currentUserData, dona, sender);
		int key = base.selectedKeys.Last();
		if (key < currentPoseSet.Count)
		{
			Interaction set = currentPoseSet[key];
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
