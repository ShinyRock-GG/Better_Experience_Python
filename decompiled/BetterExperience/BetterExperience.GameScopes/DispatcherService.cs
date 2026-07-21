using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Assets;
using Assets._ReusableScripts.Globales.Updater;
using UnityEngine;

namespace BetterExperience.GameScopes;

public class DispatcherService : PluginService
{
	private class UnityListener : CustomUpdatedMonobehaviourBase
	{
		public DispatcherService Dispatcher { get; internal set; }

		public override GlobalUpdater.UpdateType? updateEvent1 => GlobalUpdater.UpdateType.meshGeneralModsUpdate1;

		[Timed]
		public void Update()
		{
			Dispatcher.DoUpdate.Invoke();
			Dispatcher.PostUpdate();
		}

		[Timed]
		public void OnGUI()
		{
			Dispatcher.DrawGUI.Invoke();
		}

		[Timed]
		public override void OnUpdateEvent1()
		{
			Dispatcher.MeshGeneralUpdate1.Invoke();
		}
	}

	private GameObject gameObject;

	private List<Action> deferredActions = new List<Action>();

	public Observable DrawGUI { get; }

	public Observable DoUpdate { get; }

	public Observable MeshGeneralUpdate1 { get; } = new Observable();

	public InputManager Input { get; } = new InputManager();

	public DispatcherService(Observable drawGUI, Observable doUpdate)
	{
		DrawGUI = drawGUI;
		DoUpdate = doUpdate;
		DoUpdate.Add(Input.OnUpdate, base.Scope);
	}

	public override void OnStart()
	{
		base.OnStart();
		gameObject = new GameObject("BetterExperienceDispatcherObject");
		gameObject.hideFlags |= HideFlags.HideAndDontSave;
		UnityEngine.Object.DontDestroyOnLoad(gameObject);
		UnityListener listener = gameObject.AddComponent<UnityListener>();
		listener.Dispatcher = this;
	}

	public Coroutine StartCoroutine(IEnumerator e, ScopeSupport scope)
	{
		if (!scope.Started)
		{
			throw new Exception("Cannot start coroutine at stopped scope " + scope.Name);
		}
		return gameObject.GetComponent<UnityListener>().StartCoroutine(ScopedCoroutine(e, scope));
	}

	public void InvokeAsync(Action action, Action onSuccess = null, Action<Exception> onError = null)
	{
		Action<object> onSuccessAdapter = null;
		if (onSuccess != null)
		{
			onSuccessAdapter = delegate
			{
				onSuccess();
			};
		}
		InvokeAsync(delegate
		{
			action();
			return (object)null;
		}, onSuccessAdapter, onError);
	}

	public void InvokeAsync<T>(Func<T> action, Action<T> onSuccess = null, Action<Exception> onError = null)
	{
		Task.Run(delegate
		{
			try
			{
				T result = action();
				if (onSuccess != null)
				{
					InvokeLater(delegate
					{
						onSuccess(result);
					});
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Async task failed");
				if (onError != null)
				{
					onError(ex);
				}
			}
		});
	}

	private IEnumerator ScopedCoroutine(IEnumerator e, ScopeSupport scope)
	{
		while (e.MoveNext())
		{
			if (scope.Started)
			{
				yield return e.Current;
				continue;
			}
			logger.Info("Coroutine terminated due to scope destruction");
			break;
		}
	}

	[Timed]
	private void PostUpdate()
	{
		while (deferredActions.Count > 0)
		{
			Action action = deferredActions[0];
			deferredActions.RemoveAt(0);
			action();
		}
	}

	public override void OnStop()
	{
		base.OnStop();
		if (gameObject != null)
		{
			UnityEngine.Object.DestroyImmediate(gameObject);
			gameObject = null;
		}
	}

	public void InvokeLater(Action action)
	{
		if (Profiler.Enabled)
		{
			Action original = action;
			action = delegate
			{
				using (MeasureTime.Create(logger, (long t) => ObservableProfiler.FormatDelegate(original, t)))
				{
					original();
				}
			};
		}
		deferredActions.Add(action);
	}
}
