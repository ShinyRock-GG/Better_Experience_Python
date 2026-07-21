using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.AI;
using BetterExperience.GameScopes;
using UnityEngine;

namespace BetterExperience.PyStory.AI;

public class SimpleAi : SessionService
{
	public class SimpleEmotion
	{
		private Emocion impl;

		private float initialValue;

		public Action on_max_value { get; set; }

		public float value => impl.valorNoLimitado;

		public SimpleEmotion(Emocion impl)
		{
			this.impl = impl;
			impl.onMaxValue += Impl_onMaxValue;
			initialValue = impl.valorNoLimitado;
		}

		private void Impl_onMaxValue(Emocion obj)
		{
			if (on_max_value != null)
			{
				try
				{
					on_max_value();
				}
				catch (Exception ex)
				{
					Logger.Global.Error(ex, "on_max_value failure");
				}
			}
		}

		public void add(float value)
		{
			impl.ChangeValueNextUpdate(value);
		}

		public void reset()
		{
			if (impl.minValue > 0f)
			{
				impl.SetMinValue(0f);
			}
			impl.SetValueNextUpdate(0f);
		}
	}

	public class SimpleStimulus
	{
		public static Array ALL_FLAGS = Enum.GetValues(typeof(StimulusTrackingService.StimulusFlags));

		public StimulusTrackingService.StimulusKey Key { get; set; }

		public float delta_time { get; set; }

		public float total_duration { get; set; }

		public float duration { get; set; }

		public float intensity { get; set; }

		public bool covered { get; set; }

		public List<string> flags { get; } = new List<string>();

		public string stimulus => Key.Stimulus.ToString();

		public string receiver => Key.Receiver.ToString();

		public string sender => Key.Sender.ToString();

		public string emotion { get; set; }

		public SimpleStimulus()
		{
		}

		public SimpleStimulus(StimulusTrackingService.StimulusKey key, float duration, float dv, float total_duration, float intensity, StimulusTrackingService.StimulusFlags flags, string emotion)
		{
			Key = key;
			this.duration = duration;
			delta_time = dv;
			this.total_duration = total_duration;
			this.intensity = intensity;
			this.emotion = emotion;
			foreach (object flag in ALL_FLAGS)
			{
				StimulusTrackingService.StimulusFlags f = (StimulusTrackingService.StimulusFlags)flag;
				if (f != StimulusTrackingService.StimulusFlags.none && (f & flags) != StimulusTrackingService.StimulusFlags.none)
				{
					this.flags.Add(f.ToString());
				}
			}
		}
	}

	private StimulusTrackingService trackerService;

	private List<IList<StimuliReactor>> reactorStack = new List<IList<StimuliReactor>>();

	private BehaviorNode rootBehavior;

	private List<SimpleStimulus> eventPool = new List<SimpleStimulus>();

	private List<SimpleStimulus> borrowedEvents = new List<SimpleStimulus>();

	private List<SimpleStimulus> penetrations = new List<SimpleStimulus>();

	private List<SimpleStimulus> touches = new List<SimpleStimulus>();

	private List<SimpleStimulus> glances = new List<SimpleStimulus>();

	private List<SimpleStimulus> observations = new List<SimpleStimulus>();

	private List<SimpleStimulus> photos = new List<SimpleStimulus>();

	private List<SimpleStimulus> exposings = new List<SimpleStimulus>();

	private List<List<SimpleStimulus>> eventsLists = new List<List<SimpleStimulus>>();

	public SimpleEmotion Pleasure { get; private set; }

	public SimpleEmotion Anger { get; private set; }

	public SimpleEmotion Pain { get; private set; }

	public SimpleEmotion Consent { get; private set; }

	public Deseos Deseos { get; private set; }

	public SimpleAi()
	{
		eventsLists.Add(penetrations);
		eventsLists.Add(touches);
		eventsLists.Add(photos);
		eventsLists.Add(observations);
		eventsLists.Add(glances);
		eventsLists.Add(exposings);
	}

	public void Reset()
	{
		reactorStack.Clear();
	}

	public void PushBehavior(IList<StimuliReactor> reactors)
	{
		reactorStack.Insert(0, reactors);
	}

	public void SetBehaviorRoot(BehaviorNode behaviorNode)
	{
		rootBehavior = behaviorNode;
	}

	public override void OnStart()
	{
		base.OnStart();
		trackerService = Lookup<StimulusTrackingService>();
		trackerService.AfterStimulusUpdate.Add(ProcessReactions, base.Scope);
		base.Session.OnGuestReady += delegate
		{
			InitComponents();
		};
	}

