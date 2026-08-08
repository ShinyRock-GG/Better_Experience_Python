using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BetterExperience;
using BetterExperience.GameScopes;
using UnityEngine;
using Logger = BetterExperience.Logger;

namespace SeekLab;

/// <summary>
/// The testbed host. See SeekLab.csproj for why this exists at all.
///
/// THE HARD PART OF THIS FILE IS NOT LOADING — IT IS UNLOADING. ScriptEngine reloads by
/// destroying this GameObject and loading a fresh assembly; it knows nothing about the scope
/// graph we registered into. Anything not explicitly torn down in <see cref="OnDestroy"/>
/// survives as a second controller inside BetterExperience's dispatcher, and from that point on
/// every measurement is the sum of an old control law and a new one. That failure is invisible —
/// the character still moves, just wrongly — so it would be diagnosed as a bad gain.
///
/// ScopeSupport makes correct teardown available: disposing a service's scope unsubscribes every
/// handler registered through it, removes it from its parent's child list, and removes it from
/// the parent's service registry. So the rule here is that EVERY registration goes through a
/// scope we hold a reference to, and OnDestroy disposes them all.
/// </summary>
[BepInPlugin("rock.seeklab", "SeekLab (AutoSeek/AutoThrust testbed)", "1.0.0")]
public class SeekLabPlugin : BaseUnityPlugin
{
	private readonly Logger logger = new Logger { Prefix = "[SeekLab]:" };

	/// <summary>Features we injected into BE's plugin scope; disposed on unload.</summary>
	private readonly List<PluginService> injected = new List<PluginService>();

	/// <summary>
	/// Factories we appended to SessionTracker.InterviewServices, remembered so unload removes
	/// exactly ours. The list belongs to BetterExperience and contains its factories too; clearing
	/// it would silently disable half the mod.
	/// </summary>
	private readonly List<Func<PluginService>> ourFactories = new List<Func<PluginService>>();

	/// <summary>BetterExperience factories we pulled out of InterviewServices; restored on unload.</summary>
	private readonly List<Func<PluginService>> suppressedHostFactories = new List<Func<PluginService>>();

	private SessionTracker tracker;
	private bool attached;

	/// <summary>Seconds since we last checked our services are still alive.</summary>
	private float verifyT;

	private const float VerifyInterval = 0.5f;

	public void Awake()
	{
		try
		{
			Attach();
		}
		catch (Exception e)
		{
			SeekLabHandoff.LastError = e.ToString();
			SeekLabHandoff.Status = "attach FAILED: " + e.Message;
			logger.Error(e, "[SeekLab] attach failed - BetterExperience keeps ownership");
		}
	}

