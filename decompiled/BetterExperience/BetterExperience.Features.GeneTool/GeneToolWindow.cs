using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Pools;
using BetterExperience.Wrappers.Windows;
using UnityEngine;

namespace BetterExperience.Features.GeneTool;

internal class GeneToolWindow : SessionService
{
	private GeneTable appearance = new GeneTable();

	private GeneTable personality = new GeneTable();

	private DrawableWindow wnd;

	private ConfigEntry<KeyboardShortcut> hotkeyCfg;

	private IInputHandle hotkey;

	public override void OnStart()
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		if (base.Session.Guest.GuestInstance == null)
		{
			return;
		}
		hotkeyCfg = Lookup<ConfigFile>().Bind<KeyboardShortcut>("GeneTool", "Hotkey", new KeyboardShortcut(KeyCode.F7, Array.Empty<KeyCode>()), "GeneTool: Hotkey");
		Lookup<PluginOptionsService>().Expose(hotkeyCfg, base.Scope);
		hotkey = Lookup<DispatcherService>().Input.KeyboardEvent(hotkeyCfg, base.Scope);
		GeneWatchFeature geneWatch = base.Session.Guest.Scope.Find<GeneWatchFeature>();
		personality.Substitutons["Personalidad_"] = "";
		appearance.GeneWatch = geneWatch;
		personality.GeneWatch = geneWatch;
		appearance.Session = base.Session;
		personality.Session = base.Session;
		wnd = new DrawableWindow(Math.Max(800, Screen.width - 480), Math.Max(600, Screen.height - 120));
		wnd.Text = "Gene Tool - " + ((object)hotkeyCfg.Value/*cast due to constrained. prefix*/).ToString();
		wnd.Visible = false;
		VLayout<Drawable> stack = wnd.VLayout();
		DrawableTabPane tabs = stack.Add(new DrawableTabPane());
		tabs.AddTab("Appearance", appearance.Root, base.Scope);
		tabs.AddTab("Personality", personality.Root, base.Scope);
		appearance.EditGene += EditGene;
		personality.EditGene += EditGene;
		appearance.UpdateGenes += SetGenes;
		personality.UpdateGenes += SetGenes;
		appearance.SetGene += delegate(string a, float b)
		{
			SetGenes((a, b));
		};
		personality.SetGene += delegate(string a, float b)
		{
			SetGenes((a, b));
		};
		SetGuest(base.Session.Guest.GuestInstance);
		Lookup<OverlayService>().AddDrawable(new DockingContainer(Vector2Int.zero, wnd), base.Scope);
		base.Session.Guest.GuestValuesChanged.Add(delegate
		{
			SetGuest(base.Session.Guest.GuestInstance);
		}, base.Scope);
		Plugin.DoUpdate.Add(delegate
		{
			if (hotkey.Up)
			{
				wnd.Visible = !wnd.Visible;
			}
		}, base.Scope);
	}

	private void SetGene(string obj, float value)
	{
		SetGenes((obj, value));
	}

	private void SetGenes(params (string, float)[] genes)
	{
		List<GeneInfo> list = new List<GeneInfo>();
		for (int i = 0; i < genes.Length; i++)
		{
			var (gene, value) = genes[i];
			list.Add(new GeneInfo
			{
				Id = new GeneId(gene),
				Value = value
			});
		}
		base.Session.Guest.GuestInstance.UpdateAll(list);
		base.Session.Guest.SynchronizeCharacterWithInstance();
	}

	private void EditGene(string obj)
	{
		MayBeResult<string> promise = base.Session.Modal.RequestInput("Type new value 0..1", "");
		promise.OnResult += delegate(string result)
		{
			if (result != null)
			{
				try
				{
					float value = float.Parse(result);
					List<GeneInfo> update = new List<GeneInfo>
					{
						new GeneInfo
						{
							Id = new GeneId(obj),
							Value = value
						}
					};
					base.Session.Guest.GuestInstance.UpdateAll(update);
					base.Session.Guest.SynchronizeCharacterWithInstance();
				}
				catch (Exception ex)
				{
					base.Session.Modal.MessageError(ex.Message);
				}
			}
		};
	}

	public void SetGuest(GuestInstance guestInstance)
	{
		appearance.GeneFactory = base.Session.PoolManager.GeneFactory;
		personality.GeneFactory = base.Session.PoolManager.GeneFactory;
		appearance.SetGenes(guestInstance.ExtractAppearance().Values);
		personality.SetGenes(guestInstance.ExtractPersonality().Values);
	}
}
