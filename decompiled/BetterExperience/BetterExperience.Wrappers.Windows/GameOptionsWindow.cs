using System.Collections;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Assets.TValle.BeachGirl.UI.Runtime.Globales;
using Assets.TValle.IU.Runtime.Drawing.Abstracts;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.Wrappers.Windows;

internal class GameOptionsWindow : PluginService
{
	private GenericUserPanelBase componentController;

	public bool Visible
	{
		get
		{
			if (componentController != null)
			{
				return componentController.isShowing;
			}
			return false;
		}
	}

	public Observable OnVisibilityChanged { get; } = new Observable();

	public override void OnStart()
	{
		Lookup<DispatcherService>().StartCoroutine(WaitForLoaderArrive(), base.Scope);
	}

	private IEnumerator WaitForLoaderArrive()
	{
		while (true)
		{
			if (!Singleton<MainPanelGameOptions>.existeEnScena)
			{
				yield return new WaitForSeconds(0.5f);
				continue;
			}
			MainPanelGameOptions loadingPanel = Singleton<MainPanelGameOptions>.instance;
			componentController = loadingPanel.main.GetComponentInParent<GenericUserPanelBase>();
			componentController.showed += ToggleVisibility;
			componentController.hided += ToggleVisibility;
			while (Singleton<MainPanelGameOptions>.existeEnScena && loadingPanel == Singleton<MainPanelGameOptions>.instance)
			{
				yield return new WaitForSeconds(0.5f);
			}
		}
	}

	public override void OnStop()
	{
		if (componentController != null)
		{
			componentController.showed -= ToggleVisibility;
			componentController.hided -= ToggleVisibility;
		}
	}

	private void ToggleVisibility(GenericUserPanelBase obj)
	{
		OnVisibilityChanged.Invoke();
	}
}
