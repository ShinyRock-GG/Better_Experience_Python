using System;
using System.Collections.Generic;
using Assets;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.AI.ReactoresDeEstimulos;
using Assets._ReusableScripts.CuchiCuchi.Estimulos;
using Assets._ReusableScripts.CuchiCuchi.Ropa;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.PyStory.AI;

public class StimulusTrackingService : SessionService
{
	public struct StimulusKey(StimulusType stimulus, HumanBodyPartsEng receiver, SenderBodyPartEng sender) : IEquatable<StimulusKey>
	{
		public StimulusType Stimulus { get; } = stimulus;

		public HumanBodyPartsEng Receiver { get; } = receiver;

		public SenderBodyPartEng Sender { get; } = sender;

		public override bool Equals(object obj)
		{
			return obj is StimulusKey key && Equals(key);
		}

		public bool Equals(StimulusKey other)
		{
			return Stimulus == other.Stimulus && Receiver == other.Receiver && Sender == other.Sender;
		}

		public override int GetHashCode()
		{
			int hashCode = -331513979;
			hashCode = hashCode * -1521134295 + Stimulus.GetHashCode();
			hashCode = hashCode * -1521134295 + Receiver.GetHashCode();
			return hashCode * -1521134295 + Sender.GetHashCode();
		}
	}

	[Flags]
	public enum StimulusFlags
	{
		none = 0,
		bare = 1,
		right = 2,
		left = 4,
		focus = 8
	}

	public class StimulusTracker
	{
		private float immediateDuration = 0f;

		private float continuousDuration = 0f;

		private float historicalDuration = 0f;

		private float lastStimulus;

		private float immediateIntensity;

		private const float continuityThreshold = 1f;

		private bool activeThisFrame = false;

		private bool active = false;

		public StimulusFlags Flags { get; private set; }

		public StimulusKey Key { get; }

		public string LastEmotion { get; private set; }

		public float Duration => continuousDuration;

		public float ImmediateDuration => immediateDuration;

		public float HistoricalDuration => historicalDuration;

		public float ImmediateIntensity => immediateIntensity;

		public StimulusTracker(StimulusKey key)
		{
			Key = key;
		}

		public void Stimulate(float intensity, StimulusFlags flags, Emocion emotion)
		{
			immediateDuration = Time.deltaTime;
			activeThisFrame = true;
			active = true;
			immediateIntensity = intensity;
			Flags |= flags;
			if (Time.time - lastStimulus < 1f)
			{
				continuousDuration += immediateDuration;
				historicalDuration += immediateDuration;
			}
			lastStimulus = Time.time;
			LastEmotion = ((UnityEngine.Object)(object)emotion).name;
		}

		internal void AfterFrameComplete()
		{
			if (!activeThisFrame)
			{
				immediateDuration = 0f;
				immediateIntensity = 0f;
				if (active && Time.time - lastStimulus > 1f)
				{
					Reset();
				}
			}
			activeThisFrame = false;
		}

		private void Reset()
		{
			immediateDuration = 0f;
			continuousDuration = 0f;
			active = false;
			Flags = StimulusFlags.none;
		}
	}

	private Dictionary<StimulusKey, StimulusTracker> trackers = new Dictionary<StimulusKey, StimulusTracker>();

	private IRopaManager ropaManager;

	private FemaleCapturablePorCamara photoGrabber;

	public Observable AfterStimulusUpdate { get; } = new Observable();

	public IEnumerable<StimulusTracker> ActiveStimuli
	{
		get
		{
			foreach (StimulusTracker st in trackers.Values)
			{
				if (st.ImmediateDuration > 0f)
				{
					yield return st;
				}
			}
		}
	}

	public override void OnStart()
	{
		base.OnStart();
		base.Session.OnGuestReady += delegate
		{
			InitComponents();
		};
	}

	private void InitComponents()
	{
		ropaManager = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<IRopaManager>();
		photoGrabber = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<FemaleCapturablePorCamara>();
		MainReactorGenerico mainReactor = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<MainReactorGenerico>();
		mainReactor.reaccionando += MainReactor_reaccionando;
		foreach (StimulusType stimulusType in Enum.GetValues(typeof(StimulusType)))
		{
			foreach (HumanBodyPartsEng receiver in Enum.GetValues(typeof(HumanBodyPartsEng)))
			{
				foreach (SenderBodyPartEng sender in Enum.GetValues(typeof(SenderBodyPartEng)))
				{
					StimulusKey key = new StimulusKey(stimulusType, receiver, sender);
					trackers[key] = new StimulusTracker(key);
				}
			}
		}
	}

