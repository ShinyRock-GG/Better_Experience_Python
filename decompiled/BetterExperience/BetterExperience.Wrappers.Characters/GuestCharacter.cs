using System;
using System.Reflection;
using Assets;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Chars.Alteradores;
using Assets._ReusableScripts.CuchiCuchi.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Controllers.Ojos.Parpadeos;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.Genetica.NPCs;
using BetterExperience.GameScopes;
using BetterExperience.Wrappers.Physics;
using BetterExperience.Wrappers.Pools;
using HarmonyLib;
using UnityEngine;

namespace BetterExperience.Wrappers.Characters;

public class GuestCharacter
{
	// SMA 23.1: BaseFemalePoseLoader is marked [Obsolete(error:true)] — cannot reference it
	// directly in source. Accessed via reflection so the project compiles against 23.1 assemblies.
	// If the type is fully removed in a future SMA version, AccessTools.TypeByName returns null
	// and all three fields become null; the constructor and GetCurrentPoseStr() handle this
	// gracefully via null-conditional operators (?.).
	private static readonly Type _poseLoaderType =
		AccessTools.TypeByName("Assets._ReusableScripts.CuchiCuchi.Dependentes.ControllerPoses.BaseFemalePoseLoader");
	private static readonly EventInfo _poseChangedEvent =
		_poseLoaderType?.GetEvent("poseChanged");
	private static readonly PropertyInfo _currentPoseProp =
		_poseLoaderType?.GetProperty("currentPose");

	private Component poseLoader;

	private ISujetoIdentificableNpc _providedGeneticsChar;

	public ScopeSupport Scope { get; } = new ScopeSupport
	{
		Name = "GuestChar"
	};

	public Observable GuestValuesChanged { get; } = new Observable();

	public PhysicalPuppet Puppet { get; private set; }

	public GuestHeadController HeadController { get; private set; }

	public ComboGestureController GesturesController { get; private set; }

	public FemaleChar Impl { get; }

	public GuestInstance GuestInstance { get; private set; }

	public RadialMenu RadialMenu { get; private set; }

	public GameObject RootObject => Impl.gameObject;

	public LookAtControllerV2 LookAtComponent { get; private set; }

	public OjosExpresionController EyesExpressionComponent { get; private set; }

	public ModifierManager ModifierManager { get; private set; }

	public bool IsMaterialized { get; private set; }

	public event Action PoseChanged = delegate
	{
	};

	public event Action GuestMaterialized = delegate
	{
	};

	public GuestCharacter(FemaleChar currentFemaleCharacter, ISujetoIdentificableNpc providedGenetics, ScopeSupport parentScope)
	{
		GuestCharacter guestCharacter = this;
		Impl = currentFemaleCharacter;
		_providedGeneticsChar = providedGenetics;
		parentScope.AddChild(Scope);
		DispatcherService dispatcher = Scope.Lookup<DispatcherService>();

		// SMA 23.1 TIMING ISSUE — isStared vs asyncLoader race condition
		//
		// InvokeLater(Materialize) drains during Unity's Update() phase (PostUpdate).
		// StoryManager's asyncLoader subscribes to GuestMaterialized/OnGuestReady during
		// the coroutine phase, which runs AFTER Update().
		//
		// In the old EntrevistaConFemale flow, isStared was always false at scene load,
		// so we took the event-handler path — Materialize was deferred until the stared
		// event fired, by which time asyncLoader had already subscribed to OnGuestReady.
		//
		// In SMA 23.1's ScenaConMainProtagonistaFemenina, isStared is SOMETIMES true at
		// scene load, so InvokeLater(Materialize) fires in the same Update frame as the
		// scene load — BEFORE asyncLoader has subscribed. GuestMaterialized fires and is
		// missed by asyncLoader's OnGuestReady subscription, breaking the Python chain.
		//
		// The fix for this race is in GameSession.SetInterviewInstance(ScenaConMain):
		// it checks IsMaterialized AFTER creating GuestCharacter and calls the OnGuestReady
		// handler immediately if already true, bypassing the asyncLoader timing dependency.
		// StoryManager.OnSceneLoaded also checks ss.Guest != null directly in its asyncLoader
		// delegate, catching already-materialized guests without needing the event.
		if (!currentFemaleCharacter.isStared)
		{
			Scope.EventHandler(delegate(CustomMonobehaviourEventHandler x)
			{
				currentFemaleCharacter.stared += x;
			}, delegate(CustomMonobehaviourEventHandler x)
			{
				currentFemaleCharacter.stared -= x;
			}, delegate
			{
				dispatcher.InvokeLater(guestCharacter.Materialize);
			});
		}
		else
		{
			dispatcher.InvokeLater(Materialize);
		}
		poseLoader = _poseLoaderType != null ? Impl.GetComponent(_poseLoaderType) : null;
		Scope.EventHandler(delegate(Action<AnimController> x)
		{
			if (guestCharacter.poseLoader != null)
				_poseChangedEvent?.AddEventHandler(guestCharacter.poseLoader, x);
		}, delegate(Action<AnimController> x)
		{
			if (guestCharacter.poseLoader != null)
				_poseChangedEvent?.RemoveEventHandler(guestCharacter.poseLoader, x);
		}, delegate
		{
			guestCharacter.PoseChanged();
		});
	}

