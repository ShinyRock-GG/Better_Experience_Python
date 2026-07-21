using Assets;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets._ReusableScripts.Globales;
using Assets.Productos.Juegos.Reception.Scripts.Dependientes.ScenaManagers;
using Assets.Productos.Juegos.Reception.Scripts.Entrevistas;
using Assets.Productos.Juegos.Reception.Scripts.Entrevistas.Modelos;
using BepInEx.Configuration;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using UnityEngine;
using HarmonyLib;
using Assets.Productos.Juegos.Reception.Scripts.Genetica.Globales;

namespace BetterExperience.Features;

internal class AutoRateGuestFeature : PluginFeature
{
	private ConfigEntry<bool> configEnableFeature;

	private ConfigEntry<int> configAutorateAppearanceTo;

	private ConfigEntry<int> configAutoratePersonalityTo;

	private bool adjusted;

	public Observable<EstrevistaRatingModelo> AfterGuestAutorated { get; } = new Observable<EstrevistaRatingModelo>();

	public override bool Enabled => configEnableFeature.Value;

	public override void Configure(ConfigFile config)
	{
		configEnableFeature = config.Bind<bool>("Features", "EnableAutorating", false, "Autorating: Enable feature");
		configAutorateAppearanceTo = config.Bind<int>("Autorating", "AutorateAppearance", 0, "Autorating: Appearance");
		configAutoratePersonalityTo = config.Bind<int>("Autorating", "AutoratePersonality", 0, "Autorating: Personality");
	}

	public override void OnInit()
	{
		PluginOptionsService options = Lookup<PluginOptionsService>();
		options.Expose(configEnableFeature, base.Scope);
		options.Expose(configAutorateAppearanceTo, base.Scope);
		options.Expose(configAutoratePersonalityTo, base.Scope);
	}

	public override void OnStart()
	{
		SMAGlobalPatches.AfterRatingModelRefreshed.Add(AfterRatingModelRefreshed, base.Scope);
		Lookup<SessionTracker>().OnNewSession.Add(delegate(GameSession session)
		{
			session.OnGuestReady += delegate
			{
				adjusted = false;
			};
		}, base.Scope);
	}

	private void AfterRatingModelRefreshed(PanelDeEntrevistaCalificacion obj)
	{
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		// EntrevistaConFemaleDePoolDelDia is [Obsolete(error:true)] in SMA 23.1; access via Traverse
		object entrevista = SceneSingletonV2<ScenaCharacteresManager>.Instance(((Component)(object)obj).gameObject.scene);
		if (entrevista == null) return;
		StringKeyFloatValueDictionary flagScore = Traverse.Create(entrevista).Field<StringKeyFloatValueDictionary>("flagScoreAparienciaCurrentFemaleV2").Value;
		if ((flagScore?.Count ?? 0) != 0 || adjusted)
		{
			return;
		}
		adjusted = true;
		int appearanceValue = configAutorateAppearanceTo.Value;
		if (appearanceValue > 0)
		{
			EstrevistaCalificacionAparienciaFisicaModelo model = obj.ratingModel.aparienciaFisica;
			model.arms = appearanceValue;
			model.body = appearanceValue;
			model.breast = appearanceValue;
			model.buttocks = appearanceValue;
			model.crotch = appearanceValue;
			model.eyes = appearanceValue;
			model.face = appearanceValue;
			model.hair = appearanceValue;
			model.head = appearanceValue;
			model.height = appearanceValue;
			model.legs = appearanceValue;
			model.mouth = appearanceValue;
			model.nose = appearanceValue;
			model.skin = appearanceValue;
			model.waist_hip = appearanceValue;
		}
		int pvalue = configAutoratePersonalityTo.Value;
		if (pvalue > 0)
		{
			EstrevistaCalificacionPersonalidadModelo model2 = obj.ratingModel.personalidad;
			model2.angerManagement = pvalue;
			model2.painTolerance = pvalue;
			model2.exhibitionism = pvalue;
			model2.servicing = pvalue;
			model2.slutness = pvalue;
			if (pvalue >= 8)
			{
				model2.summarizing = (PersonalidadConsenting)4;
			}
			else if (pvalue >= 6)
			{
				model2.summarizing = (PersonalidadConsenting)3;
			}
			else if (pvalue >= 4)
			{
				model2.summarizing = (PersonalidadConsenting)2;
			}
			else if (pvalue >= 2)
			{
				model2.summarizing = (PersonalidadConsenting)1;
			}
			else
			{
				model2.summarizing = (PersonalidadConsenting)0;
			}
		}
		AfterGuestAutorated.Invoke(obj.ratingModel);
	}
}
