using System;
using System.Collections;
using Assets;
using Assets._ReusableScripts.Globales.Updater;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using UnityEngine;

namespace BetterExperience.Features.Overlay;

public class LoadingScreenFeature : PluginService
{
	private ModificadorDeBool hideLoaderModifier;

	public Observable<bool> VisibilityChanged { get; } = new Observable<bool>();

	public bool Visible { get; private set; }

	public override void OnStart()
	{
		base.OnStart();
		Lookup<DispatcherService>().StartCoroutine(WaitForLoaderArrive(), base.Scope);
		SMAGlobalPatches.OnLoaderScreenUpdate.Add(delegate(bool value)
		{
			Visible = value;
		}, base.Scope);
	}

	private IEnumerator WaitForLoaderArrive()
	{
		while (true)
		{
			if (!Singleton<LoadingPanel>.existeEnScena)
			{
				yield return (object)new WaitForSeconds(0.5f);
				continue;
			}
			LoadingPanel loadingPanel = Singleton<LoadingPanel>.instance;
			hideLoaderModifier = loadingPanel.hidingModificable.ObtenerModificadorNotNull(default(Guid).ToString());
			while (Singleton<LoadingPanel>.existeEnScena && loadingPanel == Singleton<LoadingPanel>.instance)
			{
				yield return (object)new WaitForSeconds(0.5f);
			}
		}
	}

	public void SetLoaderEnabled(bool value)
	{
		hideLoaderModifier.valor.valor = !value;
		VisibilityChanged.Invoke(value);
	}
}
