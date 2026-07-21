using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.ScenaManagers;
using Assets._ReusableScripts.Globales;
using Assets.Productos.Juegos.Reception.Scripts.Dependientes.ScenaManagers;
using Assets.TValle.Pro.Entrevista.Runtime.Scenas.Managers;
using BetterExperience.HarmonyPatches;
using BetterExperience.Wrappers.Characters;
using UnityEngine.SceneManagement;

namespace BetterExperience.GameScopes;

public class SessionTracker : PluginService
{
	private class GameSessionImpl : GameSession
	{
		public GameSessionImpl(bool single)
		{
			base.SingleMode = single;
		}

		public new void SetInterviewInstance(EntrevistaConFemale obj)
		{
			base.SetInterviewInstance(obj);
		}

		public new void SetInterviewInstance(ScenaConMainProtagonistaFemenina obj)
		{
			base.SetInterviewInstance(obj);
		}
	}

	public const string GAMEPLAY_LOGIC_SCENE = "EntrevistaGamePlayLogic";

	public const string RATING_GAME_LOBBY_SCENE = "EntrevistaVacia";

	public const string RATING_GAME_CHARACTER_SCENE = "EntrevistaHeroina";

	public const string SINGLE_CHARACTER_SCENE = "EntrevistaSingleMode";

	public const string DESIGNER_MODE_SCENE = "DesignerGamePlayLogic";

	private GameSessionImpl _current;

	public Observable<GameSession> OnNewSession = new Observable<GameSession>();

	private EntrevistaConFemale deferredSingleInterview;

	public GameSession Current => _current;

	public List<Func<PluginService>> SessionServices { get; } = new List<Func<PluginService>>();

	public List<Func<PluginService>> InterviewServices { get; } = new List<Func<PluginService>>();

	public bool DesignerMode { get; private set; }

	public override void OnStart()
	{
		base.OnStart();
		SMAGlobalPatches.OnBeforeSave.Add(PreSaveHook, base.Scope);
		SceneManager.sceneLoaded += SceneManager_sceneLoaded;
		SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
	}

	public override void OnStop()
	{
		base.OnStop();
		SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
		SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;
	}

	private void SceneManager_sceneUnloaded(Scene unloadedScene)
	{
		try
		{
			OnSceneUnloaded(unloadedScene);
		}
		catch (Exception e)
		{
			base.Scope.NotifyCrash(e);
		}
	}

