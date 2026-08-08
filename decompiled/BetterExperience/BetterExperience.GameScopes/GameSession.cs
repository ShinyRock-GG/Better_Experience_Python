using System;
using System.Linq;
using Assets.Productos.Juegos.Reception.Scripts.Dependientes.ScenaManagers;
using Assets.TValle.Pro.Entrevista.Runtime.Scenas.Managers;
using BetterExperience.Wrappers.Cameras;
using BetterExperience.Wrappers.Characters;
using BetterExperience.Wrappers.Pools;
using BetterExperience.Wrappers.Windows;

namespace BetterExperience.GameScopes;

public class GameSession
{
	private EntrevistaConFemale interview;

	private GuestCharacter currentFemale;

	private PlayerCharacter currentPlayer;

	private MainCamera mainCamera;

	private MainModalWindow modals;

	private PoolManager poolManager;

	public GuestCharacter Guest => currentFemale;

	public PlayerCharacter Player => currentPlayer;

	public MainCamera MainCamera => mainCamera;

	public MainModalWindow Modal => modals;

	public PoolManager PoolManager => poolManager;

	public bool SingleMode { get; protected set; }

	public ScopeSupport Scope { get; } = new ScopeSupport
	{
		Autostart = false
	};

	public Observable PreSave { get; } = new Observable();

	public event Action<GuestCharacter> OnGuestReady = delegate
	{
	};

	public event Action<GuestCharacter> OnGuestLeft = delegate
	{
	};

	// SMA 23.1 INFRASTRUCTURE — Resilient OnGuestReady invocation.
	//
	// OnGuestReady is a C# multicast event. The default invocation (this.OnGuestReady(guest))
	// calls all subscribers as a chain: if ANY subscriber throws an unhandled exception, the
	// remaining subscribers never run. This means a single crashing service (GeneToolWindow,
	// SafetyNetService, GIOService backup, etc.) can silently prevent StoryManager from getting
	// its OnGuestReady call, which breaks the entire Python trigger chain with no log output.
	//
	// This helper iterates the invocation list manually with a per-subscriber try-catch. One
	// broken subscriber logs an error and is skipped; all subsequent subscribers still run.
	// StoryManager (Python) always gets its turn regardless of what other services do.
	//
	// This is not a band-aid — it is the correct design for a plugin event where subscribers
	// are independently-written services that should not be able to break each other.
	private void InvokeOnGuestReady(GuestCharacter guest)
	{
		Logger log = Logger.Create<GameSession>();
		foreach (Action<GuestCharacter> subscriber in this.OnGuestReady.GetInvocationList().Cast<Action<GuestCharacter>>())
		{
			try
			{
				subscriber(guest);
			}
			catch (Exception ex)
			{
				log.Error("[BE] OnGuestReady subscriber '{0}' threw (non-fatal, chain continues): {1}\n{2}",
					subscriber.Method?.Name ?? "unknown", ex.GetType().Name, ex);
			}
		}
	}

	protected void SetInterviewInstance(EntrevistaConFemale obj)
	{
		interview = obj;
		GuestCharacter guest = new GuestCharacter(obj.currentFemaleCharacter, (obj is EntrevistaConSingleFemale) ? ((EntrevistaConSingleFemale)obj).currentNpc : null, Scope);
		guest.SynchronizeCharacterWithInstance();
		obj.femalePresenciaChanged += GuestPresenceChanged;
		Action handler = delegate
		{
			currentFemale = guest;
			InvokeOnGuestReady(currentFemale);
		};
		if (guest.IsMaterialized)
		{
			handler();
		}
		else
		{
			guest.GuestMaterialized += handler;
		}
	}