	private void InitComponents()
	{
		EmocionesFemeninas emotions = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<EmocionesFemeninas>();
		Pleasure = new SimpleEmotion(emotions.placer);
		Anger = new SimpleEmotion(emotions.rage);
		Pain = new SimpleEmotion(emotions.dolor);
		Consent = new SimpleEmotion(emotions.consentToHero);
		Deseos = ((Component)(object)base.Session.Guest.Impl).GetComponentInChildren<Deseos>();
	}

	private SimpleStimulus NewStimulus(StimulusTrackingService.StimulusKey key, float duration, float dv, float total_duration, float intensity, StimulusTrackingService.StimulusFlags flags, string emotion)
	{
		SimpleStimulus stimulus = null;
		if (eventPool.Count > 0)
		{
			int last = eventPool.Count - 1;
			stimulus = eventPool[last];
			eventPool.RemoveAt(last);
		}
		else
		{
			stimulus = new SimpleStimulus();
		}
		borrowedEvents.Add(stimulus);
		stimulus.Key = key;
		stimulus.duration = duration;
		stimulus.delta_time = dv;
		stimulus.total_duration = total_duration;
		stimulus.intensity = intensity;
		stimulus.emotion = emotion;
		stimulus.flags.Clear();
		foreach (object flag in SimpleStimulus.ALL_FLAGS)
		{
			StimulusTrackingService.StimulusFlags f = (StimulusTrackingService.StimulusFlags)flag;
			if (f != StimulusTrackingService.StimulusFlags.none && (f & flags) != StimulusTrackingService.StimulusFlags.none)
			{
				stimulus.flags.Add(f.ToString());
			}
		}
		return stimulus;
	}

	private void ProcessReactions()
	{
		ReleasePool();
		foreach (StimulusTrackingService.StimulusTracker s in trackerService.ActiveStimuli)
		{
			SimpleStimulus stimuli = NewStimulus(s.Key, s.Duration, s.ImmediateDuration, s.HistoricalDuration, s.ImmediateIntensity, s.Flags, s.LastEmotion);
			if (s.Key.Stimulus == StimulusType.touch)
			{
				touches.Add(stimuli);
				continue;
			}
			if (s.Key.Stimulus == StimulusType.gaze)
			{
				glances.Add(stimuli);
				continue;
			}
			if (s.Key.Stimulus == StimulusType.observe)
			{
				observations.Add(stimuli);
				continue;
			}
			if (s.Key.Stimulus == StimulusType.penetration)
			{
				penetrations.Add(stimuli);
				continue;
			}
			if (s.Key.Stimulus == StimulusType.photo)
			{
				photos.Add(stimuli);
				continue;
			}
			if (s.Key.Stimulus == StimulusType.expose)
			{
				exposings.Add(stimuli);
				continue;
			}
			logger.Error("Unsupported stimulus type {0}", s.Key.Stimulus);
		}
		if (photos.Count > 0)
		{
			logger.Error("Photos {0}", photos.Count);
		}
		DispatchStimuli();
		DispatchList(exposings);
	}

	private void ReleasePool()
	{
		eventPool.AddRange(borrowedEvents);
		borrowedEvents.Clear();
		foreach (List<SimpleStimulus> list in eventsLists)
		{
			list.Clear();
		}
	}

	private void DispatchStimuli()
	{
		foreach (List<SimpleStimulus> list in eventsLists)
		{
			if (list == exposings || DispatchList(list))
			{
				break;
			}
		}
	}

	private bool DispatchList(List<SimpleStimulus> list)
	{
		foreach (SimpleStimulus stimuli in list)
		{
			if (Dispatch(stimuli))
			{
				return true;
			}
		}
		return false;
	}

	private bool Dispatch(SimpleStimulus stimuli)
	{
		if (rootBehavior != null)
		{
			return rootBehavior.React(stimuli);
		}
		foreach (IList<StimuliReactor> x in reactorStack)
		{
			foreach (StimuliReactor matcher in x)
			{
				if ((matcher.Receiver != null && !matcher.Receiver.Contains(stimuli.Key.Receiver)) || (matcher.Sender != null && !matcher.Sender.Contains(stimuli.Key.Sender)) || (matcher.Stimulus != null && !matcher.Stimulus.Contains(stimuli.Key.Stimulus)))
				{
					continue;
				}
				try
				{
					if (matcher.Reactor(stimuli))
					{
						return true;
					}
				}
				catch (Exception ex)
				{
					logger.Error(ex, "OOPS! reactor thrown error");
				}
			}
		}
		return false;
	}
}
