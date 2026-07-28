using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BetterExperience.CustomScene;
using BetterExperience.CustomScene.Packaging;
using BetterExperience.Features.Console;
using BetterExperience.GameScopes;
using BetterExperience.PyStory.AI;
using BetterExperience.PyStory.Scripting;
using BetterExperience.PyStory.UI;
using BetterExperience.UI;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using UnityEngine;
using UnityEngine.UIElements;

namespace BetterExperience.PyStory;

public class PyStoryRuntimeService : StoryService
{
	private PythonScriptRepository scripts = new PythonScriptRepository();

	private SimpleAi simpleAi;

	private DialogueManager dialogueManager;

	private StoryManager storyManager;

	private ScriptingContext scriptingContext;

	private bool importAllScriptsMode;

	// pycs.background_boot manifest option: boot the engine WITHOUT gating the
	// loading screen — precompile/imports run on a background task after the
	// stage scope is created, and Start3 fires on the main thread when done.
	// Pairs with pycs.stage=scene to hide the python boot in menu idle time.
	private bool backgroundBootMode;

	private ScriptingStage stage;

	private ScopeSupport scriptingStageScope;

	internal CrashWindow CrashWindow { get; set; }

	public override void OnStart()
	{
		base.OnStart();
		simpleAi = Lookup<SimpleAi>();
		dialogueManager = Lookup<DialogueManager>();
		storyManager = Lookup<StoryManager>();
		scripts.Init(base.Story.VFS);
		string pystart = scripts.GetScript("main.py");
		bool referencesPycs = base.Story.MainPackage.AllDependencies.Where((Package x) => x.Manifest.plugins.ContainsKey("f95.betterexperience.pycs")).Any();
		if (pystart == null)
		{
			if (referencesPycs)
			{
				logger.Error("No py script found");
			}
			return;
		}
		logger.Info("PyScript found");
		bool.TryParse(DiccEXT.GetValueNotNull<string, string>((IDictionary<string, string>)base.Story.MainPackage.Manifest.options, "pycs.import_all", "false"), out importAllScriptsMode);
		if (importAllScriptsMode)
		{
			logger.Info("PYCS will run all py scripts");
		}
		else
		{
			logger.Info("PYCS will run only references scripts");
		}
		stage = ScriptingStage.interview;
		if (!Enum.TryParse<ScriptingStage>(DiccEXT.GetValueNotNull<string, string>((IDictionary<string, string>)base.Story.MainPackage.Manifest.options, "pycs.stage", "interview"), out stage))
		{
			stage = ScriptingStage.interview;
		}
		bool.TryParse(DiccEXT.GetValueNotNull<string, string>((IDictionary<string, string>)base.Story.MainPackage.Manifest.options, "pycs.background_boot", "false"), out backgroundBootMode);
		if (backgroundBootMode)
		{
			logger.Info("PYCS background boot enabled — python engine will not gate the loading screen");
		}
		if (stage == ScriptingStage.scene)
		{
			base.Story.SceneScopeCreated.Add(OnStartScripting, base.Scope);
		}
		else if (stage == ScriptingStage.interview)
		{
			base.Story.InterviewScopeCreated.Add(OnStartScripting, base.Scope);
		}
		else
		{
			OnStartScripting();
		}
	}

	private void OnStartScripting()
	{
		if (stage == ScriptingStage.interview)
		{
			scriptingStageScope = base.Story.SceneInterviewScope;
		}
		else if (stage == ScriptingStage.scene)
		{
			scriptingStageScope = base.Story.SceneScope;
		}
		else
		{
			scriptingStageScope = base.Scope;
		}
		Lookup<BetterExperience.Features.Console.ConsoleService>().RegisterCommand(CommandRestart, scriptingStageScope);
		CrashWindow.OnRestart.Add(StartPyEngine, scriptingStageScope);
		CreateRestartHotkey();
		StartPyEngine();
	}

