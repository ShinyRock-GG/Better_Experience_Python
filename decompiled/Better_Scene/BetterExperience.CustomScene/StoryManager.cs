using System;
using System.Collections;
using System.Collections.Generic;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Features.Overlay;
using BetterExperience.GameScopes;
using BetterExperience.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterExperience.CustomScene;

public class StoryManager : PluginService
{
	private class AsyncLoader
	{
		private ExtendedLoaderWindow loaderWnd;

		private DispatcherService dispatcher;

		private LoadingScreenFeature loadingScreen;

		private ScopeSupport scope;

		private bool active;

		public Queue<IEnumerator> Queue { get; } = new Queue<IEnumerator>();

		public AsyncLoader(ExtendedLoaderWindow loaderWnd, DispatcherService dispatcher, LoadingScreenFeature loadingScreen, ScopeSupport scope)
		{
			this.loaderWnd = loaderWnd;
			this.dispatcher = dispatcher;
			this.loadingScreen = loadingScreen;
			this.scope = scope;
			active = false;
		}

		public void Start()
		{
			if (!active)
			{
				active = true;
				dispatcher.StartCoroutine(LoaderThread(), scope);
			}
		}

		private IEnumerator LoaderThread()
		{
			// [LoadPerf] realtime stamps bracket the loading screen so log line
			// ordering + t= values reveal any post-loader "black gap" before the
			// story script makes the world visible.
			Logger.Global.Warn("[LoadPerf] t={0:F2}s loader SHOWN", Time.realtimeSinceStartup);
			loadingScreen.SetLoaderEnabled(true);
			loaderWnd.SetVisible(value: true);
			active = true;
			yield return null;
			while (Queue.Count > 0)
			{
				IEnumerator it = Queue.Dequeue();
				while (it.MoveNext())
				{
					yield return ProcessOperation(it.Current);
				}
			}
			Logger.Global.Warn("[LoadPerf] t={0:F2}s loader HIDDEN", Time.realtimeSinceStartup);
			loadingScreen.SetLoaderEnabled(false);
			loaderWnd.SetVisible(value: false);
			active = false;
		}

		private object ProcessOperation(object current)
		{
			string text = "Loading scene...";
			if (current != null)
			{
				if (current is AssetLoader.AsyncWrapper aw)
				{
					text = aw.Description;
					current = aw.Operation;
					try
					{
						dispatcher.StartCoroutine(ProgressTracker(aw.Operation), scope);
					}
					catch (Exception ex)
					{
						Logger.Create<AsyncLoader>().Error(ex, "Failed to start coroutine");
					}
				}
				else if (current is string s)
				{
					text = s;
				}
			}
			loaderWnd.SetOperation(text);
			return current;
		}

		private IEnumerator ProgressTracker(AsyncOperation operation)
		{
			while (!operation.isDone)
			{
				loaderWnd.SetProgress(operation.progress);
				yield return null;
			}
		}

		public void InvokeLater(Action action)
		{
			Queue.Enqueue(GeneratorWrapper(action));
			if (!active)
			{
				Start();
			}
		}

		public void ScheduleTask(AsyncTask task)
		{
			List<AsyncTask> tasks = new List<AsyncTask>();
			tasks.Add(task);
			AwaitTasks(tasks);
		}

		private IEnumerator GeneratorWrapper(Action a)
		{
			a();
			yield break;
		}

		public void AwaitTasks(List<AsyncTask> tasks)
		{
			Queue.Enqueue(AwaitTasksGen(tasks));
			if (!active)
			{
				Start();
			}
		}