	public void Update()
	{
		if (SeekLabHandoff.RequestReattach)
		{
			SeekLabHandoff.RequestReattach = false;
			try
			{
				Detach();
				Attach();
			}
			catch (Exception e)
			{
				SeekLabHandoff.LastError = e.ToString();
				SeekLabHandoff.Status = "reattach FAILED: " + e.Message;
			}
			return;
		}

		// KEEP TRYING. Awake is not a reliable place to find another plugin: BepInEx plugin load
		// order is not guaranteed, and ScriptEngine can load this assembly before, during or long
		// after BetterExperience is up. A one-shot attach that missed simply left the lab idle
		// while BE kept ownership — which looks exactly like the lab working, and silently costs a
		// restart per iteration, i.e. the entire problem this assembly exists to solve.
		if (!attached)
		{
			try
			{
				Attach();
			}
			catch (Exception e)
			{
				SeekLabHandoff.LastError = e.ToString();
				SeekLabHandoff.Status = "attach FAILED: " + e.Message;
				// Stop hammering once it is failing for a real reason rather than a timing one.
				attached = true;
			}
			return;
		}

		// NOTICE WHEN OUR SERVICES HAVE DIED.
		//
		// Attached is a count from when we last attached, not a statement about now. If a service
		// throws, ScopeSupport disposes its scope permanently — and a model change does exactly
		// that — so the services are gone while Attached still reads 3 and the re-attach latch
		// below never fires. The lab then sits there owning the pelvis and doing nothing, which is
		// indistinguishable from the feature being off.
		//
		// Cheap enough to check twice a second: it is a walk of one scope's service list.
		verifyT += Time.deltaTime;
		if (verifyT > VerifyInterval)
		{
			verifyT = 0f;
			if (SeekLabHandoff.Attached > 0)
			{
				int live = 0;
				foreach (PluginService svc in ServicesInScope(tracker?.Current?.Guest?.Scope))
				{
					if (svc.Scope.Started)
					{
						live++;
					}
				}
				if (live == 0)
				{
					logger.Info("[SeekLab] live services are gone (character change or a service "
						+ "crash) - re-attaching when the guest is ready");
					SeekLabHandoff.Attached = 0;
				}
			}
		}

		// The guest arrives long after the plugin does. Registering the factories is not enough on
		// its own (they only fire at OnGuestReady, and that may already have passed), so latch on
		// as soon as a guest scope exists.
		if (SeekLabHandoff.Attached == 0 && tracker?.Current?.Guest?.Scope != null)
		{
			// A new guest means a new guest scope, and BE's InterviewServices factories run at
			// OnGuestReady. With its features config-disabled there is nothing to catch, but this
			// is the one moment host services can legitimately appear, so it is the one place worth
			// re-checking — an event, not a poll.
			SuppressHostServices();
			int live = AttachToCurrentGuest();
			if (live > 0)
			{
				SeekLabHandoff.Attached = live;
				SeekLabHandoff.Status = "OWNS pelvis; " + live + " service(s) live on the current guest";
				logger.Info("[SeekLab] {0}", SeekLabHandoff.Status);
			}
		}
	}

	public void OnDestroy()
	{
		try
		{
			Detach();
		}
		catch (Exception e)
		{
			// A failed detach is the worst outcome available here: BE stays stood down while our
			// dead services keep ticking. Say so loudly rather than letting it read as a tuning bug.
			logger.Error(e, "[SeekLab] DETACH FAILED - there may now be stale controllers running. "
				+ "Restart the game before trusting any further measurement.");
			SeekLabHandoff.Status = "DETACH FAILED - restart before measuring";
		}
	}

	// -------------------------------------------------------------------------------------

