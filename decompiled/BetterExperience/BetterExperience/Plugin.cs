using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BetterExperience.Features;
using BetterExperience.Features.AlternativeRating;
using BetterExperience.Features.Console;
using BetterExperience.Features.GeneTool;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.Features.SceneCameras;
using BetterExperience.GameScopes;
using BetterExperience.HarmonyPatches;
using BetterExperience.Utils;
using BetterExperience.Wrappers.Windows;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace BetterExperience;

[BepInPlugin("f95.betterexperience", "Better Experience Mod", "1.6.0")]
[BepInDependency("com.thora.monkey", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
	public const string VERSION = "1.6.0";

	internal static Observable DrawGUI = new Observable();

	internal static Observable DoUpdate = new Observable();

	private ScopeSupport PluginScope = new ScopeSupport
	{
		Name = "Root"
	};

	private bool errorReported;

	public static bool MonkeyMode { get; private set; }

	public static ConfigEntry<int> ProfilerLevel { get; private set; }

	public static bool MonkeyPresent { get; private set; }

	public Plugin()
	{
		ConfigEntry<bool> suppressMonkey = ((BaseUnityPlugin)this).Config.Bind<bool>("Features", "SuppressMonkey", false, "");
		ProfilerLevel = ((BaseUnityPlugin)this).Config.Bind<int>("Profiler", "Level", 0, "Profiler levels: 0 - disabled, 1 - log slow python code, 2 - GC log, 3 - instrumentation");
		MonkeyPresent = Chainloader.PluginInfos.ContainsKey("com.thora.monkey");
		MonkeyMode = MonkeyPresent;
		if (MonkeyPresent && suppressMonkey.Value)
		{
			GameObject monkey = GameObject.Find("Monkey");
			if (monkey != null)
			{
				monkey.SetActive(value: false);
				MonkeyMode = false;
			}
		}
		if (MonkeyPresent)
		{
			try
			{
				Harmony.CreateAndPatchAll(typeof(MonkeyInputPatch), (string)null);
			}
			catch (Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}

	private void CreateFeatures()
	{
		PluginScope.Provide(this);
		PluginScope.Provide<ConfigFile>(((BaseUnityPlugin)this).Config);
		PluginScope.Provide(new MainModalWindow());
		PluginScope.AddService(new GlobalPersistenceService());
		PluginScope.AddService(new DispatcherService(DrawGUI, DoUpdate));
		PluginScope.AddService(new OverlayFeature());
		SessionTracker stracker = PluginScope.AddService(new SessionTracker());
		stracker.InterviewServices.Add(() => new GeneToolWindow());
		PluginScope.AddService(new ConsoleFeature());
		PluginScope.AddService(new PluginOptionsService());
		if (!MonkeyMode)
		{
			PluginScope.AddService(new SceneCameraFeature());
		}
		PluginScope.AddService(new AlternativeGeneticsFeature());
		if (!MonkeyMode)
		{
			PluginScope.AddService(new PlayerScaler());
		}
		PluginScope.AddService(new BetterHandFeature());
		PluginScope.AddService(new GuestIO());
		PluginScope.AddService(new NoMeansNoFeature());
		PluginScope.AddService(new EmoSpy());
		PluginScope.AddService(new DragControl());
		PluginScope.AddService(new NotAMicFeature());
		PluginScope.AddService(new NaturalLanguageFeature());
		PluginScope.AddService(new GeneWatchFeature());
		PluginScope.AddService(new VelocityControlFeature());
		PluginScope.AddService(new MultithreadingFeature());
		PluginScope.AddService(new LipsDeformerFeature());
		stracker.SessionServices.Add(() => new TestFeature());
		PluginScope.AddService(new AmateurModelFeature());
		PluginScope.AddService(new AutoThrustFeature());
		PluginScope.AddService(new DialogManagerFeature());
		PluginScope.AddService(new MissionControlFeature());
		PluginScope.AddService(new LexiconProcessorFeature());
		PluginScope.AddService(new AutoSeekerFeature());
		PluginScope.AddService(new PlayerPostureFeature());
		PluginScope.AddService(new AutoRateGuestFeature());
		PluginScope.AddService(new AutoratingFeature());
		PluginScope.AddService(new AutotrainingFeature());
		PluginScope.AddService(new SafetyNetFeature());
		PluginScope.AddService(new RunInBackgroundFeature());
	}

	public T AddService<T>(T instance) where T : PluginService
	{
		return PluginScope.AddService(instance);
	}

	public void Awake()
	{
		global::BetterExperience.Logger.LoggerImpl = base.Logger;
		try
		{
			PluginScope.OnException += PluginScope_OnException;
			CreateFeatures();
			((Component)this).gameObject.hideFlags |= HideFlags.HideAndDontSave;
			((MonoBehaviour)this).StartCoroutine(AsyncStart());
		}
		catch (Exception e)
		{
			HandleCrash(e);
		}
	}

	private IEnumerator AsyncStart()
	{
		yield return null;
		Stopwatch sw = new Stopwatch();
		sw.Start();
		StartServices();
		sw.Stop();
		base.Logger.LogInfo((object)("BetterExperience started in " + sw.ElapsedMilliseconds + "ms"));
	}

	private void StartServices()
	{
		Profiler.Enabled = ProfilerLevel.Value >= 3;
		if (Profiler.Enabled)
		{
			Profiler.Install();
		}
		if (ProfilerLevel.Value >= 2)
		{
			((MonoBehaviour)this).StartCoroutine(Profiler.GCReporter());
		}
		try
		{
			Harmony.CreateAndPatchAll(typeof(SMAGlobalPatches), (string)null);
			PluginScope.Start();
		}
		catch (Exception e)
		{
			HandleCrash(e);
		}
	}

	private void HandleCrash(Exception e)
	{
		base.Logger.LogError((object)e);
		MainModalWindow wnd = new MainModalWindow();
		wnd.MessageError("BetterExperience is totally doomed\nStartupError: " + e.Message + "\nMod definitely won't work properly");
		TryShowStacktrace(e);
	}

	private void PluginScope_OnException(ScopeSupport.ScopeExceptionEvent obj)
	{
		obj.Handled = true;
		new Logger().Error(obj.Exception, "Shutting down scope {0} due to uncaught exception", obj.Sender.Name);
		obj.Sender.Dispose();
		if (!errorReported)
		{
			errorReported = true;
			MainModalWindow wnd = new MainModalWindow();
			wnd.ShowBigMessage((IsLinkerError(obj.Exception) ? "*** Incompatible Game Version ***\n\n" : "") + "BetterExperience is doomed\n" + obj.Sender.Name + ": " + obj.Exception.Message + "\nNo further error reporting.");
			TryShowStacktrace(obj.Exception);
		}
	}

	private bool IsLinkerError(Exception e)
	{
		if (!(e is TypeAccessException) && !(e is TypeInitializationException))
		{
			return e is TypeLoadException;
		}
		return true;
	}

	private void TryShowStacktrace(Exception e)
	{
		try
		{
			GameSession cs = PluginScope.Lookup<SessionTracker>().Current;
			if (cs != null)
			{
				ScopeSupport ss = new ScopeSupport();
				cs.Scope.AddChild(ss);
				cs.Scope.Lookup<OverlayService>().AddDrawable(new StacktraceWindow(e, ss), ss);
			}
		}
		catch (Exception ex)
		{
			base.Logger.LogError((object)ex);
		}
	}

	public static string GetConfigFilePath(params string[] relname)
	{
		return Path.Combine(Paths.ConfigPath, "f95.betterexperience", Path.Combine(relname));
	}

	public static T LoadExistingJsonObject<T>(params string[] name)
	{
		string path = GetConfigFilePath(name);
		if (File.Exists(path))
		{
			return (T)JsonConvert.DeserializeObject(File.ReadAllText(path), typeof(T));
		}
		new Logger().Warn("Missing expected file {0}", path);
		return default(T);
	}

	public static void SaveJsonObject<T>(T obj, params string[] name)
	{
		string path = GetConfigFilePath(name);
		string json = JsonConvert.SerializeObject(obj);
		File.WriteAllText(path, json);
	}
}