	// SMA 23.1: Office*InterviewHeroina scenes now use ScenaConMainProtagonistaFemenina
	// (TValle.Pro.Entrevista.dll) instead of EntrevistaConFemale. This overload handles that
	// new scene manager type. femalePresenciaChanged is NOT wired here — the presencia system
	// is part of EntrevistaConFemale and doesn't exist on ScenaConMain.
	//
	// Race condition handled here: GuestCharacter.Materialize() may fire via InvokeLater
	// (Unity Update phase) before StoryManager's asyncLoader coroutine subscribes to
	// OnGuestReady (coroutine phase, post-Update). To prevent the miss, we check IsMaterialized
	// immediately after creating GuestCharacter and call the OnGuestReady handler right away
	// if it's already true. This makes the path isStared=true safe.
	//
	// If IsMaterialized is still false (isStared was false, stared event pending), we subscribe
	// to GuestMaterialized — the handler fires when stared eventually fires and Materialize
	// completes. asyncLoader subscribes to OnGuestReady in its second delegate, so as long as
	// that delegate has already run, the late-firing OnGuestReady is caught.
	protected void SetInterviewInstance(ScenaConMainProtagonistaFemenina obj)
	{
		Logger log = Logger.Create<GameSession>();
		// SMA 23.1: the model's genetics/NPC bind land a few seconds AFTER the `stared` event this runs on
		// (see GuestLoadGate / GuestDiagnostics). Materializing now captures a bare, geneless prefab. Defer
		// the gene-dependent materialize until the async load has completed; the gate has a timeout fallback
		// so it can never hang, and runs immediately for an already-loaded character.
		log.Info("[BE] SetInterviewInstance(ScenaConMain): waiting for async character load before materializing '{0}'", obj.character?.name ?? "null");
		GuestLoadGate.WhenLoaded(obj.character, delegate
		{
			SetInterviewInstanceLoaded(obj);
		});
	}

	private void SetInterviewInstanceLoaded(ScenaConMainProtagonistaFemenina obj)
	{
		Logger log = Logger.Create<GameSession>();
		log.Info("[BE] SetInterviewInstance(ScenaConMain): character loaded, creating GuestCharacter for '{0}'", obj.character?.name ?? "null");
		GuestCharacter guest = new GuestCharacter(obj.character, null, Scope);
		guest.SynchronizeCharacterWithInstance();
		log.Info("[BE] GuestCharacter created, IsMaterialized={0}", guest.IsMaterialized);
		Action handler = delegate
		{
			log.Info("[BE] GuestMaterialized handler fired — setting currentFemale and raising OnGuestReady");
			currentFemale = guest;
			InvokeOnGuestReady(currentFemale);
			log.Info("[BE] OnGuestReady invocation complete");
		};
		if (guest.IsMaterialized)
		{
			log.Info("[BE] Already materialized — calling handler immediately");
			handler();
		}
		else
		{
			log.Info("[BE] Not yet materialized — subscribing to GuestMaterialized event");
			guest.GuestMaterialized += handler;
		}
	}

	private void GuestPresenceChanged(EntrevistaConFemale.FemalePresencia last, EntrevistaConFemale.FemalePresencia current, EntrevistaConFemale sender)
	{
		if (current != EntrevistaConFemale.FemalePresencia.presente && sender == interview)
		{
			GuestCharacter copy = currentFemale;
			currentFemale = null;
			this.OnGuestLeft(copy);
			copy.Scope.Dispose();
		}
	}

	/// <summary>
	/// Tear the guest down if its character has been DESTROYED without the presence event firing.
	/// Returns true if it did.
	///
	/// WHY THIS EXISTS. <see cref="GuestPresenceChanged"/> already does the correct thing — null the
	/// guest, announce it, dispose the scope — but it only runs on femalePresenciaChanged from the
	/// tracked interview object. Bringing in a second model destroys the character WITHOUT
	/// satisfying that condition, so the guest scope is never disposed and every per-guest service
	/// keeps running against a dead character. The reference graph counts 57 call sites reading
	/// Guest.Impl across four assemblies; each is a latent crash, and ScopeSupport disposes the
	/// offending scope PERMANENTLY, so the feature does not come back for the rest of the session.
	///
	/// Guarding Impl to return null on destruction makes that state visible — but visible is not
	/// handled: those services should no longer exist. This watches the CONDITION instead of
	/// trusting one event to announce it.
	/// </summary>
	public bool DropGuestIfDestroyed()
	{
		if (currentFemale == null || currentFemale.Impl != null)
		{
			return false;
		}
		GuestCharacter copy = currentFemale;
		currentFemale = null;
		try
		{
			this.OnGuestLeft(copy);
		}
		catch (Exception)
		{
			// A failing subscriber must not prevent the disposal below — that is the whole point.
		}
		copy.Scope.Dispose();
		return true;
	}

	public void TerminateInterview()
	{
		if (interview != null && interview.femalePresencia == EntrevistaConFemale.FemalePresencia.presente)
		{
			interview.femalePresencia = EntrevistaConFemale.FemalePresencia.retiradaPorUserInteresado;
		}
	}

	public GameSession()
	{
		Scope.Name = "GameSession";
		currentPlayer = new PlayerCharacter();
		Scope.AddChild(currentPlayer);
		mainCamera = new MainCamera();
		modals = new MainModalWindow();
		poolManager = new PoolManager();
		Scope.AddChild(poolManager.Scope);
		Scope.Provide(this);
	}
}