		private IEnumerator AwaitTasksGen(List<AsyncTask> tasks)
		{
			Logger log = Logger.Create<AsyncLoader>();
			if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
				log.Info("[SceneLog] AwaitTasksGen starting — {0} task(s)", tasks.Count);
			for (int i = 0; i < tasks.Count; i++)
			{
				AsyncTask t = tasks[i];
				if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
					log.Info("[SceneLog] Waiting for task [{0}/{1}]: {2}", i + 1, tasks.Count, t.Text);
				AsyncTaskProgress progress = t.Progress;
				Func<string> formatProgress = () => t.Text + " " + (int)((ObservableValue<(float, int, int)>)progress).Value.Item1 + "%";
				string operation = formatProgress();
				while (!t.Task.IsCompleted)
				{
					if (progress != t.Progress)
					{
						progress = t.Progress;
						operation = formatProgress();
					}
					yield return operation;
				}
				if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
				{
					if (t.Task.IsFaulted)
						log.Error("[SceneLog] Task FAULTED [{0}/{1}]: {2} -- {3}", i + 1, tasks.Count, t.Text, t.Task.Exception?.Flatten().InnerException);
					else
						log.Info("[SceneLog] Task complete [{0}/{1}]: {2}", i + 1, tasks.Count, t.Text);
				}
				if (t.OnComplete != null)
				{
					try
					{
						t.OnComplete();
					}
					catch (Exception e)
					{
						scope.NotifyCrash(e);
					}
				}
			}
			if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
				log.Info("[SceneLog] AwaitTasksGen complete — all tasks done");
			yield return null;
		}
	}

	private class MergedScope : ScopeSupport
	{
		private ScopeSupport transientParent;

		public MergedScope(ScopeSupport transientParent)
		{
			this.transientParent = transientParent;
		}

		public override T Find<T>()
		{
			T result = transientParent.Find<T>();
			if (result != null)
			{
				return result;
			}
			return base.Find<T>();
		}
	}

	private Story currentStory;

	private PackageManager packageManager;

	private DispatcherService dispatcher;

	private OverlayService overlayService;

	private string activeScene;

	private ExtendedLoaderWindow loaderWnd;

	private AsyncLoader asyncLoader;

	private bool enableCustomSceneServices;

	public List<Func<SessionService>> StoryServices { get; } = new List<Func<SessionService>>();

	public List<Func<SessionService>> StoryInterviewServices { get; } = new List<Func<SessionService>>();

	public List<Func<SessionService>> SceneServices { get; } = new List<Func<SessionService>>();

	public Story Current => currentStory;

	public override void OnStart()
	{
		base.OnStart();
		packageManager = Lookup<PackageManager>();
		SessionTracker tracker = Lookup<SessionTracker>();
		tracker.OnNewSession.Add(OnNewSession, base.Scope);
		dispatcher = Lookup<DispatcherService>();
		overlayService = Lookup<OverlayService>();
		SceneManager.sceneLoaded += SceneManager_sceneLoaded;
		loaderWnd = new ExtendedLoaderWindow(Lookup<CustomSceneFeature>().EditorUiPanel);
		overlayService.LoadingScreen.VisibilityChanged.Add(loaderWnd.SetVisible, base.Scope);
	}

	public override void OnStop()
	{
		base.OnStop();
		SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
	}

	private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
	{
		logger.Info("Scene loaded {0}", arg0.name);
		if (currentStory != null && activeScene != null)
		{
			AssetLoader am = currentStory.Scope.Lookup<AssetLoader>();
			IEnumerator coro = am.SetupScene(activeScene, arg0.name);
			dispatcher.StartCoroutine(coro, currentStory.Scope);
		}
	}

	private void OnNewSession(GameSession session)
	{
		if (currentStory == null)
		{
			logger.Info("[SceneLog] OnNewSession - no story, skipping");
			return;
		}
		logger.Info("[SceneLog] OnNewSession - loading {0}", currentStory?.MainPackage?.Id);
		overlayService.LoadingScreen.SetLoaderEnabled(true);
		session.Scope.AddChild(currentStory.Scope);
		session.Scope.Provide(currentStory);
		session.Scope.Provide(this);
		foreach (Func<SessionService> x in StoryServices)
		{
			currentStory.Scope.AddService(x());
		}
		session.Scope.OnStart += delegate
		{
			LoadMainScene();
		};
		session.Scope.OnDispose += delegate
		{
			if (currentStory != null)
			{
				currentStory.Scope.Dispose();
			}
			currentStory = null;
			logger.Info("Story session disposed");
		};
	}

