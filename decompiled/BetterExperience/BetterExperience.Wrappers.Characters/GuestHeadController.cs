using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets;
using Assets._ReusableScripts;
using Assets._ReusableScripts.CuchiCuchi.Characters.Controladores.ControlladoresDeColoDePrioridad;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets.SingletonesAndSystemasGlobales.AbstractLayer.Globales;
using Assets.TValle.BeachGirl.Runtime;
using Assets.TValle.SystemasConstraints.RunTime.ChildOfConstraints.Implementation;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

public class GuestHeadController : IDisposable
{
	public class LookAt
	{
		public bool Enabled { get; set; }

		public Transform Transform { get; set; }

		public int Priority { get; set; } = int.MaxValue;

		public float HeadWeight { get; set; } = 1f;

		public float EyesWeight { get; set; } = 1f;

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			if (Transform != null)
			{
				sb.Append(Transform.name);
			}
			else
			{
				sb.Append("???");
			}
			sb.Append(" ").Append(Enabled).Append(" ")
				.Append(Priority);
			return sb.ToString();
		}
	}

	private Logger logger = Logger.Create<GuestHeadController>();

	private IControlladorDeBark talkController;

	private ControladorDeGestosDeBoca mimicController;

	private LookAtControllerV2 lookAtController;

	private bool muted;

	private List<LookAt> lookAtTargets = new List<LookAt>();

	private ScopeSupport scope;

	public BitMask<TiposDeGestosDeBoca> ActiveMimics { get; private set; }

	public Observable<BitMask<TiposDeGestosDeBoca>> ActiveMimicChanged { get; private set; } = new Observable<BitMask<TiposDeGestosDeBoca>>();

	public bool Mute
	{
		get
		{
			return muted;
		}
		set
		{
			if (muted != value)
			{
				muted = value;
				if (muted)
				{
					talkController.FlagMinPrioridad(int.MaxValue, 60f);
				}
				else
				{
					talkController.FlagMinPrioridad(int.MinValue, 0f);
				}
			}
		}
	}

	public GuestHeadController(GameObject gameObject, ScopeSupport scope)
	{
		talkController = gameObject.GetComponentInChildren<IControlladorDeBark>();
		mimicController = gameObject.GetComponentInChildren<ControladorDeGestosDeBoca>();
		if (mimicController.isStared)
		{
			Singleton<SystemaMainChildOf>.instance.completedCalled += Instance_completedCalled;
		}
		else
		{
			mimicController.stared += delegate
			{
				Singleton<SystemaMainChildOf>.instance.completedCalled += Instance_completedCalled;
			};
		}
		lookAtController = gameObject.GetComponentInChildren<LookAtControllerV2>();
		this.scope = scope;
		scope.Lookup<DispatcherService>().StartCoroutine(LookatUpdater(), scope);
	}

	private void Instance_completedCalled(SystemaMainChildOf obj)
	{
		try
		{
			BitMask<TiposDeGestosDeBoca> flags = default(BitMask<TiposDeGestosDeBoca>);
			ControladorDeGestosDeBoca.Stado currentState = mimicController.currentStado;
			if (currentState != null)
			{
				for (int i = 0; i < currentState.Count; i++)
				{
					ControladorDeGestosDeBoca.Orden cmd = currentState.ordenes[i];
					if (cmd != null)
					{
						flags = flags.Add(cmd.tipoDeGesto);
					}
				}
			}
			if (!ActiveMimics.Equals(flags))
			{
				ActiveMimics = flags;
				ActiveMimicChanged.Invoke(flags);
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Err");
		}
	}

	public void Dispose()
	{
		if (Singleton<SystemaMainChildOf>.existeEnScena)
		{
			Singleton<SystemaMainChildOf>.instance.completedCalled -= Instance_completedCalled;
		}
	}

	public void Say(string text, int priority = int.MaxValue, float perCharDurationFactor = 0.5f, float durationFactor = 1f)
	{
		talkController.Bark(text, vocalizar: true, priority, ControllerPrioridadConfig.prioridad, perCharDurationFactor, durationFactor);
	}

	private IEnumerator LookatUpdater()
	{
		if (logger.EnableDebug)
		{
			logger.Debug("Starting look updater {0}", scope.Started);
		}
		float lockDuration = 2f;
		while (scope.Started)
		{
			SortLookats();
			if (logger.EnableDebug)
			{
				string[] a = lookAtTargets.Select((LookAt x) => x.ToString()).ToArray();
				logger.Debug("Targets {0}", string.Join(",", a));
			}
			if (lookAtTargets.Count > 0)
			{
				LookAt target = lookAtTargets[0];
				if (target.Enabled && target.Transform != null)
				{
					lookAtController.DetenerOrdenes();
					lookAtController.Mirar(target.HeadWeight, target.EyesWeight, target.Transform, LookAtControllerV2.LookAtType.fijamente, true, LookAtControllerV2.LookAtType.fijamente, true, 1f, target.Priority, lockDuration, ControllerPrioridadConfig.prioridad, default(Vector3));
					if (logger.EnableDebug)
					{
						logger.Debug("Looking at {0}", target.Transform.name);
					}
				}
				else if (logger.EnableDebug)
				{
					logger.Debug("Target is disabled {0} {1}", target.Enabled, target.Transform);
				}
			}
			else if (logger.EnableDebug)
			{
				logger.Debug("No targets");
			}
			yield return new WaitForSeconds(1f);
		}
		if (logger.EnableDebug)
		{
			logger.Debug("Looker stopped");
		}
	}

	public LookAt CreateLookAt(ScopeSupport scope)
	{
		LookAt lookat = new LookAt();
		lookAtTargets.Add(lookat);
		scope.OnDispose += delegate
		{
			lookAtTargets.Remove(lookat);
		};
		return lookat;
	}

	private void SortLookats()
	{
		lookAtTargets.Sort(delegate(LookAt a, LookAt b)
		{
			int num = a.Enabled.CompareTo(b.Enabled);
			return (num != 0) ? (-num) : (-a.Priority.CompareTo(b.Priority));
		});
	}
}
