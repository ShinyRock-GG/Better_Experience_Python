using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Assets._ReusableScripts;
using Assets._ReusableScripts.Globales;
using Assets._ReusableScripts.UI.Modales.Globales;
using Assets.Productos.Juegos.Reception.Scripts.Entrevistas;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Assets.TValle.IU.Runtime.Modales;
using BetterExperience.GameScopes;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Features;

internal class MainMenuTracker : PluginService
{
	private PanelMainMenu mainMenu;

	public bool IsMainMenu { get; private set; }

	public Observable OnStateChanged { get; } = new Observable();

	public override void OnStart()
	{
		base.OnStart();
		Lookup<DispatcherService>().StartCoroutine(WaitForMainMenu(), base.Scope);
		Lookup<SessionTracker>().OnNewSession.Add(delegate(GameSession ss)
		{
			IsMainMenu = false;
			OnStateChanged.Invoke();
			ss.Scope.OnDispose += delegate
			{
				Lookup<DispatcherService>().StartCoroutine(WaitForMainMenu(), base.Scope);
			};
		}, base.Scope);
	}

	private IEnumerator WaitForMainMenu()
	{
		GameObject menu = GameObject.Find("/PlayerDesksMainMenu");
		while (menu == null)
		{
			yield return new WaitForSeconds(0.3f);
			menu = GameObject.Find("/PlayerDesksMainMenu");
		}
		IsMainMenu = true;
		mainMenu = UnityEngine.Object.FindObjectOfType<PanelMainMenu>();
		OnStateChanged.Invoke();
	}

	public void Continue(Action cb)
	{
		BepInEx.Logging.Logger.CreateLogSource("BE_Menu").LogInfo("[MenuTracker] Continue called, mainMenu=" + (mainMenu == null ? "NULL" : mainMenu.name));
		Traverse method = Traverse.Create((object)mainMenu).Method("UnloadMainLoadEmptyEntrevista", new Type[2]
		{
			typeof(Action),
			typeof(Action)
		}, (object[])null);
		method.GetValue(new object[2]
		{
			new Action(GlobalSingletonV2<MemoriaJson>.instance.LoadFromDiskDefaultFile),
			(Action)delegate
			{
				Singleton<ConfiguracionGeneralUsuario>.instance.playerName = GlobalSingletonV2<MemoriaJson>.instance.LeerDeep("UserName", crear: true).FindData("UserName", "Anon");
			}
		});
		cb();
		IsMainMenu = false;
		OnStateChanged.Invoke();
	}

	public void SingleInterview(Action cb)
	{
		BepInEx.Logging.Logger.CreateLogSource("BE_Menu").LogInfo("[MenuTracker] SingleInterview called, mainMenu=" + (mainMenu == null ? "NULL" : mainMenu.name));
		PortraitsDialog diag = Singleton<ModalWindow>.instance.MostrarPortraitsDialog();
		// PortraitsModelBase became PortraitsModelBase<T> in SMA 23.1 — use EventInfo + Expression.Lambda
		object portraitsModel = diag.panelDePortraits.portraitsModel;
		Type modelType = portraitsModel.GetType();

		EventInfo staringEvent = modelType.GetEvent("staring");
		if (staringEvent != null)
		{
			System.Reflection.MethodInfo staringInvoke = staringEvent.EventHandlerType.GetMethod("Invoke");
			ParameterExpression modelParam = Expression.Parameter(
				staringInvoke.GetParameters()[0].ParameterType, "model");
			Action<object> onStaring = (model) =>
			{
				Traverse t = Traverse.Create(model);
				object disponibles = t.Property("protraitsDisponibles").GetValue();
				int selected = t.Property("currentSelected").GetValue<int>();
				bool canProceed = Traverse.Create(disponibles).Method("ContieneIndex", selected).GetValue<bool>();
				if (canProceed)
				{
					Traverse.Create((object)mainMenu).Method("LoadSingle", new object[2] { diag, model }).GetValue();
					cb();
					IsMainMenu = false;
					OnStateChanged.Invoke();
				}
				else
				{
					Singleton<ModalWindow>.instance.Clear<PortraitsDialog>();
				}
			};
			var convertedParam = Expression.Convert(modelParam, typeof(object));
			var callBody = Expression.Invoke(Expression.Constant(onStaring), convertedParam);
			var lambda = Expression.Lambda(staringEvent.EventHandlerType, callBody, modelParam);
			staringEvent.AddEventHandler(portraitsModel, lambda.Compile());
		}

		EventInfo cancelingEvent = modelType.GetEvent("canceling");
		if (cancelingEvent != null)
		{
			System.Reflection.MethodInfo cancelInvoke = cancelingEvent.EventHandlerType.GetMethod("Invoke");
			ParameterExpression[] cancelParams = cancelInvoke.GetParameters()
				.Select(p => Expression.Parameter(p.ParameterType)).ToArray();
			Action onCanceling = () => Singleton<ModalWindow>.instance.Clear<PortraitsDialog>();
			var cancelBody = Expression.Invoke(Expression.Constant(onCanceling));
			var cancelLambda = Expression.Lambda(cancelingEvent.EventHandlerType, cancelBody, cancelParams);
			cancelingEvent.AddEventHandler(portraitsModel, cancelLambda.Compile());
		}
	}
}