	private void Attach()
	{
		// Ask the Chainloader first. FindObjectOfType only sees ACTIVE, ENABLED components in
		// loaded scenes, which is not a promise BepInEx makes about a plugin's host object; the
		// Chainloader registry is the authoritative list of what is loaded.
		BetterExperience.Plugin be = null;
		foreach (PluginInfo info in Chainloader.PluginInfos.Values)
		{
			be = info.Instance as BetterExperience.Plugin;
			if (be != null)
			{
				break;
			}
		}
		if (be == null)
		{
			be = FindObjectOfType<BetterExperience.Plugin>();
		}
		if (be == null)
		{
			// NOT an error and NOT terminal: Update retries every frame. Load order between two
			// plugins is not guaranteed, so "not there yet" is the common case at Awake.
			SeekLabHandoff.Status = "waiting for BetterExperience to load";
			return;
		}

		// Take ownership BEFORE registering anything, so there is no frame in which both copies
		// are live. The order matters more than it looks: BE's services check this flag per tick.
		SeekLabHandoff.ExternalOwner = true;

		// Injecting a PluginFeature into BE's already-started plugin scope starts it immediately
		// (ScopeSupport.AddChild starts a child whose parent is already Started), so the config
		// binding and option exposure in these features run here exactly as they do at boot.
		injected.Add(be.AddService(new AutoThrustFeature()));
		injected.Add(be.AddService(new AutoSeekerFeature()));

		tracker = injected[0].Scope.Lookup<SessionTracker>();

		// SELF-HEAL FIRST. If a previous generation failed to detach — or never got the chance —
		// its services are still ticking, and the newly loaded assembly is the only code
		// guaranteed to run afterwards. Sweeping on LOAD rather than relying solely on unload is
		// what makes an interrupted reload recoverable instead of permanently corrupting the run.
		SweepStaleGenerations(injected[0].Scope.Parent);

		// Mission Control comes too, and must be suppressed on BE's side BEFORE ours registers —
		// two panels would stack in the same overlay slot. Its window binds to an AutoThrustService
		// INSTANCE, so BE's copy can only ever drive BE's now-dormant service: the controls would
		// look normal and do nothing.
		SuppressHostPluginService("MissionControl");
		injected.Add(be.AddService(new MissionControlFeature()));

		// The features registered their own InterviewServices factories during OnStart above.
		// Remember which entries are ours by the declaring assembly of the factory delegate, so
		// Detach removes exactly those and leaves BetterExperience's alone.
		foreach (Func<PluginService> f in tracker.InterviewServices)
		{
			if (f.Method.DeclaringType != null
				&& f.Method.DeclaringType.Assembly == typeof(SeekLabPlugin).Assembly
				&& !ourFactories.Contains(f))
			{
				ourFactories.Add(f);
			}
		}

		// KILL THE HOST'S LIVE COPIES OUTRIGHT.
		//
		// A cooperative flag is not enough. It only stops the code paths that check it, and BE's
		// seeker also holds a per-frame `updatingPelvisPosition` subscription plus penetration
		// callbacks that never see the flag. It also depends on the DEPLOYED BetterExperience.dll
		// actually containing the guards — and that DLL is locked while the game runs, so the
		// build being tested is routinely older than the one with the guard in it. Both failure
		// modes end the same way: two seekers on one pelvis, which is indistinguishable from a bad
		// control law and poisons every measurement taken to judge one.
		//
		// So take the actuator away instead of asking politely. Disposing the host service's scope
		// unsubscribes every handler it registered — the pelvis hook and the pene callbacks
		// included — and removes it from the guest registry. It comes back on the next scene load,
		// and Detach re-registers its factory, so this is suppression, not damage.
		SuppressHostServices();

		// LIVE ATTACH. InterviewServices factories are invoked once, at OnGuestReady. Registering
		// alone therefore does nothing until the next scene load — which would make every reload
		// cost a scene transition and defeat the point. If a guest is already present, instantiate
		// against its scope right now.
		int live = AttachToCurrentGuest();

		attached = true;
		SeekLabHandoff.Attached = live;
		SeekLabHandoff.LastError = "";
		SeekLabHandoff.Status = live > 0
			? "OWNS pelvis; " + live + " service(s) live on the current guest"
			: "OWNS pelvis; registered, waiting for a guest";
		logger.Info("[SeekLab] attached. {0}", SeekLabHandoff.Status);
		Census(injected[0].Scope.Parent);
	}