	private void LoadMainScene()
	{
		string scene = currentStory.MainPackage.Manifest.mainScene;
		if (scene != null)
		{
			asyncLoader.Queue.Enqueue(LoadSceneAsync(scene));
		}
		asyncLoader.InvokeLater(OnSceneLoaded);
		asyncLoader.Start();
	}

	private IEnumerator LoadSceneAsync(string scene)
	{
		activeScene = scene;
		AssetLoader am = currentStory.Scope.Lookup<AssetLoader>();
		return am.LoadScene(scene);
	}

	private void OnSceneLoaded()
	{
		currentStory.SceneScope = new ScopeSupport();
		currentStory.SceneScope.Name = "Scene";
		currentStory.SceneScope.Autostart = false;
		currentStory.Scope.AddChild(currentStory.SceneScope);
		GameSession ss = currentStory.SceneScope.Lookup<GameSession>();
		ScheduleInstantiateServices(SceneServices, currentStory.SceneScope);
		asyncLoader.InvokeLater(delegate
		{
			logger.Info("[BE] AsyncLoader: SceneScopeStart delegate running");
			currentStory.SceneScope.Start();
			currentStory.SceneScopeCreated.Invoke();
			logger.Info("[BE] AsyncLoader: SceneScopeCreated invoked");
		});
		asyncLoader.InvokeLater(delegate
		{
			// SMA 23.1 race-condition fix: the original code only took the direct path if
			// ss.SingleMode && ss.Guest != null. But with ScenaConMainProtagonistaFemenina,
			// isStared=true at scene load, so Materialize() fires via dispatcher.InvokeLater
			// during the Update phase — BEFORE this coroutine-phase callback even runs.
			// Removing the SingleMode guard means we catch the already-materialized case in
			// both SingleMode and normal interview scenes.
			logger.Info("[BE] AsyncLoader: OnGuestReady check — Guest={0} SingleMode={1}",
				ss.Guest != null ? "ready" : "null", ss.SingleMode);
			if (ss.Guest != null)
			{
				logger.Info("[BE] Guest already ready — scheduling CreateInterviewScope directly");
				asyncLoader.InvokeLater(CreateInterviewScope);
			}
			else
			{
				logger.Info("[BE] Guest not ready — subscribing to OnGuestReady");
				ss.OnGuestReady += delegate
				{
					logger.Info("[BE] OnGuestReady fired — calling CreateInterviewScope");
					CreateInterviewScope();
				};
			}
			dispatcher.StartCoroutine(ReportSceneLoadingWarnings(SceneWarnings.Instance.CollectWarnings(), immediate: false), currentStory.Scope);
			SceneWarnings.Instance.OnNewWarning.Add(delegate
			{
				dispatcher.StartCoroutine(ReportSceneLoadingWarnings(SceneWarnings.Instance.CollectWarnings(), immediate: true), currentStory.Scope);
			}, currentStory.Scope);
		});
	}

	private void ScheduleInstantiateServices(List<Func<SessionService>> sceneServices, ScopeSupport sceneScope)
	{
		List<AsyncTask> joinTasks = new List<AsyncTask>();
		if (enableCustomSceneServices)
		{
			foreach (Func<SessionService> x in sceneServices)
			{
				SessionService service = sceneScope.AddService(x());
				if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
					logger.Info("[SceneLog] Instantiated scene service: {0}", service.GetType().Name);
				if (service is StoryService storyService)
				{
					joinTasks.AddRange(storyService.AsyncHandles);
					if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
						logger.Info("[SceneLog]   -> added {0} async handle(s) from {1}", storyService.AsyncHandles.Count, service.GetType().Name);
				}
			}
		}
		if (CustomSceneFeature.VerboseSceneLogging?.Value == true)
			logger.Info("[SceneLog] ScheduleInstantiateServices done -- {0} async task(s) queued", joinTasks.Count);
		asyncLoader.AwaitTasks(joinTasks);
	}