	public void Materialize()
	{
		// IMPORTANT: Materialize() is called via DispatcherService.InvokeLater(), which drains
		// during PostUpdate() — a method tagged [Timed]. The [Timed] attribute wraps the call
		// in a try-catch that silently swallows ALL exceptions, producing zero log output on
		// failure. This makes debugging impossible without the explicit try-catch below.
		//
		// DO NOT remove the try-catch inside this method. Without it, any exception here
		// vanishes completely, IsMaterialized stays false, GuestMaterialized never fires,
		// OnGuestReady never fires, and Python never starts — with no error in the log.
		Logger log = Logger.Create<GuestCharacter>();
		log.Info("[BE] Materialize() started for char '{0}'", Impl?.name ?? "null");
		try
		{
			Puppet = new PhysicalPuppet(Impl.gameObject);
			log.Info("[BE] Materialize step 1/7: PhysicalPuppet OK");
			HeadController = new GuestHeadController(Impl.gameObject, Scope);
			log.Info("[BE] Materialize step 2/7: GuestHeadController OK");
			GesturesController = new ComboGestureController(Impl.gameObject, Scope);
			log.Info("[BE] Materialize step 3/7: ComboGestureController OK");
			Scope.AddChild(HeadController);
			RadialMenu = new RadialMenu(Impl.gameObject);
			log.Info("[BE] Materialize step 4/7: RadialMenu OK");
			PoolManager pools = Scope.Lookup<GameSession>()?.PoolManager;
			log.Info("[BE] Materialize step 5/7: PoolManager={0}", pools != null ? "found" : "NULL");
			// SMA 23.1 SYSTEMIC FIX — Do NOT create GuestInstance when genetics are absent.
			//
			// ScenaConMainProtagonistaFemenina passes null for the genetics character because its
			// characters are pre-built static assets, not pool-generated NPCs. Previously we called
			// new GuestInstance(null, pool) which produced an object that looked non-null but had a
			// null internal Instance field. Every downstream null check (GuestInstance == null) passed
			// incorrectly, then crashed when any code called Instance.aparienciaFisica or similar.
			//
			// By keeping GuestInstance null here, all existing null checks throughout the codebase
			// (GeneToolWindow.OnStart, GIOService.GenericExport, etc.) correctly skip genetics-
			// dependent operations rather than crashing. GuestInstance == null is the correct and
			// honest representation of "this character has no genetics backing."
			//
			// FindGuest by ID is still attempted — a static character COULD theoretically have a
			// matching pool entry if someone added one manually, though this is unlikely in practice.
			if (pools != null)
			{
				GuestInstance = pools.FindGuest(Impl.ID_Unico.ToString());
				if (GuestInstance == null && pools.Count > 0 && _providedGeneticsChar != null)
				{
					// Only create a GuestInstance when we have genetics to back it.
					// ScenaConMain path: _providedGeneticsChar is null → skip, leave GuestInstance null.
					GuestInstance = new GuestInstance(_providedGeneticsChar, pools.AnyPool);
				}
				log.Info("[BE] Materialize step 5b: GuestInstance={0}", GuestInstance != null ? "found" : "NULL (no genetics — ScenaConMain character)");
			}
			LookAtComponent = Impl.GetComponentInChildren<LookAtControllerV2>();
			EyesExpressionComponent = Impl.GetComponentInChildren<OjosExpresionController>();
			ModifierManager = new ModifierManager(Impl.gameObject);
			log.Info("[BE] Materialize step 6/7: ModifierManager OK");
			IsMaterialized = true;
			log.Info("[BE] Materialize step 7/7: complete — firing GuestMaterialized");
			this.GuestMaterialized();
			log.Info("[BE] GuestMaterialized fired OK");
		}
		catch (Exception ex)
		{
			log.Error("[BE] Materialize() THREW: {0}\n{1}", ex.GetType().Name, ex);
		}
	}

	public string GetCurrentPoseStr()
	{
		return (_currentPoseProp?.GetValue(poseLoader))?.ToString() ?? "";
	}

	public void SynchronizeCharacterWithInstance()
	{
		// SMA 23.1 BAND-AID — same null risk as ModifierManager
		//
		// GetComponentEnRoot<AlteradoresDeAparienciaFemenina/Femenina>() may return null in
		// 23.1 (same root cause as the ModifierManager null guard). This call happens in
		// GameSession.SetInterviewInstance BEFORE Materialize(), so it can crash independently.
		// Null-guarding here until we identify where these components live in 23.1.
		//
		// TODO: resolve the same TODO in ModifierManager — find the 23.1 equivalents of these
		// modifier components and update both search sites consistently.
		var mesh = Impl.GetComponentEnRoot<AlteradoresDeAparienciaFemenina>();
		var script = Impl.GetComponentEnRoot<AlteradoresDePersonalidadFemenina>();
		if (mesh != null) mesh.flagToForceUpdateValores = true;
		if (script != null) script.flagToForceUpdateValores = true;
		GuestValuesChanged.Invoke();
	}
}