	private StimulusFlags IsPartCovered(ParteDelCuerpoHumano parte)
	{
		try
		{
			if (parte.TryParce(out var ropaCubre) && ropaManager.Cubriendo(ropaCubre))
			{
				return StimulusFlags.none;
			}
		}
		catch (Exception)
		{
		}
		return StimulusFlags.bare;
	}

	[Timed]
	private void MainReactor_reaccionando(IList<ICalculoDeEstimulo> calculos, IReactorInyectable reactor)
	{
		foreach (ICalculoDeEstimulo c in calculos)
		{
			if (c.tipo != TipoDeCalculoDeEstimulo.frame)
			{
				continue;
			}
			if (c is ICalculoDeEstimuloTactil et && et.estimulo.tipo == DireccionDeEstimulo.recibida)
			{
				HumanBodyPartsEng touchedPart = (HumanBodyPartsEng)et.estimulo.principalFixed;
				SenderBodyPartEng touchedWith = (SenderBodyPartEng)et.estimulanteParte;
				StimulusKey key = new StimulusKey(StimulusType.touch, touchedPart, touchedWith);
				if (trackers.TryGetValue(key, out var tracker))
				{
					tracker.Stimulate(et.estimulo.velocidadRelativaEmulada.magnitude, IsPartCovered(et.estimulo.principalFixed), c.emocion);
				}
			}
			else if (c is ICalculoDeEstimuloVisual ev)
			{
				HumanBodyPartsEng touchedPart2 = (HumanBodyPartsEng)ev.estimulo.principalFixed;
				SenderBodyPartEng touchedWith2 = (SenderBodyPartEng)ev.estimulanteParte;
				StimulusType type = ((ev.estimulo.tipo == DireccionDeEstimulo.recibida) ? StimulusType.gaze : StimulusType.observe);
				if (type == StimulusType.gaze)
				{
					type = ((ev.estimulo.tipoDeEstimuloVisual == TipoDeEstimuloVisual.normal) ? StimulusType.gaze : ((ev.estimulo.tipoDeEstimuloVisual != TipoDeEstimuloVisual.fotografiada) ? StimulusType.gaze : StimulusType.photo));
				}
				StimulusKey key2 = new StimulusKey(type, touchedPart2, touchedWith2);
				if (trackers.TryGetValue(key2, out var tracker2))
				{
					tracker2.Stimulate(0f, IsPartCovered(ev.estimulo.principalFixed), c.emocion);
				}
			}
			else if (c is ICalculoDeEstimuloCoitalHole ec)
			{
				HumanBodyPartsEng touchedPart3 = (HumanBodyPartsEng)ec.estimulo.principalFixed;
				SenderBodyPartEng touchedWith3 = (SenderBodyPartEng)ec.estimulanteParte;
				StimulusKey key3 = new StimulusKey(StimulusType.penetration, touchedPart3, touchedWith3);
				if (trackers.TryGetValue(key3, out var tracker3))
				{
					tracker3.Stimulate(ec.estimulo.velocidadDeCambios.profundidadPeneLocal, StimulusFlags.none, c.emocion);
				}
			}
			else if (c is ICalculoDeEstimuloPorDesvestir ed)
			{
				// SMA 23.1: the old `partePrincipalEstimulada` property is gone. On InteracionEstimulanteBasica
				// it is [Obsolete(error:true)] ("Reemplazado por parteprincipal con contexto") and throws, and the
				// concrete calculo (CalculoDeEstimuloDesvestidoResultado) exposes no property of that name -- so the
				// previous Traverse-by-name silently returned default(ParteDelCuerpoHumano) (part 0). Read the
				// estimulo's principalFixed directly, exactly like the coital branch above (ec.estimulo.principalFixed).
				// Non-reflection, non-obsolete, and restores the intended body part.
				ParteDelCuerpoHumano _parteDesvestida = ed.estimulo.principalFixed;
				HumanBodyPartsEng bodypart = (HumanBodyPartsEng)_parteDesvestida;
				StimulusKey key4 = new StimulusKey(StimulusType.expose, bodypart, SenderBodyPartEng.hands);
				if (trackers.TryGetValue(key4, out var tracker4))
				{
					tracker4.Stimulate(1f, IsPartCovered((ParteDelCuerpoHumano)bodypart), c.emocion);
				}
			}
		}
		foreach (StimulusTracker t in trackers.Values)
		{
			t.AfterFrameComplete();
		}
		AfterStimulusUpdate.Invoke();
	}
}