	private void CreateRestartHotkey()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		DispatcherService dispatcher = Lookup<DispatcherService>();
		IInputHandle refreshKey = dispatcher.Input.KeyboardEvent(new KeyboardShortcut(KeyCode.F5, Array.Empty<KeyCode>()), scriptingStageScope);
		dispatcher.DoUpdate.Add(delegate
		{
			if (refreshKey.Up)
			{
				base.Session.Modal.MessageBoxYesNo("Restart script?").OnResult += delegate(bool yes)
				{
					if (yes)
					{
						CommandRestart();
					}
				};
			}
		}, base.Scope);
	}

	[BetterExperience.Features.Console.ConsoleCommand("Restart pyscript", new string[] { "pycs", "restart" })]
	private string CommandRestart()
	{
		StartPyEngine();
		return "ok";
	}

	private void StartPyEngine()
	{
		CrashWindow.SetWindowVisible(v: false);
		if (scriptingContext != null)
		{
			scriptingContext.Dispose();
		}
		simpleAi.Reset();
		dialogueManager.SetActive(value: false);
		Start1_Sync();
		AsyncTask task = Start2_Async();
		if (backgroundBootMode)
		{
			// Fire-and-forget: precompile/imports run on their background task
			// without joining the loader queue (loading screen closes on time);
			// Start3 runs on the main thread via coroutine once they finish.
			logger.Warn("[LoadPerf] t={0:F2}s python background boot started (not gating loader)", UnityEngine.Time.realtimeSinceStartup);
			Lookup<DispatcherService>().StartCoroutine(BackgroundBootThenStart3(task), scriptingStageScope);
			return;
		}
		task.OnComplete = Start3_Sync;
		storyManager.ScheduleTask(task);
	}

	private IEnumerator BackgroundBootThenStart3(AsyncTask task)
	{
		while (!task.Task.IsCompleted)
		{
			yield return null;
		}
		if (task.Task.IsFaulted)
		{
			logger.Error("Background python preload faulted: {0}", task.Task.Exception?.Flatten().InnerException);
		}
		Start3_Sync();
	}

	private AsyncTask Start2_Async()
	{
		List<string> precompileFiles = new List<string>();
		List<Package> stdlibs = base.Story.MainPackage.AllDependencies.Where((Package x) => x.Manifest.options.ContainsKey("pycs.stdlib")).ToList();
		if (stdlibs.Count > 0)
		{
			Package stdlib = stdlibs[0];
			foreach (VirtIOEntry e in stdlib.LocalFS.Enumerate())
			{
				if (e.Name.EndsWith(".py"))
				{
					precompileFiles.Add(Path.Combine(e.Path, e.Name));
				}
			}
		}
		AsyncTaskProgress preloaderProgress = new AsyncTaskProgress();
		return new AsyncTask("Loading scripts", Task.Run(delegate
		{
			scriptingContext.PreloadModules(precompileFiles, preloaderProgress);
		}), preloaderProgress);
	}

	private void Start1_Sync()
	{
		scriptingContext = new ScriptingContext(Lookup<DispatcherService>(), scripts, stage);
		scriptingStageScope.AddChild(scriptingContext.ScriptingScope);
		scriptingContext.OnErrorReport.Add(OnScriptError, scriptingStageScope);
		ExposeRuntime();
	}

	private unsafe void Start3_Sync()
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		logger.Warn("[LoadPerf] t={0:F2}s Start3_Sync begin (python main)", UnityEngine.Time.realtimeSinceStartup);
		MeasureTime val = MeasureTime.Create(logger, (Func<long, string>)((long t) => $"Python startup: {t}ms"), true);
		try
		{
			if (importAllScriptsMode)
			{
				ImportAllModules();
			}
			try
			{
				object result = scriptingContext.Engine.Execute<object>("import main\nmain.start()");
				if (result is IEnumerable ie)
				{
					scriptingContext.StartPyEngineCoroutine(ie.GetEnumerator(), scriptingContext.MainStrand);
				}
			}
			catch (Exception e)
			{
				scriptingContext.ScriptingScope.NotifyCrash(e);
			}
		}
		finally
		{
			((IDisposable)(*(MeasureTime*)(&val))/*cast due to constrained. prefix*/).Dispose();
		}
		scriptingStageScope.OnDispose += DisposeContext;
		Time.timeScale = 0f;
		Lookup<DispatcherService>().InvokeLater(delegate
		{
			Time.timeScale = 1f;
		});
	}

	private void DisposeContext()
	{
		if (scriptingContext != null)
		{
			scriptingContext.Dispose();
			scriptingContext = null;
		}
	}

	private void ImportAllModules()
	{
		logger.Info("Importing all python modules...");
		Stopwatch stopwatch = new Stopwatch();
		stopwatch.Start();
		int imported = 0;
		foreach (string scriptfile in scripts.AutoimportScripts)
		{
			string name = scriptfile;
			if (name.ToLowerInvariant().EndsWith(".py"))
			{
				name = name.Substring(0, name.Length - 3);
			}
			if (name.ToLowerInvariant().EndsWith("__init__"))
			{
				name = name.Substring(0, name.Length - 8);
			}
			if (name.EndsWith("\\"))
			{
				name = name.Substring(0, name.Length - 1);
			}
			string module = name.Replace("\\", ".");
			logger.Debug("Module {0} as {1}", name, module);
			try
			{
				scriptingContext.Engine.Execute<object>("import " + module);
				imported++;
			}
			catch (Exception ex)
			{
				logger.Error("Module import failed {0}: {1}", module, ex.Message);
			}
		}
		stopwatch.Stop();
		logger.Info("Loaded {0} modules in {1}ms.", imported, stopwatch.ElapsedMilliseconds);
	}

	private void OnScriptError(string obj)
	{
		if (!UIBuilder.IsVisible((VisualElement)CrashWindow))
		{
			CrashWindow.SetError(obj);
			CrashWindow.SetWindowVisible(v: true);
		}
	}

	private void ExposeRuntime()
	{
		ScriptScope rtapi = Python.CreateModule(scriptingContext.Engine, "__pycsrt");
		rtapi.SetVariable("api", (object)new PyStoryRuntime(base.Session, base.Scope, scriptingContext));
		rtapi.SetVariable("ai", (object)simpleAi);
	}
}