	private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode arg1)
	{
		try
		{
			OnSceneLoaded(scene);
		}
		catch (Exception e)
		{
			base.Scope.NotifyCrash(e);
		}
	}

	// SMA 23.1 renamed scenes: EntrevistaGamePlayLogic→GamePlayLogic,
	// EntrevistaVacia→Office*InterviewVacia, EntrevistaSingleMode→Office*SingleMode,
	// EntrevistaHeroina→Office*InterviewHeroina (exact names TBD for single/heroina).
	private static bool IsSessionStartScene(string name)
	{
		return name == "EntrevistaVacia" || name == "EntrevistaSingleMode"
			|| name.EndsWith("InterviewVacia") || name.EndsWith("SingleMode");
	}

	private static bool IsSingleModeScene(string name)
	{
		return name == "EntrevistaSingleMode" || name.EndsWith("SingleMode");
	}

	private static bool IsGamePlayLogicScene(string name)
	{
		return name == "EntrevistaGamePlayLogic" || name == "GamePlayLogic";
	}

	private static bool IsHeroinaScene(string name)
	{
		return name == "EntrevistaHeroina" || name.EndsWith("InterviewHeroina");
	}

	private void OnSceneUnloaded(Scene unloadedScene)
	{
		if (IsGamePlayLogicScene(unloadedScene.name))
		{
			OnCurrentSceneUnload_Event();
		}
		if (unloadedScene.name == "DesignerGamePlayLogic")
		{
			DesignerMode = false;
		}
	}

	private void OnSceneLoaded(Scene scene)
	{
		if (IsSessionStartScene(scene.name) && Current == null)
		{
			if (DesignerMode)
			{
				return;
			}
			_current = new GameSessionImpl(IsSingleModeScene(scene.name));
			base.Scope.Provide(_current, _current.Scope);
			foreach (Func<PluginService> supplier in SessionServices)
			{
				Current.Scope.AddService(supplier());
			}
			Current.OnGuestReady += delegate(GuestCharacter guest)
			{
				// SMA 23.1 RESILIENCE — Per-service try-catch around InterviewService startup.
				//
				// InterviewServices are started inside OnGuestReady. If any service's OnStart()
				// throws and ScopeSupport does not catch it, the exception propagates out of this
				// delegate and breaks the OnGuestReady multicast chain — StoryManager (Python) never
				// gets its turn. InvokeOnGuestReady() in GameSession provides the outer chain guard,
				// but we also guard here so a broken InterviewService doesn't stop the others from
				// starting even within this single subscriber.
				foreach (Func<PluginService> serviceFactory in InterviewServices)
				{
					try
					{
						guest.Scope.AddService(serviceFactory());
					}
					catch (Exception ex)
					{
						logger.Error("[SessionTracker] InterviewService startup failed (non-fatal, continuing): {0}\n{1}",
							ex.GetType().Name, ex);
					}
				}
			};
			OnNewSession.Invoke(Current);
			Current.Scope.Start();
		}
		if (scene.name == "DesignerGamePlayLogic")
		{
			DesignerMode = true;
		}
		if (IsHeroinaScene(scene.name))
		{
			if (_current != null)
			{
				if (_current.Guest == null)
				{
					LinkInterviewInstance(_current, scene);
				}
				else
				{
					logger.Error("Ignoring new character");
				}
			}
			else
			{
				logger.Error("Character loaded before session initialization");
			}
		}
		else if (IsSingleModeScene(scene.name))
		{
			if (_current != null)
			{
				LinkInterviewInstance(_current, scene);
			}
			else
			{
				logger.Error("Character loaded before session initialization");
			}
		}
	}

	private void LinkInterviewInstance(GameSessionImpl session, Scene scene)
	{
		logger.Info("[SessionTracker] LinkInterviewInstance for scene: {0}", scene.name);
		EntrevistaConFemale interview = (EntrevistaConFemale)SceneSingletonV2<ScenaCharacteresManager>.Instance(scene);
		if (interview == null)
		{
			// SMA 23.1 fallback: SceneSingletonV2 may not resolve the type — search globally
			logger.Info("[SessionTracker] SceneSingletonV2 returned null for '{0}', trying FindObjectOfType", scene.name);
			interview = UnityEngine.Object.FindObjectOfType<EntrevistaConFemale>();
		}
		if (interview == null)
		{
			// SMA 23.1: fall back to ScenaConMainProtagonistaFemenina (new scene manager replacing EntrevistaConFemale)
			ScenaConMainProtagonistaFemenina scenaMain = UnityEngine.Object.FindObjectOfType<ScenaConMainProtagonistaFemenina>();
			if (scenaMain != null)
			{
				logger.Info("[SessionTracker] Found ScenaConMainProtagonistaFemenina for scene '{0}' — using new-style character link", scene.name);

				// CRITICAL DIAGNOSTIC LOG — DO NOT REMOVE
				// isStared controls two divergent code paths with very different timing:
				//
				//   TRUE  → SetInterviewInstance called immediately (same Update frame as scene load).
				//            GuestCharacter.Materialize() is scheduled via InvokeLater, which also
				//            runs in this same Update frame. See GuestCharacter constructor comments
				//            for details on the asyncLoader race condition this causes.
				//
				//   FALSE → Subscribes to character.stared event. SetInterviewInstance is deferred
				//            until stared fires. If stared NEVER fires in 23.1's character lifecycle,
				//            SetInterviewInstance is never called, GuestCharacter is never created,
				//            and Python never starts — with zero error in the log.
				//
				// If you see "Found ScenaConMainProtagonistaFemenina" in the log but no subsequent
				// "[BE] SetInterviewInstance(ScenaConMain)" message, check this log line to see
				// which branch was taken. If isStared=False and "[BE] stared event fired" never
				// appears, the stared event is not firing in this SMA version.
				logger.Info("[SessionTracker] scenaMain.character.isStared={0}", scenaMain.character.isStared);

				if (scenaMain.character.isStared)
				{
					session.SetInterviewInstance(scenaMain);
				}
				else
				{
					scenaMain.character.stared += delegate
					{
						logger.Info("[SessionTracker] stared event fired for ScenaConMain character '{0}' — calling SetInterviewInstance", scenaMain.character?.name ?? "null");
						session.SetInterviewInstance(scenaMain);
					};
				}
				return;
			}
			logger.Error("[SessionTracker] No EntrevistaConFemale or ScenaConMainProtagonistaFemenina found for scene '{0}' — skipping character link", scene.name);
			return;
		}
		if (interview.isStared)
		{
			session.SetInterviewInstance(interview);
			return;
		}
		interview.stared += delegate
		{
			session.SetInterviewInstance(interview);
		};
	}

	private void OnCurrentSceneUnload_Event()
	{
		if (Current != null)
		{
			Current.Scope.Dispose();
			_current = null;
		}
		deferredSingleInterview = null;
	}

	private void PreSaveHook()
	{
		if (Current != null)
		{
			DateTime dt = DateTime.Now;
			Current.PreSave.Invoke();
		}
	}
}
