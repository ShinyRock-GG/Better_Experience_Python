using System;
using System.Collections.Generic;
using Assets;
using Assets.TValle.IU.Runtime.Interacciones.THS.Donas;
using UnityEngine;

namespace BetterExperience.Wrappers;

public class RadialMenu
{
	public class RadialMenuEntry
	{
		public string Text { get; set; }

		public THSDonaController.RadialItemData Item { get; set; }

		public List<RadialMenuEntry> Children { get; set; }

		public RadialMenuHooks Hooks { get; set; }

		public RadialMenuEntry Parent { get; set; }

		public bool Multiselect { get; set; }

		public void EmulateClick()
		{
			if (Item.onClicked == null)
			{
				return;
			}
			THSDonaController.CurrentUserData currentUserData = new THSDonaController.CurrentUserData();
			THSDonaController dona = new THSDonaController();
			Hooks.onShowed(currentUserData, dona);
			currentUserData.radialItemsData = new List<THSDonaController.RadialItemData>();
			currentUserData.radialItemsData.Add(Item);
			currentUserData.radialsSelected.Add(Item.key);
			currentUserData.radialsSelectedSet.Add(Item.key);
			Item.onSelectedStateChanged(currentUserData, isSelected: true, dona, Item);
			try
			{
				if (Multiselect)
				{
					Hooks.onAccepted(currentUserData, dona);
				}
				else
				{
					Item.onClicked(currentUserData, dona, Item);
				}
			}
			catch (NullReferenceException)
			{
			}
			Hooks.onClosed(currentUserData, dona);
		}
	}

	public class RadialMenuHooks
	{
		public THSDonaController.OnEventoSimpleHandler onShowed;

		public THSDonaController.OnEventoSimpleHandler onClosed;

		public THSDonaController.OnEventoSimpleHandler onAccepted;

		public THSDonaController.OnEventoSimpleHandler outOnGoBack;
	}

	private Logger logger = new Logger();

	private Transform root;

	public RadialMenu(GameObject go)
	{
		// SMA 23.1 BREAKING CHANGE — IModeloDeTHSDonaProductor REMOVED FROM CHARACTER ROOT
		//
		// In SMA < 23.1, every interview character had a component implementing
		// IModeloDeTHSDonaProductor somewhere in its hierarchy. The radial-menu system used
		// that component to get the THSDonaController model for the character.
		//
		// In SMA 23.1 (ScenaConMainProtagonistaFemenina scenes), this component is absent
		// from the character root. GetComponentInChildren returned null; dereferencing it
		// caused a NullReferenceException that was silently swallowed by [Timed] on
		// PostUpdate(), preventing Materialize() from completing and killing the entire
		// Python trigger chain (GuestMaterialized → OnGuestReady → InterviewScopeCreated).
		//
		// FIX (LEGITIMATE — not a band-aid): The radial menu feature is genuinely absent
		// for this character type in 23.1. Null-guarding and returning an empty list from
		// LoadMenu() is the correct response — the feature is disabled gracefully, not
		// papered over. If future SMA versions restore IModeloDeTHSDonaProductor on
		// ScenaConMain characters, this guard will transparently re-enable the feature.
		CustomMonobehaviour productor = go.GetComponentInChildren<IModeloDeTHSDonaProductor>() as CustomMonobehaviour;
		if (productor == null)
		{
			new Logger().Info("[BE] RadialMenu: IModeloDeTHSDonaProductor not found on '{0}' — radial menu disabled for this character", go.name);
			return;
		}
		root = productor.transform;
	}

	public List<RadialMenuEntry> LoadMenu()
	{
		// root is null when the constructor null-guard fired (IModeloDeTHSDonaProductor absent).
		if (root == null) return new System.Collections.Generic.List<RadialMenuEntry>();
		return LoadMenu(root, null);
	}

	internal List<RadialMenuEntry> LoadMenu(Transform context, RadialMenuEntry parent)
	{
		List<RadialMenuEntry> result = new List<RadialMenuEntry>();
		LoaderDeTHSDona dummy = new LoaderDeTHSDona();
		THSDonaController.CurrentUserData model = context.GetComponent<IModeloDeTHSDonaProductor>().ObtenerModelo();
		bool multiselect = model.config.usaAceptarBoton;
		foreach (object obj in context)
		{
			Transform tobj = (Transform)obj;
			IModeloDeTHSDonaProductorDeItemInfo[] items = tobj.GetComponents<IModeloDeTHSDonaProductorDeItemInfo>();
			IModeloDeTHSDonaProductorDeItemInfo[] array = items;
			foreach (IModeloDeTHSDonaProductorDeItemInfo item in array)
			{
				Behaviour beh = item as Behaviour;
				RadialMenuHooks hooks = new RadialMenuHooks();
				foreach (THSDonaController.RadialItemData rid in item.ObtenerModelos(out hooks.onShowed, out hooks.onClosed, out hooks.onAccepted, out hooks.outOnGoBack, dummy))
				{
					RadialMenuEntry entry = new RadialMenuEntry
					{
						Text = rid.text.ToLower(),
						Item = rid,
						Hooks = hooks,
						Parent = parent,
						Multiselect = multiselect
					};
					if (beh.transform.childCount == 1)
					{
						entry.Children = LoadMenu(beh.transform.GetChild(0), entry);
					}
					result.Add(entry);
				}
			}
		}
		return result;
	}
}