	private IEnumerator ReportSceneLoadingWarnings(List<SceneWarnings.Warning> warnings, bool immediate)
	{
		yield return new WaitForSeconds(1f);
		if (warnings.Count <= 0)
		{
			yield break;
		}
		if (!immediate)
		{
			logger.Info("Scene loader warning report");
			logger.Info("---------------------------");
		}
		foreach (SceneWarnings.Warning w in warnings)
		{
			string msg = "";
			if (w.Count > 1)
			{
				msg = "[" + w.Count + "] ";
			}
			msg += w.Message;
			overlayService.InfoMessage(msg);
			logger.Info(msg);
		}
		if (!immediate)
		{
			overlayService.InfoMessage("See log for more info");
			logger.Info("---------------------------");
		}
	}

	private void CreateInterviewScope()
	{
		logger.Info("[BE] CreateInterviewScope() called");
		GameSession ss = currentStory.SceneScope.Lookup<GameSession>();
		logger.Info("[BE] CreateInterviewScope: ss.Guest={0} SceneScope.Started={1}",
			ss.Guest != null ? "ready" : "null", currentStory.SceneScope.Started);
		currentStory.SceneInterviewScope = new MergedScope(ss.Guest.Scope);
		currentStory.SceneInterviewScope.Name = "SceneGuest";
		currentStory.SceneInterviewScope.Autostart = false;
		currentStory.SceneScope.AddChild(currentStory.SceneInterviewScope);
		ScheduleInstantiateServices(StoryInterviewServices, currentStory.SceneInterviewScope);
		ss.Guest.Scope.OnDispose += currentStory.SceneInterviewScope.Dispose;
		if (activeScene != null)
		{
			AssetLoader am = currentStory.Scope.Lookup<AssetLoader>();
			asyncLoader.Queue.Enqueue(am.RunInterviewSequence(activeScene));
		}
		if (currentStory.SceneScope.Started)
		{
			asyncLoader.InvokeLater(delegate
			{
				logger.Info("[BE] InterviewScopeCreated: starting SceneInterviewScope and invoking");
				currentStory.SceneInterviewScope.Start();
				currentStory.InterviewScopeCreated.Invoke();
				logger.Info("[BE] InterviewScopeCreated.Invoke() complete");
			});
		}
		else
		{
			logger.Error("[BE] CreateInterviewScope: SceneScope not started — cannot invoke InterviewScopeCreated");
		}
	}

	public void SelectStory(Package package, List<Package> disabledExts)
	{
		logger.Info("[SceneLog] SelectStory: {0}", package?.Id ?? "null");
		VirtIO vfs = packageManager.CreateMergedFS(package, disabledExts);
		currentStory = new Story(package, vfs);
		currentStory.Scope.Name = "Story_" + package.Id;
		currentStory.Scope.Provide(this);
		overlayService.LoadingScreen.SetLoaderEnabled(true);
		loaderWnd.SetTitle("Loading scene: " + package.Name + " by " + package.Manifest.author + " [" + package.Id + ":" + package.Version?.ToString() + "]");
		asyncLoader = new AsyncLoader(loaderWnd, dispatcher, overlayService.LoadingScreen, currentStory.Scope);
		bool disableServices = false;
		if (currentStory.MainPackage.Manifest.options.TryGetValue("no_css", out var nocss))
		{
			bool.TryParse(nocss, out disableServices);
		}
		enableCustomSceneServices = !disableServices;
	}

	public void ScheduleTask(AsyncTask task)
	{
		asyncLoader.ScheduleTask(task);
	}
}