	/// <summary>
	/// Dispose BetterExperience's own AutoSeeker/AutoThrust services and unregister their
	/// factories, so exactly one assembly is driving the pelvis. Reflection is unavoidable:
	/// AutoSeekerService is a private nested type and cannot be named from here.
	/// </summary>
	private void SuppressHostServices()
	{
		System.Reflection.Assembly hostAsm = typeof(BetterExperience.Plugin).Assembly;

		// Stop new ones being created for the next guest, remembering them so Detach restores.
		for (int i = tracker.InterviewServices.Count - 1; i >= 0; i--)
		{
			Func<PluginService> f = tracker.InterviewServices[i];
			Type owner = f.Method.DeclaringType;
			if (owner != null && owner.Assembly == hostAsm && IsSeekOrThrust(owner.FullName))
			{
				suppressedHostFactories.Add(f);
				tracker.InterviewServices.RemoveAt(i);
			}
		}

		// Dispose the ones already live on this guest.
		ScopeSupport guestScope = tracker.Current?.Guest?.Scope;
		if (guestScope == null)
		{
			return;
		}
		System.Reflection.FieldInfo localsField = typeof(ScopeSupport).GetField("localObjects",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		if (localsField == null)
		{
			logger.Info("[SeekLab] cannot reach ScopeSupport.localObjects - host services NOT suppressed");
			return;
		}
		var locals = localsField.GetValue(guestScope) as System.Collections.IEnumerable;
		var doomed = new List<PluginService>();
		foreach (object o in locals)
		{
			PluginService svc = o as PluginService;
			if (svc != null && svc.GetType().Assembly == hostAsm && IsSeekOrThrust(svc.GetType().FullName))
			{
				doomed.Add(svc);
			}
		}
		foreach (PluginService svc in doomed)
		{
			try
			{
				svc.Scope.Dispose();
				logger.Info("[SeekLab] suppressed host service {0}", svc.GetType().Name);
			}
			catch (Exception e)
			{
				logger.Error(e, "[SeekLab] failed to suppress host service {0}", svc.GetType().Name);
			}
		}
	}

	private static bool IsSeekOrThrust(string fullName)
	{
		return fullName != null
			&& (fullName.Contains("AutoSeeker") || fullName.Contains("AutoThrust")
				|| fullName.Contains("MissionControl"));
	}

	/// <summary>
	/// Dispose a BetterExperience PLUGIN-scope feature by name. Mission Control is registered on
	/// the plugin scope rather than per-guest, so the guest-scope sweep never sees it.
	/// </summary>
	private void SuppressHostPluginService(string nameFragment)
	{
		System.Reflection.Assembly hostAsm = typeof(BetterExperience.Plugin).Assembly;
		System.Reflection.FieldInfo localsField = typeof(ScopeSupport).GetField("localObjects",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		if (localsField == null)
		{
			return;
		}
		// The features we already injected are children of BE's plugin scope, so its scope is
		// reachable as their parent.
		ScopeSupport pluginScope = injected.Count > 0 ? injected[0].Scope.Parent : null;
		if (pluginScope == null)
		{
			return;
		}
		var locals = localsField.GetValue(pluginScope) as System.Collections.IEnumerable;
		var doomed = new List<PluginService>();
		foreach (object o in locals)
		{
			PluginService svc = o as PluginService;
			if (svc != null && svc.GetType().Assembly == hostAsm
				&& svc.GetType().FullName != null
				&& svc.GetType().FullName.Contains(nameFragment))
			{
				doomed.Add(svc);
			}
		}
		foreach (PluginService svc in doomed)
		{
			try
			{
				svc.Scope.Dispose();
				logger.Info("[SeekLab] suppressed host plugin service {0}", svc.GetType().Name);
			}
			catch (Exception e)
			{
				logger.Error(e, "[SeekLab] failed to suppress {0}", svc.GetType().Name);
			}
		}
	}

	private int AttachToCurrentGuest()
	{
		GameSession session = tracker?.Current;
		ScopeSupport guestScope = session?.Guest?.Scope;
		if (guestScope == null)
		{
			return 0;
		}
		// WAIT FOR A LIVE GUEST, NOT MERELY A PRESENT ONE.
		//
		// During a model change the session still holds a Guest whose Impl has been destroyed. A
		// destroyed Unity object is not a null reference, so `Guest != null` passes and the
		// services then throw on first touch — taking their scopes down permanently and burying
		// the cause under BE's own crash-handler failure. Unity's == operator sees through it.
		//
		// Returning 0 here is not a failure: Update retries every frame while Attached is 0, so
		// the services come up on their own as soon as the new character is assembled.
		if (session.Guest.Impl == null || session.Player == null
			|| session.Player.GameObject == null)
		{
			return 0;
		}
		int n = 0;
		foreach (Func<PluginService> f in ourFactories)
		{
			try
			{
				guestScope.AddService(f());
				n++;
			}
			catch (Exception e)
			{
				// One service failing to start must not prevent the other from starting — the same
				// per-service guard SessionTracker applies, and for the same reason.
				logger.Error(e, "[SeekLab] live attach of one service failed");
				SeekLabHandoff.LastError = e.ToString();
			}
		}
		return n;
	}

	private void Detach()
	{
		if (tracker != null)
		{
			foreach (Func<PluginService> f in ourFactories)
			{
				tracker.InterviewServices.Remove(f);
			}
		}
		ourFactories.Clear();

		// Dispose the live per-guest services first. They are children of the guest scope, not of
		// the features we injected, so disposing the features would NOT take them with it — that
		// asymmetry is exactly the kind of thing that leaves a controller running.
		foreach (PluginService live in FindLiveLabServices())
		{
			try
			{
				live.Scope.Dispose();
			}
			catch (Exception e)
			{
				logger.Error(e, "[SeekLab] disposing a live service failed");
			}
		}

		foreach (PluginService f in injected)
		{
			try
			{
				f.Scope.Dispose();
			}
			catch (Exception e)
			{
				logger.Error(e, "[SeekLab] disposing an injected feature failed");
			}
		}
		injected.Clear();

		// Give the host its factories back, so unloading the lab restores the shipped mod rather
		// than leaving the game with no seeker at all.
		if (tracker != null)
		{
			foreach (Func<PluginService> f in suppressedHostFactories)
			{
				tracker.InterviewServices.Add(f);
			}
		}
		suppressedHostFactories.Clear();

		// Hand the pelvis back LAST, once nothing of ours can still act on it.
		SeekLabHandoff.ExternalOwner = false;
		attached = false;
		SeekLabHandoff.Attached = 0;
		SeekLabHandoff.Status = "detached - BetterExperience owns the pelvis";
		logger.Info("[SeekLab] detached; ownership returned to BetterExperience.");
	}

	/// <summary>
	/// Live per-guest services that belong to THIS assembly. Identified by assembly rather than by
	/// type name, because after a reload the previous SeekLab assembly is still in memory with
	/// identically-named types — matching on name would dispose the wrong generation's services,
	/// or fail to dispose the right one.
	/// </summary>
	private IEnumerable<PluginService> FindLiveLabServices()
	{
		// MATCH BY ASSEMBLY *NAME*, AND ENUMERATE — both of those are load-bearing.
		//
		// The first version compared `Assembly` by IDENTITY and asked Find<T> for one instance of
		// each known type. Both are wrong once a reload has happened, and wrong in the direction
		// that silently accumulates controllers:
		//
		//   - Every reload produces a NEW Assembly object. The previous generation's service is a
		//     different Type from a different Assembly, so an identity comparison rejects it and
		//     it is never disposed. It keeps ticking, forever.
		//   - Find<T> returns the FIRST match of ONE type, so even with the right predicate it
		//     could only ever have cleaned up one generation per reload.
		//
		// Result, measured after ~8 reloads: several seekers commanding one pelvis, which showed
		// up as errY flipping sign and pelvisY jumping 80 mm per logged frame against a commanded
		// 3 mm — three numbers that cannot describe a single controller.
		//
		// Matching on the assembly NAME catches every generation, including the ones this code is
		// not running in.
		foreach (PluginService svc in ServicesInScope(tracker?.Current?.Guest?.Scope))
		{
			yield return svc;
		}
	}

	/// <summary>
	/// Count every live seek/thrust/panel service in both scopes, from EVERY assembly, and report
	/// it. This is the measurement that answers "is something running twice" — the one question
	/// that has repeatedly been guessed at instead of measured.
	///
	/// SeekLabHandoff.Attached cannot answer it: that counts only what THIS generation created, so
	/// a leftover generation or a live BetterExperience copy is invisible to it. Duplicate
	/// controllers have no visible marker of their own — two controllers on one pelvis just produce
	/// motion belonging to neither control law — so the count has to be taken deliberately.
	/// </summary>
	private void Census(ScopeSupport pluginScope)
	{
		var tally = new Dictionary<string, int>();
		foreach (ScopeSupport scope in new[] { tracker?.Current?.Guest?.Scope, pluginScope })
		{
			foreach (PluginService svc in AllServicesInScope(scope))
			{
				Type t = svc.GetType();
				if (!IsSeekOrThrust(t.FullName))
				{
					continue;
				}
				string key = t.Assembly.GetName().Name + "." + t.Name;
				tally[key] = (tally.TryGetValue(key, out int n) ? n : 0) + 1;
			}
		}
		var parts = new List<string>();
		bool dupes = false;
		foreach (var kv in tally)
		{
			parts.Add(kv.Key + "=" + kv.Value);
			if (kv.Value > 1)
			{
				dupes = true;
			}
		}
		string line = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "(none)";
		SeekLabHandoff.LastError = dupes ? "DUPLICATES: " + line : "";
		if (dupes)
		{
			logger.Error(null, "[SeekLab] CENSUS - DUPLICATE CONTROLLERS: {0}. Every measurement "
				+ "taken in this state is void.", line);
		}
		else
		{
			logger.Info("[SeekLab] census: {0}", line);
		}
	}

	/// <summary>
	/// Is this assembly ANY generation of SeekLab?
	///
	/// THE BUG THIS EXISTS TO FIX. ScriptEngine does not load the assembly under its own name — it
	/// renames each load, e.g. "SeekLab-639217412782518635". So the obvious test,
	/// <c>GetName().Name == "SeekLab"</c>, matched NOTHING: the stale-generation sweep reported
	/// zero every time, detach cleaned up nothing, and five generations accumulated, each with its
	/// own seeker, thruster and panel. Five controllers on one pelvis is five times the commanded
	/// motion, five hotkey handlers arming on one keypress, and five copies of every static — so
	/// the speed slider wrote to the newest generation's while four others ran at their default.
	///
	/// Every symptom reported over several rounds — "moving twice as fast", "four arms per Space",
	/// "insanely fast", "the slider does nothing" — is this one line.
	/// </summary>
	private static bool IsLabAssembly(System.Reflection.Assembly asm)
	{
		string n = asm.GetName().Name;
		return n != null
			&& (n == "SeekLab" || n.StartsWith("SeekLab-", StringComparison.Ordinal));
	}

	/// <summary>Every PluginService in a scope, whatever assembly it came from.</summary>
	private static IEnumerable<PluginService> AllServicesInScope(ScopeSupport scope)
	{
		if (scope == null)
		{
			yield break;
		}
		System.Reflection.FieldInfo localsField = typeof(ScopeSupport).GetField("localObjects",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		if (localsField == null)
		{
			yield break;
		}
		var locals = localsField.GetValue(scope) as System.Collections.IEnumerable;
		var found = new List<PluginService>();
		foreach (object o in locals)
		{
			if (o is PluginService svc)
			{
				found.Add(svc);
			}
		}
		foreach (PluginService svc in found)
		{
			yield return svc;
		}
	}

	/// <summary>Every PluginService in a scope that came from ANY generation of SeekLab.</summary>
	private static IEnumerable<PluginService> ServicesInScope(ScopeSupport scope)
	{
		if (scope == null)
		{
			yield break;
		}
		System.Reflection.FieldInfo localsField = typeof(ScopeSupport).GetField("localObjects",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		if (localsField == null)
		{
			yield break;
		}
		var locals = localsField.GetValue(scope) as System.Collections.IEnumerable;
		var found = new List<PluginService>();
		foreach (object o in locals)
		{
			PluginService svc = o as PluginService;
			if (svc != null && IsLabAssembly(svc.GetType().Assembly))
			{
				found.Add(svc);
			}
		}
		// Snapshot before yielding: the caller disposes, which mutates this very list.
		foreach (PluginService svc in found)
		{
			yield return svc;
		}
	}

	/// <summary>
	/// Dispose every SeekLab service left over from ANY earlier generation, in both the guest and
	/// plugin scopes. Called on LOAD as well as unload: a detach that failed — or never ran,
	/// because ScriptEngine unloaded us abruptly — must not be able to leave a controller running,
	/// and the newly loaded generation is the only code guaranteed to execute afterwards.
	/// </summary>
	private void SweepStaleGenerations(ScopeSupport pluginScope)
	{
		int n = 0;
		foreach (ScopeSupport scope in new[] { tracker?.Current?.Guest?.Scope, pluginScope })
		{
			foreach (PluginService svc in ServicesInScope(scope))
			{
				if (svc.GetType().Assembly == typeof(SeekLabPlugin).Assembly)
				{
					continue;   // ours, still starting up
				}
				try
				{
					svc.Scope.Dispose();
					n++;
				}
				catch (Exception e)
				{
					logger.Error(e, "[SeekLab] failed to dispose stale {0}", svc.GetType().Name);
				}
			}
		}
		if (n > 0)
		{
			logger.Info("[SeekLab] swept {0} stale service(s) from earlier generations - "
				+ "they had been running alongside the current build.", n);
		}
	}
}
