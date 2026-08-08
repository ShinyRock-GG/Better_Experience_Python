using System;
using System.Collections.Generic;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.AI.Reactores.Effector;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Ai.Reactores.Orales;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using Assets;
using Assets.TValle.BeachGirl;
using BepInEx.Configuration;
// SEEKLAB: these two were implicit while this file lived in BetterExperience.Features — the
// enclosing namespace and its parent are always in scope. They import Logger, PlayerScaler and
// PlayerCharacter's internals. They do NOT make AutoSeekerFeature/AutoThrustFeature ambiguous:
// C# resolves a name in the ENCLOSING namespace (SeekLab) before consulting any using-directive,
// so those bind to this assembly's copies, which is what we want.
using BetterExperience;
using BetterExperience.Features;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using UnityEngine;

// SEEKLAB COPY. Byte-identical to BetterExperience.Features/AutoSeekerFeature.cs except for the
// changes forced by living in another assembly, each marked "SEEKLAB:". Keep that discipline —
// the merge-back is only cheap while the diff stays small enough to read.
//
// SEEKLAB: namespace. It cannot stay BetterExperience.Features: this assembly REFERENCES
// BetterExperience, which already declares AutoSeekerFeature/AutoThrustFeature there, and the
// two would be ambiguous (CS0433) at every use site.
namespace SeekLab;

/// <summary>
/// LIVE SEEK TUNING. Public statics on purpose: a static is reachable through the dev probe's
/// T: root with no service lookup, so these can be changed WHILE the game runs —
///
///     curl "http://localhost:8910/set?path=T:AutoSeekTuning.SpeedScale&amp;value=6"
///
/// — which turns "is it fast enough?" into a ten-second experiment instead of a rebuild, a
/// restart and a re-setup.
///
/// The reason to push the speed HARD is not impatience. A placement loop that converges at 6x
/// has real stability margin; one that only converges slowly is sitting on the edge and will
/// break the moment anything else changes. Fast first, then slow it to taste — if it works fast
/// it will work slow, and the converse is emphatically not true.
/// </summary>
public static class AutoSeekTuning
{
	/// <summary>Multiplies every seek translation and rotation rate. 1 = shipped behaviour.</summary>
	public static float SpeedScale = 4f;

	/// <summary>Approach time constant, seconds. Smaller = snappier convergence.</summary>
	public static float ApproachTau = 0.35f;

	/// <summary>Degrees per second of yaw during the rotation stage.</summary>
	public static float RotateDegPerSec = 50f;

	/// <summary>Set true to log a line per placement tick. Off by default — it is per-frame.</summary>
	public static bool Verbose;

	/// <summary>
	/// Where the boca target sits along the hole axis, metres. POSITIVE moves it OUTWARD, toward
	/// and past the lip surface; negative pushes it into the mouth.
	///
	/// `Labios.Closing_Entrada` is the game's mouth entrance, but it sits INSIDE the lips — so
	/// aiming at it asks the tip to be somewhere it can only reach by already having entered, and
	/// the approach spends its time trying to occupy a point behind a surface it has not passed.
	/// The lip surface is where contact should be made and where the negotiation should start.
	///
	/// Live-tunable because the correct value is a property of the mesh, not of the code:
	///     curl -X POST -d '' ".../set?path=T:AutoSeekTuning.BocaTargetOut&amp;value=0.03"
	/// Dial it until the white tip marker sits ON the lips rather than through them.
	/// </summary>
	public static float BocaTargetOut = 0.02f;

	/// <summary>
	/// Boca presentation tilt, SIGNED degrees. Positive is intended to be nose-up.
	///
	/// Live-tunable because the sign is a fact about the game's frames, not about the intent, and
	/// it has now been guessed wrong in both directions. Flipping it in game answers in seconds
	/// what deriving it from Quaternion.AngleAxis conventions has twice failed to:
	///
	///     curl -X POST -d '' ".../set?path=T:SeekLab.AutoSeekTuning.BocaUpTilt&amp;value=-8"
	///
	/// The align log prints axisY alongside it; a nose-up SHAFT means a NEGATIVE outward-axis y,
	/// because the shaft points into the mouth, opposite the outward axis.
	/// </summary>
	public static float BocaUpTilt = 8f;

	/// <summary>
	/// Measure the lateral miss against <c>worldOutHoleDirection</c> (true) or the bone axis
	/// <c>-hole.forward</c> (false). They differ by ~13 degrees. FALSE is the last configuration
	/// placement was known to work in; true was tried once and broke approach from every position,
	/// so it stays opt-in until measured. See LateralOffsetFromAxis.
	/// </summary>
	public static bool UseOutHoleAxis;

	/// <summary>
	/// COLLINEARITY GATE, degrees. The shaft must lie this close to the hole's own axis before the
	/// dock is attempted at all.
	///
	/// Position alone was the entire handover condition, which is why a shaft could arrive at the
	/// right POINT while pointing somewhere else entirely and still commit to the press — and a
	/// press along the wrong axis does not insert, it bows the shaft and shoves the hole aside.
	/// Checking the angle first turns "arrived" into "arrived AND aimed".
	/// </summary>
	public static float CollinearEnterDeg = 12f;

	/// <summary>
	/// Collinearity at which an in-progress dock is ABANDONED, degrees. Deliberately wider than
	/// the entry gate: a dock that drifts a little should be allowed to finish, but one that has
	/// clearly lost the line should retreat and re-approach rather than grind at a bad angle.
	/// The gap between the two IS the hysteresis — a single threshold would chatter.
	/// </summary>
	public static float CollinearAbortDeg = 28f;
}

internal class AutoSeekerFeature : PluginFeature
{
	// SEEKLAB: was `private`. The lab host has to name this type to dispose live instances on
	// reload, and a private nested class is unreachable even from the same assembly.
	internal class AutoSeekerService : SessionService
	{
		private const float TRANSLATION_SPEED = 0.1f;

		private const int MAX_SOLVABLE_V_ANGLE = 80;

		private const float TRANSLATION_PRECISSION = 0.005f;

		/// <summary>
		/// Dead zone while the dock cycle is running.
		///
		/// TRANSLATION_PRECISSION is 5 mm: below that, both `fixY` and the flat move switch off. It
		/// is a sensible resting dead zone for a coarse approach and it makes the 3 mm calibration
		/// gate UNREACHABLE BY CONSTRUCTION — the last log showed every error component inside the
		/// dead zone (miss = 0.0000, 0.0043, 0.0030) with the tip parked at calErr 0.0046 and all
		/// four attempts timing out one and a half millimetres short.
		///
		/// A servo cannot hold a tolerance finer than the error at which it stops correcting, so
		/// the dock cycle needs its own, well under its gate. Only in effect near the hole, so the
		/// coarse approach keeps its quiet dead zone and does not jitter at rest.
		/// </summary>
		private const float DockMovePrecision = 0.0008f;

		private IInputHandle hotkey;

		private PelvisMovementController ctl;

		private AutoplacerState state;

		private Vector3 pelvisTarget;

		private List<Behaviour> unwantedBehaviors = new List<Behaviour>();

		private OverlayService overlay;

		private AutoThrustFeature.AutoThrustService autoThruster;

		private bool autoscale;

		public float MaxDepth
		{
			get
			{
				if (autoThruster == null)
				{
					return 0.3f;
				}
				return autoThruster.MaxDepth / 2f;
			}
		}

		public ConfigEntry<KeyboardShortcut> HotkeyCfg { get; internal set; }

		public ConfigEntry<bool> Autothrust { get; internal set; }

		public override void OnStart()
		{
			base.OnStart();
			overlay = Lookup<OverlayService>();
			DispatcherService disp = Lookup<DispatcherService>();
			disp.DoUpdate.Add(OnUpdate, base.Scope);
			hotkey = disp.Input.KeyboardEvent(HotkeyCfg, base.Scope);
			ctl = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.vagHole.entrada);
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.anusHole.entrada);
			base.Session.Guest.Puppet.GetIKBoneTransform(base.Session.Guest.Impl.bocaHole.entrada);
			base.Scope.EventHandler(delegate(UpdatingPelvisPosition h)
			{
				ctl.updatingPelvisPosition += h;
			}, delegate(UpdatingPelvisPosition h)
			{
				ctl.updatingPelvisPosition -= h;
			}, OnUpdatingPelvisPosition);

			// PENETRATION HANDSHAKE — subscribe to the game's own signals.
			//
			// Penetration is negotiated: Penetrador raises peneTryingEnterInHole when the tip is at
			// an entrance attempting entry, Penetraciones.AceptaPenetracion decides, and
			// GetNextCoolDown paces retries. Until now the seeker inferred all of that from
			// geometry — "within 2 cm" stood in for "trying", and a guessed 1.2 s stood in for the
			// cooldown period. Both were guesses about a thing the game states outright.
			//
			// Subscribing replaces the guesses with the signal. Wrapped in Scope.EventHandler, the
			// same helper the pelvis event above uses, so unsubscribe is tied to scope lifetime and
			// cannot leak.
			try
			{
				Penetrador pen = base.Session.Player.Character.pene;
				base.Scope.EventHandler(delegate(IPeneCallbacksHandler h)
				{
					pen.peneTryingEnterInHole += h;
				}, delegate(IPeneCallbacksHandler h)
				{
					pen.peneTryingEnterInHole -= h;
				}, OnPeneTryingEnter);
				base.Scope.EventHandler(delegate(IPeneCallbacksHandler h)
				{
					pen.peneEnteredInHole += h;
				}, delegate(IPeneCallbacksHandler h)
				{
					pen.peneEnteredInHole -= h;
				}, OnPeneEntered);
			}
			catch (Exception e)
			{
				// Non-fatal by design: the distance-based dwell remains as a fallback, so a hole
				// type that never raises these (boca is the suspect) behaves as it does today
				// rather than breaking.
				logger.Info("[AutoSeek] could not subscribe to penetration events ({0}) - "
					+ "falling back to distance-based dwell", e.Message);
			}

			InitUnwantedBehaviors();
			autoThruster = TryLookup<AutoThrustFeature.AutoThrustService>();
			autoscale = TryLookup<PlayerScaler.ScalerService>() != null;
		}

		private void InitUnwantedBehaviors()
		{
			// Called again after a model change, so it must not accumulate — otherwise every swap
			// leaves the previous character's dead entries in the list forever.
			unwantedBehaviors.Clear();
			ReactorConEffectorAEstimulosTactiles[] componentsInChildren = base.Session.Guest.Impl.GetComponentsInChildren<ReactorConEffectorAEstimulosTactiles>();
			foreach (ReactorConEffectorAEstimulosTactiles c in componentsInChildren)
			{
				unwantedBehaviors.Add(c);
			}
			ReactorSexAtPorVerPene[] componentsInChildren2 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorVerPene>();
			foreach (ReactorSexAtPorVerPene c2 in componentsInChildren2)
			{
				unwantedBehaviors.Add(c2);
			}
			ReactorSexAtPorTocarPene[] componentsInChildren3 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorTocarPene>();
			foreach (ReactorSexAtPorTocarPene c3 in componentsInChildren3)
			{
				unwantedBehaviors.Add(c3);
			}
			ReactorSexAtPorSerPenetrada[] componentsInChildren4 = base.Session.Guest.Impl.GetComponentsInChildren<ReactorSexAtPorSerPenetrada>();
			foreach (ReactorSexAtPorSerPenetrada c4 in componentsInChildren4)
			{
				unwantedBehaviors.Add(c4);
			}
		}

		private void OnUpdatingPelvisPosition(ref Vector3 currentLocalTarget, Transform effectorTransform, PelvisMovementController sender)
		{
			pelvisTarget = currentLocalTarget;
		}

		/// <summary>Wall-clock of the most recent "trying to enter" signal, and which hole it was
		/// for. Time-stamped rather than a bool because the interesting question is always "is the
		/// game CURRENTLY attempting entry", and an event only tells you about an instant.</summary>
		private float tryingSince = -99f;

		private IHole tryingHole;

		/// <summary>True while the game is actively attempting entry into the hole we are seeking.
		/// The staleness window covers the gap between retry attempts — Penetraciones paces those
		/// itself, so a signal a moment ago still means "in progress", not "over".</summary>
		private bool GameIsTryingToEnter =>
			Time.time - tryingSince < TryingSignalStaleSeconds
			&& (state == null || tryingHole == null || SameHole(tryingHole, state.Hole));

		/// <summary>Compare by entrance position: IHole and the Transform the seeker holds are
		/// different types, and BE's Hole transform is a proxy that MIRRORS entrada rather than
		/// being it, so reference equality would never match.</summary>
		private bool SameHole(IHole h, Transform target)
		{
			try
			{
				return h != null && target != null
					&& (h.entrada.position - target.position).sqrMagnitude < 0.01f;
			}
			catch
			{
				return true;
			}
		}

		private void OnPeneTryingEnter(IHole hole, IPene pene)
		{
			tryingSince = Time.time;
			tryingHole = hole;
		}

		private void OnPeneEntered(IHole hole, IPene pene)
		{
			// Authoritative completion. Polling isPenetrating still works and stays as the primary
			// exit, but this fires on the exact frame rather than up to one tick later.
			logger.Info("[AutoSeek] game reports ENTERED - handing off");
			if (state != null && state.ExitReason == ExitReason.None)
			{
				state.ExitReason = ExitReason.Completed;
			}
		}

		private void SetScriptsEnabled(bool enabled)
		{
			// THESE OUTLIVE THE CHARACTER THEY CAME FROM.
			//
			// unwantedBehaviors is captured once, from the guest present at OnStart. Calling in a
			// new model destroys those components, and a destroyed Unity object is not a null
			// REFERENCE — the managed wrapper survives, so the list still looks populated while
			// every native object behind it is gone. Setting .enabled then throws inside native
			// code, the exception escapes OnUpdate, and ScopeSupport shuts the ENTIRE seeker down:
			//
			//     Shutting down scope AutoSeekerService due to uncaught exception
			//     NullReferenceException at UnityEngine.Behaviour.set_enabled
			//
			// Unity's == operator reports destroyed objects as null, which is exactly what makes
			// this checkable. Prune as we go, and re-capture from the current guest when the list
			// has emptied out, so a model swap costs a rebuild of the list rather than the feature.
			bool anyDead = false;
			for (int i = 0; i < unwantedBehaviors.Count; i++)
			{
				Behaviour ub = unwantedBehaviors[i];
				if (ub == null)
				{
					anyDead = true;
					continue;
				}
				try
				{
					ub.enabled = enabled;
				}
				catch (Exception)
				{
					anyDead = true;
				}
			}
			if (anyDead)
			{
				unwantedBehaviors.RemoveAll(b => b == null);
				logger.Info("[AutoSeek] rebuilt the reactor list after a character change "
					+ "({0} live)", unwantedBehaviors.Count);
				try
				{
					InitUnwantedBehaviors();
				}
				catch (Exception e)
				{
					logger.Info("[AutoSeek] could not re-capture reactors ({0}) - continuing",
						e.Message);
				}
			}
		}

		// ── CONTINUOUS HANDOFF ───────────────────────────────────────────────────────────────
		// The handoff used to be one-way and one-shot: seek completes, thrust starts, and when
		// the stroke eventually depenetrates nothing re-acquires — the session just stops. So the
		// hotkey behaved like "do one thing" rather than "take over".
		//
		// Armed by the hotkey, this loops: seek -> thrust -> (depenetration) -> seek -> ... until
		// the user presses the hotkey again. It lives entirely in the seeker because the seeker
		// can already observe pene.isPenetrating; AutoThrust needs no knowledge of any of it,
		// which keeps the two from developing opinions about each other's state.
		//
		// Gated on the SAME Autothrust config toggle that gates the existing handoff, so enabling
		// continuous behaviour is not a new setting to discover — it is what that setting always
		// implied.
		private bool loopArmed;

		private float notPenetratingT;

		/// <summary>How long depenetration must persist before re-seeking. A stroke that briefly
		/// pulls clear at the top of its travel is NOT a finished session, and re-acquiring on
		/// every such moment would fight the thrust rather than follow it.</summary>
		private const float ReseekAfterSeconds = 0.9f;

		private void OnUpdate()
		{
			MaleChar c = base.Session.Player.Character;

			// SPACE DISABLES BOTH. AutoThrust owns that hotkey, and stopping the thrust plainly
			// means "stop assisting" — not "stop assisting until I come out, then start hunting
			// again". The loop only watched isPenetrating, so a deliberate stop followed by
			// depenetration read as a completed session and triggered a fresh seek.
			//
			// Checked here rather than in AutoThrust so the dependency stays one-way: the seeker
			// already observes the thruster, and giving the thruster knowledge of the seek loop is
			// how the two start fighting over who owns the session.
			if (autoThruster != null && autoThruster.UserStopped && loopArmed)
			{
				loopArmed = false;
				notPenetratingT = 0f;
				if (state != null && state.ExitReason == ExitReason.None)
				{
					state.ExitReason = ExitReason.Manual;   // cancel a seek already in flight
				}
				overlay.InfoMessage("Assist off [ seek + thrust ]");
				logger.Info("[AutoSeek] user stopped AutoThrust - disarming the seek loop too");
			}

			if (state != null)
			{
				if (state.ExitReason == ExitReason.None)
				{
					if (hotkey.Up && hotkey.Duration < 2f)
					{
						state.ExitReason = ExitReason.Manual;
					}
					else if (c.pene.isPenetrating)
					{
						state.ExitReason = ExitReason.Completed;
					}
					else if (c.pene.IsBlocked())
					{
						state.ExitReason = ExitReason.NoTool;
					}
				}
				if (state.ExitReason != ExitReason.None)
				{
					SetScriptsEnabled(enabled: true);
					if (state.ExitReason == ExitReason.VerticalAngleTooWide)
					{
						// A GEOMETRY failure, not a transient one. Retrying would loop forever at
						// the same impossible angle, so disarm and say why.
						loopArmed = false;
						overlay.InfoMessage("Auto-seek stopped: too wide angle [ loop disarmed ]");
					}
					else if (state.ExitReason == ExitReason.UnreachableTarget)
					{
						loopArmed = false;
						overlay.InfoMessage("Auto-seek stopped: unreachable target [ loop disarmed ]");
					}
					else if (state.ExitReason == ExitReason.Retry)
					{
						// NOT a failure — the pose was wrong, and a fresh approach can fix it.
						// Stays armed; the re-acquire path below starts the next attempt after the
						// usual debounce.
						overlay.InfoMessage("Auto-seek re-approaching");
					}
					else
					{
						if (state.ExitReason == ExitReason.Manual)
						{
							// The hotkey is the ONLY way out of the loop, so it must always
							// disarm — otherwise cancelling a seek would silently restart one.
							loopArmed = false;
							overlay.InfoMessage("Auto-seek stopped [ loop disarmed ]");
						}
						else
						{
							if (state.ExitReason == ExitReason.Completed && autoThruster != null && Autothrust.Value)
							{
								autoThruster.TryStartSequence();
							}
							overlay.InfoMessage("Auto-seek stopped");
						}
					}
					state = null;
					notPenetratingT = 0f;
				}
				else
				{
					Tracer.DrawTransform(state.Hole);
					// SUB-STEP WITH SPEED. Accuracy is distance-per-correction, not
					// corrections-per-second: at 4x a single frame covers four times the ground,
					// so the same per-frame solve is four times coarser and can shoot straight
					// through a 2 cm gate inside one tick. Sub-stepping holds the distance moved
					// between evaluations roughly constant however fast the transit runs.
					//
					// Bounded at 4 — this re-solves GEOMETRY, not physics, and the plant only
					// updates once per frame, so beyond a few sub-steps we are re-reading
					// identical sensor values and paying for the privilege.
					// SUB-STEPPING REMOVED — it was a speed multiplier wearing a stability costume.
					//
					// It ran UpdatePlacement N times per frame (N = ceil(SpeedScale/2), so 2 at the
					// default 4x). The intent was finer integration at high speed. But every command
					// inside UpdatePlacement is already scaled by Time.deltaTime, and nothing divided
					// dt across the sub-steps — so each pass issued a WHOLE frame's motion. N passes
					// meant N times the movement, not the same movement in N pieces.
					//
					// Two visible consequences, both reported: the character moved about twice as
					// fast as the slider claimed (4x rates times 2 sub-steps = 8x), and every log
					// line appeared exactly twice — which reads like two seekers running and sent
					// this investigation after a duplicate-controller bug that did not exist.
					//
					// Real sub-stepping would need dt/N threaded through every command. SpeedScale
					// already multiplies the rates, so one honest step per frame is both correct and
					// what the slider claims to do.
					UpdatePlacement();
				}
			}
			else if (hotkey.Up && hotkey.Duration < 2f && !c.pene.isPenetrating && !c.pene.IsBlocked())
			{
				// STARTING A SEEK IS AN EXPLICIT "ASSIST ON" — clear the stop intent.
				//
				// Without this the "Space disables both" behaviour deadlocks the feature. UserStopped
				// is only ever cleared while PENETRATING (AutoThrust.ReactInput's start branch, or
				// TryStartSequence on handoff). So: stop the thrust with Space, depenetrate, press
				// Space again to seek — loopArmed goes true while UserStopped is still true, and the
				// disarm check below fires on the very next frame. Every frame. The seek cannot start
				// because the thrust was stopped, and the thrust cannot clear the flag because that
				// requires a seek. Observed as an endless
				// "user stopped AutoThrust - disarming the seek loop too".
				if (autoThruster != null)
				{
					autoThruster.ClearUserStop();
				}
				loopArmed = Autothrust.Value;
				StartSeek(loopArmed ? "Auto-seek started [ loop armed ]" : "Auto-seek started");
			}
			else if (loopArmed && Autothrust.Value)
			{
				// RE-ACQUIRE. Once armed, depenetration is treated as "the stroke ended, go find
				// it again" rather than "we are done". Debounced, because a stroke that briefly
				// pulls clear at the top of its travel is not a finished session — re-seeking on
				// every such moment would fight the thrust instead of following it.
				if (c.pene.isPenetrating || c.pene.IsBlocked())
				{
					notPenetratingT = 0f;
				}
				else
				{
					notPenetratingT += Time.deltaTime;
					if (notPenetratingT >= ReseekAfterSeconds)
					{
						StartSeek("Auto-seek re-acquiring");
					}
				}
			}
		}

		private void StartSeek(string message)
		{
			state = new AutoplacerState();
			state.RootTransform = base.Session.Player.RootMotion;
			SetScriptsEnabled(enabled: false);
			overlay.InfoMessage(message);
			state.Hole = GetClosestHole();
			ResetVertical();
			ResetSmoothing();
			// Each attempt re-plans its own ballistic move; the LEARNED gain deliberately
			// survives, so successive attempts start better informed than the first.
			notPenetratingT = 0f;
		}

		private void UpdatePlacement()
		{
			// PRESS-IN LIVES ON THE TARGET, NOT THE SENSOR.
			//
			// Aiming exactly at the entrance plane leaves the tip resting against it rather than
			// seated in it, and the negotiation wants a little intrusion. Per hole because they
			// are not alike: vag and anus admit a shaft against soft tissue that gives, while the
			// boca sits on a moving head and a deep target there just chases her face.
			//
			// This is the same magnitude that was previously (and wrongly) added to the tip
			// MEASUREMENT — now applied where it belongs, so it reads as intent and does not
			// corrupt every derived number.
			// ── TRANSIT vs CALIBRATE ─────────────────────────────────────────────────────────
			//
			// Two failures at a metre out, both from treating every distance the same way.
			//
			// 1. Alignment was throttling the approach at a range where alignment is irrelevant.
			//    20 degrees off matters at the entrance; it does not matter at a metre. The result
			//    was creeping across open floor for no reason.
			//
			// 2. Targeting the hole POSITION from far away arrives pointing AT it but beside the
			//    line — parallel and offset, exactly what was observed. Approaching a point ON the
			//    axis instead makes collinearity a property of the DESTINATION, rather than
			//    something to be corrected once already close, where the geometry is worst and the
			//    corrections fight each other.
			//
			// So: transit to a standoff sitting on the hole's own axis, full speed, no alignment
			// gating; then hand to the fine approach, which starts already on the line.
			// THE HOLE'S OWN OUTWARD DIRECTION, not a bone axis.
			//
			// entrada.forward is the ENTRANCE BONE's forward, which is not the same thing as the
			// direction the hole opens. Measured live on the vag they differ by about 13 degrees
			// ((0,-1.000,+0.009) versus (0,-0.976,-0.219)) — enormous against a 6-12 degree
			// collinearity gate, and enough on its own to explain a shaft that converges to
			// "aligned" by the controller's reckoning while visibly disagreeing with the drawn axis.
			//
			// worldOutHoleDirection is what the game itself uses. Fall back to the bone axis only
			// if it is unavailable.
			// ONE AXIS, AT THE SOURCE. This was HoleOutDirection(), and everything derived from it
			// — the transit standoff, the press-in offset, the slip-off-target reference — pointed
			// ~120 degrees away from the axis the dock cycle, the collinearity readout and the
			// drawn line all use. Four separate bugs this session traced to a copy of this
			// expression that had not been corrected yet; fixing the individual sites just moved
			// the next failure somewhere else. DockAxisOut() is the single source, honours the
			// UseOutHoleAxis experiment, and falls back to the bone axis.
			// REVERTED to HoleOutDirection() for the LEGACY approach paths.
			//
			// Repointing this at the source looked like the right cleanup — one axis everywhere —
			// but it moved the TRANSIT standoff, which is the destination the whole coarse approach
			// aims at, and the result was a measured regression: shaft angle went from ~1 degree at
			// the calibration point to a steady 28-30, with tipMiss growing 0.044 -> 0.105 instead
			// of closing. The legacy approach is built around this direction and arrives correctly
			// with it.
			//
			// So the two axes stay separate, deliberately: the coarse approach keeps the one it was
			// tuned for, and the dock cycle uses DockAxisOut() for every point it drives at and
			// every gate it judges. Which of the two is geometrically "right" is still the open
			// UseOutHoleAxis question — but "make them the same" is not free, and the measurement
			// says this one is load-bearing.
			Vector3 axisOut = HoleOutDirection();
			Vector3 pressIn = axisOut * PressInFor(state.Hole);
			float rangeToHole = (state.Hole.position - PeneClosestPointTo(state.Hole)).magnitude;
			bool transiting = rangeToHole > TransitDist;

			// ══ TIMED DOCK CYCLE ═════════════════════════════════════════════════════════════
			//
			// Near the hole the seeker derived its phase STATELESSLY from instantaneous conditions,
			// so contact making and breaking on adjacent frames flipped it Transit <-> Negotiate
			// indefinitely: it ground against a 31 mm lateral miss at half speed instead of ever
			// backing off and re-solving. A soft throttle is the wrong response to being four times
			// out of tolerance; that is a stop-and-re-solve.
			//
			// Replaced with an explicit, TIMED cycle that commits to one thing at a time:
			//
			//   RETREAT — go to the standoff on the hole's axis, a few cm out. Position only.
			//   ALIGN   — hold there and null angle AND lateral to a TIGHT threshold. No forward
			//             motion at all: aligning while advancing is what produced parallel-but-
			//             offset, because the geometry worsens as you close.
			//   COMMIT  — drive straight in on a DEADLINE sized to the distance. Touching the
			//             correct hole ends it; running out of time resets to RETREAT.
			//
			// The deadline is the point. An advance that has not touched in the time it takes to
			// cover the standoff is, by definition, not going where it was aimed, and the cheapest
			// correct response is to restart from clean geometry rather than keep pushing and
			// deform the chain. Failure is made cheap instead of rare.
			// HYSTERESIS ON ENTRY/EXIT. With one threshold the cycle reset to None and re-entered
			// on consecutive frames as the range jittered across it — five "None -> Retreat" lines
			// in a row, each one throwing away the stage timer and the alignment dwell, so nothing
			// could ever complete. Exit is now well outside entry, and the retreat DELIBERATELY
			// drives outward past the entry range, so the two must not be the same number.
			bool inNearField = state.DockStage == DockStage.None
				? rangeToHole < DockCycleRange
				: rangeToHole < DockCycleExitRange;
			if (!state.FinalStage && inNearField)
			{
				UpdateDockCycle(rangeToHole);
			}
			else if (state.DockStage != DockStage.None)
			{
				// Say which of the two exits fired. "left the near field" printed for both, and it
				// was the FinalStage handover that mattered — the message hid the mechanism fight.
				logger.Info("[AutoSeek] DOCK {0} -> None: {1} (range {2:F3}m)", state.DockStage,
					state.FinalStage ? "handed over to the final-stage press" : "left the near field",
					rangeToHole);
				state.DockStage = DockStage.None;
			}

			// ── RETREAT: a real backoff, not just a state flag ───────────────────────────────
			//
			// Clearing FinalStage re-ran the approach FROM WHEREVER THE SEEKER ALREADY WAS — which
			// after a violation is jammed against her at a bad angle, the worst possible geometry
			// to re-converge from. A violation now drives an explicit retreat to the on-axis
			// standoff, and only then re-approaches.
			//
			// This is precisely what lets the transit boundary stay tight. Instead of reserving
			// margin so that a dock never fails, failure is made CHEAP: retreat 6 cm along the
			// axis, re-converge with clean geometry, try again. A short correct recovery beats a
			// long cautious approach, because the recovery only runs when something went wrong.
			Vector3 standoffPoint = state.Hole.position + axisOut * TransitStandoff;
			if (state.Retreating)
			{
				float back = (standoffPoint - PeneClosestPointTo(state.Hole)).magnitude;
				if (back <= RetreatArrivedTol || state.RetreatT > RetreatMaxSeconds)
				{
					logger.Info("[AutoSeek] retreat complete ({0:F3}m from standoff, {1:F1}s) - "
						+ "re-approaching with clean geometry", back, state.RetreatT);
					state.Retreating = false;
					state.RetreatT = 0f;
					ResetSmoothing();
				}
				else
				{
					state.RetreatT += Time.deltaTime;
				}
			}

			// Retreating and transiting share a destination; only the urgency differs. Getting
			// clear is no place to be gentle — every extra frame jammed deforms the chain further.
			Vector3 worldTarget = (state.Retreating || transiting)
				? standoffPoint
				: state.Hole.position + pressIn;

			// The timed cycle owns the target while it owns the near field. RETREAT and ALIGN hold
			// the SAME point — the standoff on the axis — so alignment happens at a fixed station
			// rather than against a target that is closing in as we correct, which is what made
			// "aligned" and "arrived" fight each other. Only COMMIT aims at the hole.
			if (state.DockStage != DockStage.None)
			{
				// DRIVE TO EXACTLY THE POINT WE ARE JUDGED AGAINST. This used `axisOut`
				// (HoleOutDirection) while CalibrationPoint() uses DockAxisOut(), sending the
				// retreat toward a station ~120 degrees away from the one calErr measures. It could
				// never arrive: every retreat timed out at 1.2 s and handed ALIGN a tip 8 cm from a
				// 3 mm gate. A waypoint and its acceptance test must be the same point, by
				// construction rather than by two expressions that ought to agree.
				// SLEW THE SETPOINT, DO NOT STEP IT.
				//
				// RETREAT/ALIGN station 3 cm OUT; COMMIT presses 5 mm PAST. Switching stage moved
				// the target 3.5 cm in a single frame, and a proportional controller answers a step
				// with a lurch — that is the teleporting feel, and it happens at every stage change,
				// several times per approach while the cycle is still settling.
				//
				// Moving the setpoint at a bounded speed makes the handover continuous in position:
				// the same stages, the same destinations, but the character travels between them
				// instead of being re-aimed instantly. Slew is fast enough not to add meaningful
				// delay (3.5 cm in ~0.2 s) and slow enough that no frame ever sees a step.
				Vector3 desired;
				if (state.DockStage == DockStage.Commit)
				{
					// TRAVEL DOWN THE LINE, DO NOT AIM AT ITS FAR END.
					//
					// Targeting the press point and letting each axis converge on its own produces
					// whatever path the mixture of horizontal and vertical gains happens to give —
					// an arc into the hole, not a straight run. The destination was right and the
					// trajectory was not.
					//
					// Instead the setpoint RIDES THE AXIS: take the tip's current position along
					// the hole's axis, step it inward by one frame of commit speed, and place the
					// target at that depth ON the axis. Two consequences fall out for free — the
					// target carries no perpendicular component, so any lateral drift is corrected
					// continuously rather than tolerated until the end; and the advance is a fixed
					// rate rather than a proportional term that slows as it closes. The tip slides
					// straight in at constant speed.
					Vector3 axis = DockAxisOut();
					Vector3 tipNow = PeneClosestPointTo(state.Hole);
					float axialNow = Vector3.Dot(tipNow - state.Hole.position, axis);
					// Boca's goal depth is OUTWARD (the lip surface), so the stop must be its own
					// goal, not a fixed inward one — clamping to -DockPressPast would drive the
					// tip into the mouth, which is the exact thing BocaTargetOut exists to prevent.
					float axialGoal;
					if (TargetIsBoca())
					{
						// HOLD AT LIP HEIGHT, LET THE TAP DO THE MOTION.
						//
						// The oscillation is no longer expressed in this target at all — it is
						// applied directly to the avatar in DockUnifiedMove, because routing it
						// through the setpoint meant the smoother lagged and attenuated it and the
						// tip only ever ALMOST reached the lips.
						//
						// What remains here is a station: the lip plane, at exactly her lip HEIGHT.
						// Taking the offset horizontally rather than along the tilted axis is what
						// keeps the tip level with her mouth — the 8 degree presentation angle is
						// for the SHAFT, and letting it also raise the target would lift the tip
						// above the lips by the same argument.
						Vector3 outAxis = DockAxisOut();
						Vector3 hOut = new Vector3(outAxis.x, 0f, outAxis.z);
						hOut = (hOut.sqrMagnitude > 1E-06f) ? hOut.normalized : outAxis;
						// RETRY LADDER: tap a while, back off a centimetre, come back.
						//
						// A tap that is not landing does not start landing by being repeated in the
						// same place — whatever it is short of, or catching on, is a property of
						// that position. Withdrawing and re-presenting changes the approach she is
						// responding to and gives the contact a fresh start, which is the same
						// reason the other holes retreat and re-solve rather than pressing harder.
						state.BocaAttemptT += Time.deltaTime;
						bool backingOff = state.BocaAttemptT
							> BocaAttemptSeconds + BocaBackoffSeconds;
						if (backingOff)
						{
							state.BocaAttemptT = 0f;
						}
						float outNow = state.BocaAttemptT > BocaAttemptSeconds
							? BocaBackoffDist : 0f;

						Vector3 station = state.Hole.position
							+ hOut * (AutoSeekTuning.BocaTargetOut + outNow);
						station.y = state.Hole.position.y;
						desired = station + state.DockBias;
					}
					else
					{
						// The other holes advance monotonically: once in, stay in.
						float rideDepth = Mathf.Max(
							axialNow - DockCommitSpeed * Time.deltaTime, -DockPressPast);
						desired = state.Hole.position + axis * rideDepth + state.DockBias;
					}
				}
				else
				{
					desired = CalibrationPoint();
				}
				if (!state.DockTargetPrimed)
				{
					// Prime to the target ALREADY IN USE, not to the new station — otherwise
					// entering the cycle is itself a step, and the first thing the smoothing sees
					// is the lurch it exists to prevent.
					state.DockTargetPrimed = true;
					state.DockTargetSlewed = worldTarget;
				}
				state.DockTargetSlewed = Vector3.MoveTowards(state.DockTargetSlewed, desired,
					DockTargetSlewPerSec * Time.deltaTime);
				worldTarget = state.DockTargetSlewed;
			}
			// Lead the target by where it will be one reaction-time from now.
			worldTarget += HoleVelocity() * HoleVelLead;
			Vector3 targetLocal = state.RootTransform.InverseTransformPoint(worldTarget);

			if (transiting)
			{
				logger.InfoRare(60,
					"[AutoSeek] TRANSIT {0:F2}m out - closing to the on-axis standoff at full "
					+ "speed; alignment gates nothing at this range", rangeToHole);
			}
			Transform transform = ((IPene)base.Session.Player.Character.pene).partePunta;
			// True end of the shaft, not the origin of the tip part — see PeneTipPoint().
			state.TranslateInto = targetLocal - state.RootTransform.InverseTransformPoint(PeneClosestPointTo(state.Hole));
			Tracer.DrawTransform(state.RootTransform);
			if (!state.ResetComplete)
			{
				// THIS GATE IS WHY PLACEMENT LOCKED UP AT RANGE.
				//
				// It returns before every actuator below it, so until it completes the seeker
				// cannot translate at all — it logs "TRANSIT at full speed" and moves nothing,
				// which is exactly the reported symptom: parked a third of a metre out, forever.
				//
				// The gate demanded |z| <= 5 mm. Measured live, currentLocalTarget.z has a mean of
				// 0.001 and a SWING of 60-80 mm: the pelvis depth is driven by the game as well as
				// by us, so it oscillates about zero at roughly six times the tolerance. A settling
				// test on a continuously excited signal never passes, and nothing here noticed the
				// difference between "still settling" and "will never settle".
				//
				// So: widen the tolerance to something the signal can actually satisfy, and bound
				// the wait in time. The reset is a nicety — starting the approach from a neutral
				// depth — and a nicety must never be able to block the entire feature.
				state.ResetT += Time.deltaTime;
				bool resetTimedOut = state.ResetT > ResetMaxSeconds;
				if (Mathf.Abs(pelvisTarget.z) > ResetTolerance && !resetTimedOut)
				{
					// FRAME-RATE BUG. This clamped to +/-0.01 PER TICK, not per second: 1.44 m/s at
					// 144 fps, 0.3 m/s at 30 fps. The reset leg therefore took nearly five times
					// longer on a slow machine, which is a large part of why placement felt
					// inconsistent. Now a rate, scaled by deltaTime like everything else.
					float dv = Mathf.Clamp(0f - pelvisTarget.z,
						-ResetRatePerSec * AutoSeekTuning.SpeedScale * Time.deltaTime,
						ResetRatePerSec * AutoSeekTuning.SpeedScale * Time.deltaTime);
					ctl.AddProfundidadDelta(dv);
					return;
				}
				state.ResetComplete = true;
				if (resetTimedOut)
				{
					logger.Info("[AutoSeek] depth reset gave up after {0:F2}s at z={1:F4} "
						+ "(tol {2:F3}) - proceeding anyway; the pelvis depth is externally driven "
						+ "and does not settle.", state.ResetT, pelvisTarget.z, ResetTolerance);
				}
				if (autoscale)
				{
					base.Session.Player.ResetScale();
				}
			}
			// YAW RUNS ALONGSIDE THE APPROACH, NOT BEFORE IT.
			//
			// This used to be `if (UpdateRotation()) return;` — a hard gate that froze all
			// translation until the heading was within 1 degree. Combined with the per-axis
			// returns below, that is what made placement a sequence of separate manoeuvres:
			// turn, then sidle, then walk in. Rotating while travelling is both faster and how
			// anything actually moves; the only thing the old ordering bought was that the
			// translation target stopped moving under the rotation, and a slow-varying target is
			// exactly what the proportional approach already tolerates.
			//
			// The unsolvable-angle check still aborts, because that is a real exit, not a stage.
			if (UpdateRotation() && state.ExitReason != ExitReason.None)
			{
				return;
			}
			Transform rootmotion = base.Session.Player.RootMotion;
			Vector3 dp = state.TranslateInto;
			// SHAFT DIRECTION IS MEASURED, NOT TAKEN FROM A BONE'S AXIS.
			//
			// This used partePunta.forward — a bone's own forward, which need not lie along the
			// shaft at all, and whose relationship to it changes with pose. The real direction is
			// base -> tip, and the tip is the same point the white marker draws, so the number now
			// agrees with what is on screen.
			//
			// The target is COLLINEARITY WITH THE HOLE'S BLUE AXIS (Tracer draws red=up,
			// green=right, blue=forward). We want the shaft lying along that line pointing INTO
			// her, i.e. anti-parallel to Hole.forward — which for the boca reads as "parallel to
			// the floor" only because that hole's axis happens to be horizontal. Stated as
			// collinearity it generalises: vag's blue points straight down and the same rule then
			// demands a completely different pose, correctly.
			Vector3 shaftDir = PeneClosestPointTo(state.Hole)
				- ((IPene)base.Session.Player.Character.pene).parteBase.position;
			shaftDir = (shaftDir.sqrMagnitude > 1E-08f) ? shaftDir.normalized : transform.forward;

			// SMOOTH THE SHAFT DIRECTION.
			//
			// The shaft is a physics chain: it sags under gravity, springs after every correction,
			// and jitters frame to frame. Steering pitch on the instantaneous reading means chasing
			// that jitter — the controller reacts to a wobble, the wobble reacts to the correction,
			// and the loop settles wherever those two happen to balance rather than at the target.
			// That is consistent with the observed symptom: the shaft resting a little BELOW the
			// hole axis and staying there, since the correction stops as soon as the instantaneous
			// reading crosses zero even though the resting angle has not.
			//
			// A short EMA gives the controller the shaft's SETTLED direction instead of its
			// current one. Slow enough to reject spring and sag, fast enough to track a real
			// re-aim.
			if (!shaftDirPrimed) { shaftDirSmoothed = shaftDir; shaftDirPrimed = true; }
			else
			{
				shaftDirSmoothed = Vector3.Slerp(shaftDirSmoothed, shaftDir,
					Mathf.Clamp01(Time.deltaTime / (TargetIsBoca() ? ShaftDirTauBoca : ShaftDirTau))).normalized;
			}
			shaftDir = shaftDirSmoothed;
			float vangle = UnityUtils.FromToAxisAngle(shaftDir, -state.Hole.forward, rootmotion.right);
			// The `+ worldTipPartLength * 0.1f` fudge that used to sit here is GONE: it was a tenth
			// of the tip offset, on the depth axis only. PeneTipPoint() now carries the whole
			// offset as a vector, so re-adding any part of it here would double-count it.
			if (dp.y + pelvisTarget.y > 0.2f)
			{
				state.ExitReason = ExitReason.UnreachableTarget;
				return;
			}
			// ── SIMULTANEOUS APPROACH ────────────────────────────────────────────────────────
			//
			// This was SERIAL: `if (fixY) { …; return; }`, then pitch, then X, each with its own
			// return, so exactly ONE axis moved per frame in a fixed priority order. Raising the
			// speed only fast-forwards that same one-at-a-time dance — which is why 8x looked
			// identical, just quicker. The axes are independent actuators (pelvis vertical, avatar
			// lateral, pelvis depth, avatar forward); nothing ever required them to take turns.
			//
			// Now every axis is commanded EVERY tick, and forward motion is gated CONTINUOUSLY on
			// how well aligned we are rather than by a hard stage flag. The old `dp.z -= 1f` hack
			// existed to fake exactly that gate — shoving the depth target a metre backwards so
			// the forward branch could not run while misaligned. A smooth 0..1 factor does the
			// same job without the discontinuity, so the approach curves in instead of stepping.
			// Dead zone tightens inside the dock cycle — see DockMovePrecision. A servo cannot hold
			// a tolerance finer than the error at which it stops correcting.
			float movePrec = MovePrecision();
			// UNIFIED NEAR-FIELD MOVE. While the dock cycle owns placement, one smoothed magnitude
			// drives every actuator (see smoothDock) instead of two independent controllers, and
			// the per-axis paths below stand down. Straight line, single arrival.
			bool dockDrive = state.DockStage != DockStage.None && DockUnifiedMove(dp);
			bool fixY = !dockDrive && Mathf.Abs(dp.y) > movePrec;
			bool fixX = !dockDrive && Mathf.Abs(dp.x) > movePrec;

			// 1 when laterally and vertically on target, 0 when badly off. Forward speed scales
			// with it, so we converge diagonally rather than in axis-aligned legs.
			float perpErr = Mathf.Max(Mathf.Abs(dp.x), Mathf.Abs(dp.y));
			float align01 = 1f - Mathf.Clamp01((perpErr - TRANSLATION_PRECISSION) / ApproachGateDist);
			align01 = align01 * align01;   // ease-in: hold back harder while genuinely misaligned

			// COLLINEARITY GATES THE APPROACH, NOT JUST THE DOCK.
			//
			// Aim was only checked at the handover, so the approach would happily close the whole
			// distance while pointing the wrong way and then sit at the threshold waiting for a
			// pitch correction it should have made on the way in. That is why collinearity still
			// felt unprioritised despite the driver existing: it had the entire approach to work
			// during, and instead did its work last, standing still.
			//
			// Folding aim into the same 0..1 factor that throttles forward motion makes closing
			// distance CONDITIONAL on being aimed — so pitch converges during travel, and arriving
			// and being aimed happen together instead of in sequence.
			// COLLINEAR IS NOT PARALLEL.
			//
			// This measured only the DIRECTION term. A shaft pointing perfectly down the hole's
			// axis but offset a few centimetres to the side scores zero degrees, satisfies every
			// gate, then drives forward parallel to the hole and misses it. Same heading, different
			// line — which is exactly a dock that looks correctly aimed and still does not arrive.
			//
			// Collinearity needs BOTH: same direction AND same line. The second term is the
			// perpendicular distance from the tip to the hole's axis, independent of which way we
			// point. They also want different actuators — angle is corrected by pitch and yaw,
			// offset by translation — so conflating them left the lateral error with no owner.
			float aimErr = Vector3.Angle(shaftDir, -state.Hole.forward);
			Vector3 lateralVec = LateralOffsetFromAxis(state.Hole);
			float lateralMiss = lateralVec.magnitude;

			// DRIVE IT, DO NOT JUST REFUSE.
			//
			// The previous version throttled forward motion on lateral error and left it at that.
			// Nothing was assigned to REMOVE the error, so the seeker would settle parallel to the
			// axis, some distance off the line, with forward motion clamped to zero — and simply
			// stop. A gate with no corrective path is a deadlock, not a safety feature.
			//
			// We have both lines: the shaft (yellow) and the hole axis (cyan). The offset between
			// them is a vector, so feed it to the actuator that can cancel it — translation. Added
			// to the horizontal move below, so closing the lateral error and closing the distance
			// happen in the same motion instead of one blocking the other.
			//
			// PROGRESSIVE TOLERANCE. A fixed tolerance is wrong at both ends: far out it demands
			// precision that does not matter yet and stalls the approach, and up close it permits
			// slop that guarantees a miss. So the requirement tightens with proximity — loose at a
			// pene length, tight at the entrance — which also gives a continuous path IN rather
			// than a wall to bounce off.
			float axialDist = Mathf.Abs(Vector3.Dot(
				PeneClosestPointTo(state.Hole) - state.Hole.position,
				(-state.Hole.forward).normalized));
			float lineTol = ProgressiveLineTol(axialDist);

			// ── CONTACT FIRST, THEN ALIGN ABOUT THE CONTACT POINT ────────────────────────────
			//
			// The gating was backwards. Requiring collinearity BEFORE allowing approach means a
			// misaligned seeker can never reach the contact that would let it align: it throttles
			// its own forward motion toward zero and sits there. That is the stall on the vag and
			// anus, and no tolerance value fixes it, because the ordering is the bug.
			//
			// Touching is the easy half and barely needs alignment. Once the tip IS touching it is
			// effectively PINNED — the body can pitch and yaw about that point without losing
			// contact, which is exactly how this would be done by hand. So the phases invert:
			//
			//   not touching -> approach is the priority. Only gross misalignment slows it, and
			//                   never to a stop; arriving is what unlocks the alignment phase.
			//   touching     -> stop advancing. Correct the angle about the contact point instead.
			//                   Pressing on while off-axis only displaces the hole.
			//
			// The angular controllers run in BOTH phases. What changes is whether closing distance
			// is permitted while they work.
			// ── EXPLICIT PHASE MACHINE ───────────────────────────────────────────────────────
			//
			// The behaviour used to EMERGE from four interacting booleans — transiting, inContact,
			// FinalStage, Retreating — each set in one place and read in several others. That is
			// why it was unpredictable: the combinations were never enumerated, so states existed
			// that nobody designed (advancing while retreating, pinned while transiting, dwelling
			// with nothing touching). Naming the phases makes the illegal combinations
			// unrepresentable rather than merely unlikely.
			//
			//   TRANSIT    > 2 cm      fast, no gating, target = a point ON the axis
			//   HOLD       <= 2 cm     STOP. Correct collinearity in place; nothing moves forward
			//   TOUCH      aligned     creep forward until the CORRECT hole reports contact
			//   NEGOTIATE  contact     hold, and let the game's acceptance run
			//
			// A violation returns to HOLD, never to TRANSIT: position was fine and only the angle
			// failed, so re-running the whole approach threw away work that was already correct.
			// ── DIVERGENCE DETECTOR ──────────────────────────────────────────────────────────
			//
			// Everything else here reacts to ABSOLUTE thresholds: angle over N degrees, lateral
			// over N millimetres, contact lost. Nothing reacted to the error simply GETTING WORSE.
			// So the tip could drift steadily away from the target for as long as it stayed inside
			// tolerance, and only trip a limit after the situation was already bad — by which point
			// the shaft is usually bowed and the recovery is expensive.
			//
			// Progress should be monotonic while closing. Sustained regression means the current
			// approach is not working, and that is knowable long before any threshold is crossed:
			// the sign of the rate carries the information, not the magnitude of the error.
			//
			// This is the white-T distance specifically — the same point the marker draws and the
			// same one every other term is derived from, so a penalty here cannot disagree with
			// what is on screen.
			float tipToTarget = (PeneClosestPointTo(state.Hole) - worldTarget).magnitude;
			if (!state.TipDistPrimed)
			{
				state.TipDistPrimed = true;
				state.TipDistSlow = tipToTarget;
			}
			// Rate of change, smoothed — a raw derivative on a physics chain is mostly noise.
			float tipRate = (tipToTarget - state.TipDistSlow) / Mathf.Max(1E-04f, Time.deltaTime);
			state.TipDistSlow = Mathf.Lerp(state.TipDistSlow, tipToTarget,
				Mathf.Clamp01(Time.deltaTime / TipDistTau));
			state.TipRateSlow = Mathf.Lerp(state.TipRateSlow, tipRate,
				Mathf.Clamp01(Time.deltaTime / TipRateTau));

			bool diverging = state.TipRateSlow > TipDivergeRate;
			if (diverging) state.DivergeT += Time.deltaTime;
			else state.DivergeT = Mathf.Max(0f, state.DivergeT - Time.deltaTime * 2f);

			int contactCount = HoleContactCount();
			bool inContact = contactCount > 0;
			float enterDeg = EnterDegFor();
			float aim01 = 1f - Mathf.Clamp01(
				(aimErr - enterDeg)
				/ Mathf.Max(4f, AutoSeekTuning.CollinearAbortDeg - enterDeg));
			float line01 = 1f - Mathf.Clamp01(
				(lateralMiss - lineTol) / Mathf.Max(0.01f, CollinearLineAbort - lineTol));

			// Phase selection, evaluated once so every branch below agrees on where we are.
			float holdErr = Mathf.Max(aimErr / Mathf.Max(1f, enterDeg),
				lateralMiss / Mathf.Max(0.002f, lineTol));
			bool alignedEnough = holdErr <= 1f && BendNow() <= SeekDirTrustBend;
			// SUSTAINED divergence forces HOLD regardless of how good the absolute numbers look.
			// Brief growth is normal — she moves, the chain springs — so it has to persist before
			// it counts. Stopping and re-correcting is nearly free; continuing to close on a
			// target we are actively losing is what produces the jams.
			bool divergedTooLong = state.DivergeT > TipDivergeSeconds;

			SeekPhase phase = inContact ? SeekPhase.Negotiate
				: transiting ? SeekPhase.Transit
				: (alignedEnough && !divergedTooLong) ? SeekPhase.Touch
				: SeekPhase.Hold;

			if (divergedTooLong && phase == SeekPhase.Hold)
			{
				logger.InfoRare(45,
					"[AutoSeek] DIVERGING - tip moving away from target at {0:F3} m/s for {1:F1}s "
					+ "(dist {2:F4}m) - holding to re-correct rather than chasing",
					state.TipRateSlow, state.DivergeT, tipToTarget);
			}
			if (phase != state.Phase)
			{
				logger.Info("[AutoSeek] {0} -> {1}  range={2:F3}m angle={3:F1}deg lateral={4:F4}m "
					+ "contacts={5}", state.Phase, phase, rangeToHole, aimErr, lateralMiss, contactCount);
				state.Phase = phase;
				// DO NOT DUMP VELOCITY WHILE THE DOCK CYCLE IS DRIVING.
				//
				// SeekPhase is derived statelessly from instantaneous conditions, so near the hole
				// it flaps — Transit -> Touch -> Hold -> Touch -> Transit within a few frames, on
				// contact and tolerance noise. Each of those transitions reset the motion smoothers
				// to zero, so the approach lost its velocity several times a second: the visible
				// "jittery indecisiveness", produced by a bookkeeping side effect rather than by
				// any control decision.
				//
				// The cycle has its own stage transitions and resets smoothing at each of them,
				// which is where that belongs. While it owns the near field, the old phase label is
				// telemetry and must not touch the actuators.
				if (state.DockStage == DockStage.None)
				{
					ResetSmoothing();   // a previous phase's velocity does not belong to this one
				}
			}

			if (!inContact)
			{
				// TRANSIT: no gating whatsoever. The destination is already on the axis, so
				// arriving IS becoming collinear — there is nothing for alignment to protect
				// against, and throttling here is what produced a creep across open floor.
				//
				// ACQUIRE (close in): a shaft 20 degrees off still reaches the entrance; one that
				// never arrives cannot be corrected by anything. Misalignment may slow the
				// approach, never stop it.
				switch (phase)
				{
					case SeekPhase.Transit:
						// Destination is already on the axis, so arriving IS becoming collinear.
						align01 = 1f;
						break;
					case SeekPhase.Hold:
						// STOP and self-correct. Advancing while misaligned is what jammed the
						// shaft against her at an angle it could not recover from.
						align01 = 0f;
						logger.InfoRare(90,
							"[AutoSeek] HOLD at {0:F3}m - stopped, correcting (angle={1:F1}deg "
							+ "lateral={2:F4}m, need <={3:F1}deg / {4:F4}m)",
							rangeToHole, aimErr, lateralMiss, enterDeg, lineTol);
						break;
					default:
						// TOUCH: aligned, so close the last centimetres deliberately.
						align01 *= TouchApproachGain;
						break;
				}

				// CREEP UNTIL CONTACT IS PROVEN — this is the remaining standoff.
				//
				// The approach drives dp to zero and then stops, because reaching the computed
				// target IS its whole job. But ARRIVING IS NOT TOUCHING. If the target is even
				// slightly short — a tip estimate a few millimetres out, an entrance transform
				// sitting just inside the surface, her having shifted since — the seeker parks
				// somewhere it believes is correct, considers itself successful, and waits
				// indefinitely for a contact that can never occur. No amount of tolerance tuning
				// helps, because by its own reckoning it has already succeeded.
				//
				// The target is a hypothesis; contact is the evidence. So while nothing is
				// touching, keep closing regardless of what the arithmetic claims. Bounded, so a
				// genuinely unreachable hole still fails rather than grinding forward forever.
				if (dp.magnitude < ContactCreepStart && state.CreepAccum < ContactCreepMax)
				{
					float creep = ContactCreepRate * Mathf.Max(0.25f, AutoSeekTuning.SpeedScale)
						* Time.deltaTime;
					state.CreepAccum += creep;
					// Along the HOLE's axis, into her — not the body's forward, which may point
					// somewhere else entirely.
					base.Session.Player.Move(state.RootTransform.InverseTransformDirection(
						(-state.Hole.forward).normalized) * creep);
					logger.InfoRare(90,
						"[AutoSeek] arrived but NOT touching (dp={0:F4}m) - creeping {1:F3}/{2:F3}m "
						+ "along the hole axis until contact proves it",
						dp.magnitude, state.CreepAccum, ContactCreepMax);
				}
			}
			else
			{
				// PINNED — RELATIVE TO THE HOLE, NOT TO THE WORLD.
				//
				// Zeroing forward motion held station in WORLD space, which loses contact the
				// instant she moves: turn the head and the lips travel away from a tip that is
				// dutifully holding still. Contact has to be maintained against a moving target,
				// so "pinned" must mean pinned to HER.
				//
				// A small forward authority does that. dp is recomputed every tick against the
				// live hole transform, so retaining some gain means the body tracks the entrance
				// as it moves — following it around the arc of a turning head — while the lateral
				// term keeps us on the axis and pitch/yaw keep us collinear through the sweep. Low
				// enough that it follows rather than presses, which is what displaced the hole
				// before.
				align01 = ContactFollowGain;
				state.CreepAccum = 0f;
				logger.InfoRare(90,
					"[AutoSeek] contact made - tracking the entrance and aligning about the contact "
					+ "(angle={0:F1}deg lateral={1:F4}m)", aimErr, lateralMiss);
			}

			if (AutoSeekTuning.Verbose || lateralMiss > lineTol)
			{
				logger.InfoRare(60,
					"[AutoSeek] collinearity: angle={0:F1}deg lateral={1:F4}m (tol {2:F4} at "
					+ "{3:F3}m out) -> aim01={4:F2} line01={5:F2}",
					aimErr, lateralMiss, lineTol, axialDist, aim01, line01);
			}

			if (AutoSeekTuning.Verbose || lateralMiss > CollinearLineTol)
			{
				logger.InfoRare(60,
					"[AutoSeek] collinearity: angle={0:F1}deg lateral={1:F4}m (tol {2:F3}) "
					+ "-> aim01={3:F2} line01={4:F2}", aimErr, lateralMiss, CollinearLineTol,
					aim01, line01);
			}

			if (!state.FinalStage)
			{
				// VERTICAL — pelvis. Unchanged in intent, minus the early return.
				if (fixY)
				{
					if (dp.y < 0f || pelvisTarget.y < 0f)
					{
						// FEED-FORWARD, THEN TRIM.
						//
						// Chasing a stale error is what caused the squatting: the body lags, so the
						// error we react to describes a pose we already commanded away from.
						// Reacting more carefully (rate limiting) makes that stable but slow.
						//
						// Better: compute the WHOLE move once, commit it ballistically over a
						// fixed window, let the plant settle, and only then close the loop on
						// whatever is left. During the ballistic phase there is no feedback at all,
						// so lag cannot destabilise anything — there is no loop to destabilise.
						//
						// It needs the command-to-world gain, which is not a constant we can look
						// up: it depends on scale, pose, and the aperture coupling that tilts the
						// shaft as the pelvis rises. So it is MEASURED from the ballistic move
						// itself — commanded versus achieved — and refined on every attempt. The
						// first move may be 20 % off; the next is close; after that it is right.
						// Returns true while the ballistic move owns the axis; the rate-limited trim
						// below then runs only once it has finished and settled.
						// TRIM — rate-limited, and only after the ballistic move has settled.
						//
						// NOT a fraction of the error.
						//
						// This was `dp.y * Min(1f, dt * 5 * SpeedScale)` — a proportional command
						// with NO upper bound on how much of the error it could issue in one
						// frame, and the Min(1f, ...) explicitly allowed one hundred per cent of
						// it. At 8x that works out to ~0.67 of the remaining error per frame at
						// 60 fps.
						//
						// The pelvis is a laggy plant: AddVerticalDelta commands a target the IK
						// then takes several frames to reach, so dp.y still reports the OLD
						// position while we keep adding more. Commands accumulate faster than the
						// body responds, it sails past, the error flips sign, and it does the
						// same thing in reverse — which is the squatting.
						//
						// TranslateTowards is proportional AND rate-capped, so a frame can only
						// ever issue a bounded step. Lag then costs convergence time instead of
						// causing oscillation, which is the trade you want: too slow is visible
						// and harmless, unstable is neither.
						float vBefore = pelvisTarget.y;
						float vStep = VerticalStep(dp.y);
						ctl.AddVerticalDelta(vStep);
						logger.InfoRare(30, "[AutoSeek/vert] errY={0:F4} pending={1:F4} "
							+ "step={2:F5} pelvisY={3:F4} lastAchieved={4:F5}",
							dp.y, vertPending, vStep, vBefore, vBefore - vertLastPelvisY);
						vertLastPelvisY = vBefore;
					}
					else if (autoscale)
					{
						Vector3 scale = Vector3.up * 0.1f * Time.deltaTime;
						base.Session.Player.AddScale(scale);
					}
					else
					{
						state.ExitReason = ExitReason.UnreachableTarget;
						return;
					}
				}

				// PITCH — ACTIVELY DRIVE COLLINEARITY, via pelvis Z.
				//
				// The gate below waits for the shaft to be collinear with the hole axis; nothing
				// was actually driving it there. This is that driver, and pelvis Z is the right
				// actuator because FREECAL measured it as the pitch lever — 69 deg per unit
				// averaged, 37 in the lower half and 100 in the upper. Reading that map from
				// AutoThrust rather than keeping a second copy matters: two features disagreeing
				// about the character's own kinematics is how they end up fighting each other.
				//
				// The old expression had three problems. It was gated on
				// `(vangle < 0f || pelvisTarget.y < 0f)`, so in some poses it would only correct
				// one SIGN of error and simply tolerate the other. Its step was |vangle|/100, a
				// scale factor with no physical meaning that happens to be near the real 1/100
				// deg-per-unit only in the upper half of the range. And it converged at a rate
				// unrelated to how wrong it actually was.
				//
				// Now: required Z = -pitchError / degPerUnit, rate-limited. The sign comes from the
				// measurement, so there is no learner and no direction to guess.
				// THE BENDY-PENE TRAP.
				//
				// shaftDir is tip-minus-base: a CHORD ACROSS A CURVE. When the shaft bows, that
				// chord swings even though the base has not moved and nothing about the aim has
				// changed. Feeding it to a pitch controller closes a loop through the deformation
				// itself — bend swings the chord, the chord commands pitch, the pitch presses, the
				// press bends it further. The controller would chase its own bending and read the
				// result as progress.
				//
				// So while the shaft is bowed the direction measurement is simply NOT TRUSTWORTHY,
				// and the correct response is to stop steering on it rather than to steer more
				// carefully. The press already backs off on bend; this makes the aiming term agree
				// with that instead of fighting it.
				float shaftBend = BendNow();
				bool dirTrustworthy = shaftBend <= SeekDirTrustBend;
				if (!dirTrustworthy)
				{
					logger.InfoRare(60,
						"[AutoSeek] shaft bowed {0:P0} - direction reading is a chord across a "
						+ "curve, not an aim; suspending pitch correction until it straightens",
						shaftBend);
				}

				float pitchErr = vangle;
				// THE GATE THAT WAS SILENTLY OFF.
				//
				// This required |pelvisTarget.z| < MaxDepth, which resolves to autoThruster.MaxDepth
				// / 2 = 0.1 — against a pelvis z range of roughly -0.5 .. +0.48. So the pitch
				// driver only ran inside the middle fifth of its own travel and was closed
				// everywhere else, which is why collinearity was never actually driven no matter
				// how the gains were tuned. It also inherited MaxDepth = 0.2f, the hardcoded
				// constant that should have been the live zRange all along.
				//
				// The guard's real intent was surely "do not push z where it cannot go". That is a
				// HEADROOM question, so ask it directly: is there room left in the direction we
				// need? A range limit stops the correction; being far from centre does not.
				float zNeed = 0f - pitchErr;   // sign only; magnitude computed below
				ZRoom(out float zRoomPlus, out float zRoomMinus);
				bool haveRoom = (zNeed >= 0f) ? zRoomPlus > 0.002f : zRoomMinus > 0.002f;

				if (!dirTrustworthy || !haveRoom)
				{
					logger.InfoRare(90,
						"[AutoSeek] pitch drive idle: {0}{1}(err={2:F1}deg room+={3:F3} room-={4:F3})",
						dirTrustworthy ? "" : "shaft bowed; ",
						haveRoom ? "" : "no z headroom; ", pitchErr, zRoomPlus, zRoomMinus);
				}

				// BOCA: SET THE PITCH ONCE, THEN LEAVE THE HIPS ALONE.
				//
				// Pitch is solved through the pelvis, and for the boca that closes a feedback loop
				// with the target itself: her head turns in response to the hips, so every pitch
				// correction moves the mouth, which changes the pitch error, which triggers another
				// correction. The visible result is the hips climbing endlessly — there is no fixed
				// angle to settle on, because the angle is what keeps moving the goal.
				//
				// So the hips get ONE say. Once the presentation angle is close enough, latch it and
				// let translation, yaw and the lip tap do the rest: those move the tip without
				// re-posing the hips, so they do not re-excite her head.
				//
				// Released only on a large excursion — she has genuinely turned away — with the gap
				// between lock and release acting as hysteresis so it cannot chatter.
				if (TargetIsBoca())
				{
					float absPitch = Mathf.Abs(pitchErr);
					if (state.BocaPitchLocked && absPitch > BocaPitchReleaseDeg)
					{
						state.BocaPitchLocked = false;
						logger.Info("[AutoSeek] boca: pitch drifted to {0:F1}deg - unlocking the "
							+ "hips to re-present", pitchErr);
					}
					else if (!state.BocaPitchLocked && absPitch <= BocaPitchLockDeg)
					{
						state.BocaPitchLocked = true;
						logger.Info("[AutoSeek] boca: presentation angle reached ({0:F1}deg) - "
							+ "locking hip pitch; translation and yaw take it from here", pitchErr);
					}
				}
				else if (state.BocaPitchLocked)
				{
					state.BocaPitchLocked = false;
				}

				if (dirTrustworthy && haveRoom && Mathf.Abs(pitchErr) > 1f
					&& !state.BocaPitchLocked)
				{
					float degPerUnit = 69f;
					try
					{
						if (autoThruster != null) degPerUnit = autoThruster.PitchDegPerUnitZ;
					}
					catch
					{
					}
					degPerUnit = Mathf.Clamp(Mathf.Abs(degPerUnit), 20f, 200f);

					// SAME LAG PROBLEM AS VERTICAL, SAME FIX.
					//
					// A plain rate-limited proportional command oscillates here for exactly the
					// reason it did on the vertical axis: the pelvis takes several frames to reach
					// a commanded target, so pitchErr still reports the OLD angle while we keep
					// adding more command on top. It sails past, the error flips, and it does it
					// again in reverse. This axis simply never got to demonstrate that, because
					// the MaxDepth gate meant it was almost never running.
					//
					// So: subtract commanded-but-unobserved (pending), and drive through the same
					// bounded-acceleration smoother as the translation axes so the motion is
					// continuous rather than a per-frame step.
					float wantZ = (0f - pitchErr) / degPerUnit;

					if (!pitchPrimed) { pitchLastErr = pitchErr; pitchPrimed = true; }
					// Progress shows up as the ANGLE closing; convert it into the z units the debt
					// is denominated in, using the same map that produced the command.
					float progressDeg = pitchLastErr - pitchErr;
					pitchLastErr = pitchErr;
					pitchPending -= progressDeg / degPerUnit;
					pitchPending = Mathf.Clamp(pitchPending, -PitchPendingMax, PitchPendingMax);

					float effectiveZ = wantZ - pitchPending;

					// Rate scales with the SQUARE ROOT of SpeedScale, not linearly. At 4x a linear
					// scaling gives 1.4 units/s against a range of about 1.0 — the whole travel in
					// under a second, into an actuator that lags. Speed should raise the ceiling,
					// not guarantee overshoot.
					float rate = PitchRatePerSec * Mathf.Sqrt(Mathf.Max(0.05f, AutoSeekTuning.SpeedScale))
						* LeverScale() * (TargetIsBoca() ? BocaGainDamping : 1f);
					float step = smoothPitch.Step(effectiveZ, Time.deltaTime,
						AutoSeekTuning.ApproachTau, rate, rate * 3f);
					pitchPending += step;
					ctl.AddProfundidadDelta(step);
					ballisticClean = false;

					logger.InfoRare(60,
						"[AutoSeek] pitch {0:F1}deg -> wantZ {1:F4} step {2:F5} "
						+ "({3:F0} deg/unit, {4})", pitchErr, wantZ, step, degPerUnit,
						(autoThruster != null && autoThruster.PitchMapCalibrated && !autoThruster.PitchMapStale)
							? "FREECAL-measured"
							: ((autoThruster != null && autoThruster.PitchMapStale)
								? "default (map STALE - character resized since calibration)"
								: "default (not calibrated)"));
				}

				// HORIZONTAL — ONE VECTOR, NOT TWO AXES.
				//
				// Even with the early returns gone, Move(x,0,0) followed by Move(0,0,z) still
				// traces an axis-aligned path: the character sidles across, then walks in. Moving
				// along the NORMALISED error direction is a straight diagonal to the destination —
				// shorter path, less time, and it reads as walking to a spot rather than pacing
				// out a rectangle.
				//
				// The forward component stays throttled by align01, so an approach that begins
				// badly misaligned CURVES in rather than driving straight at her.
				// The lateral error is expressed in WORLD space; the move is in root-local. Convert
				// and add it, so translation actively cancels the offset from the axis line rather
				// than merely being forbidden to advance while it exists.
				Vector3 lateralLocal = state.RootTransform.InverseTransformDirection(lateralVec);
				Vector3 flat = new Vector3(
					dp.x - lateralLocal.x * (inContact ? LateralTrackGain : LateralCorrectGain),
					0f,
					dp.z * align01 - lateralLocal.z * (inContact ? LateralTrackGain : LateralCorrectGain));
				float flatMag = flat.magnitude;
				// OUTSIDE the precision gate on purpose. Inside it, the one case that needs
				// explaining — "we are in TRANSIT and commanding nothing" — is the exact case that
				// produces no output at all.
				if (transiting)
				{
					// dp AND calErr ON ONE LINE, SAME FRAME.
					//
					// They are the same quantity — error from the measured tip to the point we are
					// driving at — so they must agree. Across separately-throttled log lines they
					// appeared to differ 15-fold (|dp| 0.002 vs calErr 0.031), but comparing
					// readings from different frames is exactly the mistake that has cost this work
					// before. One line, one frame, no inference.
					logger.InfoRare(30, "[AutoSeek/transit] dp=({0:F3},{1:F3},{2:F3}) |dp|={3:F4} "
						+ "calErr={4:F4} stage={5} lat=({6:F3},{7:F3}) flatMag={8:F4} align01={9:F2} "
						+ "range={10:F3} phase={11} final={12} pelvisY={13:F3}",
						dp.x, dp.y, dp.z, dp.magnitude,
						(CalibrationPoint() - PeneClosestPointTo(state.Hole)).magnitude,
						state.DockStage, lateralLocal.x, lateralLocal.z, flatMag, align01,
						rangeToHole, state.Phase, state.FinalStage, pelvisTarget.y);
				}
				if (flatMag > MovePrecision() && !dockDrive)
				{
					// Transit gets a much higher speed ceiling and a snappier time constant: it is
					// covering open ground, not placing anything. A metre should take about a
					// second, not thirty.
					float moveCap = transiting ? TransitSpeed * Mathf.Max(0.25f, AutoSeekTuning.SpeedScale)
											 : SmoothMaxSpeed;
					// Tau shortens as speed rises: a higher ceiling with an unchanged time constant just
					// means the cap is never reached, so "faster" does nothing until the distance is long.
					float moveTau = transiting
						? TransitTau / Mathf.Max(0.25f, AutoSeekTuning.SpeedScale)
						: AutoSeekTuning.ApproachTau;
					float moveStep = smoothFlat.Step(
						flatMag, Time.deltaTime, moveTau, moveCap, moveCap * 4f);
					// COMMANDED vs ACHIEVED. "TRANSIT at full speed" while the range does not fall
					// has exactly two explanations — we are commanding ~0, or we are commanding
					// properly and something is undoing it — and they need opposite fixes. Logging
					// the command next to the resulting displacement separates them in one run
					// instead of one round-trip each.
					Vector3 posBefore = base.Session.Player.GameObject.transform.position;
					base.Session.Player.Move(flat / flatMag * moveStep);
					if (transiting)
					{
						logger.InfoRare(30, "[AutoSeek/transit] dp=({0:F3},{1:F3},{2:F3}) "
							+ "lat=({3:F3},{4:F3}) flatMag={5:F4} align01={6:F2} step={7:F4} "
							+ "cap={8:F3} achieved={9:F4}",
							dp.x, dp.y, dp.z, lateralLocal.x, lateralLocal.z, flatMag, align01,
							moveStep, moveCap,
							(base.Session.Player.GameObject.transform.position - posBefore).magnitude);
					}
					// Any other actuator moving during the ballistic window invalidates its
					// commanded-vs-achieved measurement.
					ballisticClean = false;
				}

				if (AutoSeekTuning.Verbose)
				{
					logger.Info("[AutoSeek] dp=({0:F3},{1:F3},{2:F3}) vangle={3:F1} align01={4:F2}",
						dp.x, dp.y, dp.z, vangle, align01);
				}

				// THE TARGET MUST BE IN FRONT, NOT MERELY NEAR.
				//
				// This read `dp.z < ApproachGateDist`, which ANY negative dp.z satisfies — so a
				// target BEHIND the player counted as "close enough, start pressing", and the press
				// then drove forward, away from it. Measured live during a boca seek: root-local dp
				// was (x +0.107, y -0.069, z -0.391). The mouth was 39 cm behind him and the seeker
				// committed to the press anyway, which is why every attempt logged the full 0.18 m
				// without contact, retried, and hunted vertically forever — the geometry could
				// never resolve because it was walking the wrong way.
				//
				// Requiring a POSITIVE dp.z makes the condition "in front of me AND close", which
				// is what this stage always meant.
				if (dp.z < -ApproachGateDist)
				{
					logger.InfoRare(60,
						"[AutoSeek] target is BEHIND by {0:F3}m (root-local) - repositioning, "
						+ "not pressing", -dp.z);
				}
				// COLLINEARITY IS A PRECONDITION OF DOCKING, NOT A HOPE.
				//
				// Angle between the shaft and the hole's own axis, in full 3D — not the single
				// plane `vangle` measures. Position said "arrived"; this says "aimed". Without it
				// the seeker could reach the right POINT while pointing somewhere else, commit to
				// the press, and bow the shaft against the entrance instead of entering it.
				float collinearDeg = Vector3.Angle(shaftDir, -state.Hole.forward);

				// Same trap as the pitch driver: a bowed shaft makes this angle describe the BEND
				// rather than the aim, and a small reading on a bent shaft is meaningless — the
				// chord can point straight at the hole while the shaft itself is folded. Docking on
				// that would be committing to a measurement we know is lying, so a bent shaft can
				// never satisfy the gate however good the number looks.
				// Both terms, or it is not collinear. Angle alone let a parallel-but-offset shaft
				// through the gate and into a dock it could never complete.
				float gateLateral = LateralMissFromAxis(state.Hole);
				bool aimed = dirTrustworthy
					&& collinearDeg <= EnterDegFor()
					&& gateLateral <= (TargetIsBoca() ? CollinearLineTol * BocaAngleTightening : CollinearLineTol);

				// ONE DOCKING AUTHORITY AT A TIME.
				//
				// This is the ORIGINAL dock gate, and it is looser than the timed cycle's: it needs
				// no calibration point, no sustained alignment, and no commit deadline. While the
				// cycle owns the near field, this firing on its own terms takes the decision away
				// from it mid-ALIGN — observed as the cycle passing its gate (calErr 0.0022) and
				// then immediately reporting "left the near field at 0.026m", which was FinalStage
				// going true underneath it. The cycle reset, FinalStage later cleared, the range was
				// still inside the entry band so it re-entered Retreat, and the two mechanisms
				// handed the pelvis back and forth — the visible jitter.
				//
				// So this gate stands down whenever the cycle is running. The cycle hands over the
				// same way, by dropping to DockStage.None once it has real contact, after which
				// this path takes it from there.
				// ...AND ONLY OUTSIDE THE CYCLE'S TERRITORY. `DockStage == None` alone is not
				// enough: it is also None on the approach BEFORE the cycle engages, so the legacy
				// gate could still claim the dock a few centimetres out, set FinalStage, and lock
				// the cycle out entirely (it only runs while !FinalStage). Its own slip detector
				// then policed the dock and aborted repeatedly. Requiring the range as well means
				// the near field belongs to the cycle unconditionally.
				if (state.DockStage == DockStage.None && rangeToHole >= DockCycleRange
					&& !fixX && !fixY && dp.z >= 0f && dp.z < ApproachGateDist && aimed)
				{
					logger.Info("[AutoSeek] docking: collinear {0:F1}deg (gate {1:F0}), "
						+ "dp=({2:F3},{3:F3},{4:F3})", collinearDeg,
						AutoSeekTuning.CollinearEnterDeg, dp.x, dp.y, dp.z);
					state.FinalStage = true;
				}
				else if (!fixX && !fixY && dp.z >= 0f && dp.z < ApproachGateDist)
				{
					// In position but NOT aimed. Holding here is correct: the approach's pitch and
					// yaw terms are still running and will close the angle. Docking now would be
					// the failure this gate exists to prevent.
					logger.InfoRare(45,
						"[AutoSeek] in position but off-axis by {0:F1}deg (gate {1:F0}) - waiting "
						+ "for alignment before docking", collinearDeg, AutoSeekTuning.CollinearEnterDeg);
					// ...AND ACTUALLY WAIT.
					//
					// This branch said "waiting" and then fell straight through into the final-stage
					// press below, where the abort checks live. So an approach that arrived in
					// position but off-axis — which is the NORMAL case when starting from a bad
					// angle — immediately tripped `lostLine`, backed off, and spent a dock attempt.
					// Five of those restart the seek, so it gave up before the pitch and yaw terms
					// had a chance to close the angle at all: it could only ever dock from a start
					// that was already nearly parallel.
					//
					// Returning here is what the comment above always claimed happened. The
					// approach keeps running, alignment converges, and the aimed branch takes over.
					return;
				}
				else
				{
					return;
				}
			}
			// FINAL STAGE — PRESS TO THE TARGET, DO NOT EASE OFF AT IT.
			//
			// This used to be TranslateTowards(dp.z, 0.05f): a damped approach that moved at a
			// TENTH speed over the last 5 cm — exactly the stretch where the mesh first meets
			// adjacent skin. The shaft is a physics chain, so on contact it deflects and
			// compresses rather than advancing, and the tip's world position stalls while the
			// seeker obligingly slows to a crawl. The result is a seek that stops just short,
			// every time, with nothing obviously wrong in the numbers.
			//
			// So: no damping here, and the goal is PAST the entrance rather than at it. The
			// debug lines already mark where the tip has to be; drive the mesh there and let the
			// soft body absorb the difference, instead of negotiating with the contact.
			if (Mathf.Abs(dp.z) > 0f)
			{
				// BEND MEANS STOP PRESSING. Pressing a flexible shaft into a surface it is not
				// aligned with does not insert it — it BOWS it, the tip rides upward off the
				// target, and every additional millimetre of press makes the angle worse. That is
				// the observed failure: the white marker climbs above the target and the shaft
				// ends up pointing up rather than forward.
				//
				// Bend is measurable directly from the game's own straight-vs-current length pair,
				// so rather than pressing harder we back off, which lets the shaft straighten, and
				// then re-approach from a pose that is not the one that jammed.
				// LOST THE LINE — ABANDON THE DOCK.
				//
				// Checked every tick DURING the press, not only on entry: she moves, he settles,
				// and an approach that was aimed a moment ago can drift off-axis mid-dock. Pressing
				// on from there is the same failure as never having been aimed, just later. The
				// abort threshold is deliberately wider than the entry gate so a dock that wobbles
				// slightly still completes — the gap between the two is the hysteresis, and a
				// single threshold would chatter between docking and retreating.
				// COLLINEARITY VIOLATION -> INSTANT BACKOFF TO STANDOFF, THEN RE-NEGOTIATE.
				//
				// Previously a violation abandoned the whole attempt and re-ran the entire approach
				// from wherever the character stood. That is far more disruption than the fault
				// warrants: the placement is usually fine and only the ANGLE has drifted. Retreating
				// a couple of centimetres along the hole axis unloads the shaft, lets it straighten,
				// and leaves everything else where it already was — so the next attempt starts from
				// a good pose instead of rebuilding one.
				//
				// Bounded by an attempt count, because "back off and try again" with no limit is a
				// loop, not a recovery.
				// ONCE INSIDE, THIS DETECTOR IS MEASURING THE WRONG THING ENTIRELY.
				//
				// Every quantity here describes an approach: the angle between the shaft and the
				// entrance, and the distance from the tip to a point just inside it. After entry
				// those stop being errors and become descriptions of normal use — the tip travels
				// deep, so tipMiss IS the penetration depth, and the shaft angle relative to the
				// entrance swings as the stroke runs. Measured after a good entry:
				//
				//     tipMiss=0.073m (limit 0.035)  angle=41deg  bend=0.018
				//
				// which is not a failed dock, it is a successful one being stroked. Firing here set
				// ExitReason.Retry, which restarted the seek — so a completed entry pulled out and
				// re-approached, repeatedly. That is the random stopping and starting, and the
				// re-approach is free to pick a different hole, which is the target changing.
				//
				// Placement is finished the moment the game says we are in. From there AutoThrust
				// owns the pelvis and the game owns the geometry.
				if (base.Session.Player.Character.pene.isPenetrating)
				{
					state.DockAttempts = 0;
					return;
				}

				float dockCollinearDeg = Vector3.Angle(shaftDir, -state.Hole.forward);
				bool lostLine = dockCollinearDeg > Mathf.Max(6f, AutoSeekTuning.CollinearAbortDeg);
				bool bowing = BendNow() > SeekBendAbort;

				// THE TIP ITSELF MUST STAY ON TARGET.
				//
				// Angle and bend are both properties of the SHAFT, and both can look acceptable
				// while the tip has slid off the entrance entirely — riding up over it, or pushed
				// aside as she moves. The white marker is the ground truth for where the tip is,
				// and the hole transform is where it needs to be, so compare them directly instead
				// of inferring position from two shape measurements.
				// MEASURE AGAINST THE SAME POINT WE DRIVE AT. `state.Hole.position + pressIn` is
				// built from axisOut/HoleOutDirection — the wrong axis, ~120 degrees from the one
				// every other part of the dock uses. So this reference sat 12 mm off in a direction
				// the tip never travels, tipMiss never fell below the threshold however perfect the
				// placement, and "slipped off target" fired at 1.1 degrees with zero bend. Five of
				// those restarted the approach from scratch, which re-picks the hole — the random
				// stopping, starting, and target changes.
				//
				// Fourth place this same axis mismatch has hidden. Any comparison of "where the tip
				// is" against "where it should be" has to come from DockPressPoint()/DockAxisOut(),
				// not from a second expression that ought to agree.
				float tipMiss = (PeneClosestPointTo(state.Hole) - DockPressPoint()).magnitude;
				bool slipped = tipMiss > DockTipMissMax;
				if (lostLine || bowing || slipped)
				{
					state.DockAttempts++;
					if (state.DockAttempts > MaxDockAttempts)
					{
						logger.Info("[AutoSeek] {0} after {1} dock attempts - re-approaching from "
							+ "scratch (tipMiss={2:F4}m/{3:F3}, angle={4:F1}deg, bend={5:F4})",
							lostLine ? "off-axis" : (bowing ? "bowing" : "tip off target"),
							state.DockAttempts, tipMiss, DockTipMissMax, dockCollinearDeg, BendNow());
						state.ExitReason = ExitReason.Retry;
						return;
					}
					// REPORT THE NUMBER THAT TRIGGERED IT. This printed angle and bend — the two
					// values that look healthy — while the trigger was tipMiss, so the abort read
					// as "backing off at 1.2 degrees for no reason". Print all three, and which
					// one fired.
					logger.Info("[AutoSeek] dock attempt {0}: {1} (tipMiss={2:F4}m/{3:F3} "
						+ "angle={4:F1}deg bend={5:F4}) - backing off to the {6:F0}cm standoff",
						state.DockAttempts,
						lostLine ? "LOST THE LINE" : (bowing ? "BOWING" : "TIP SLIPPED OFF TARGET"),
						tipMiss, DockTipMissMax, dockCollinearDeg, BendNow(), StandoffDist * 100f);
					state.FinalStage = false;
					state.Retreating = true;   // retreat to the standoff, then re-approach
					state.RetreatT = 0f;
					state.PressAccum = 0f;
					state.DwellT = 0f;
					ResetSmoothing();              // do not carry the dock's velocity into a retreat
					return;
				}

				float bend = BendNow();
				if (bend > SeekBendAbort)
				{
					state.BackoffAccum += Mathf.Abs(TranslateTowards(-SeekBackoffDist));
					base.Session.Player.Move(new Vector3(0f, 0f, TranslateTowards(-SeekBackoffDist)));
					if (state.BackoffAccum > SeekBackoffDist)
					{
						logger.Info("[AutoSeek] bend {0:P0} while pressing - backing off and "
							+ "re-approaching (shaft was bowing, not entering)", bend);
						state.ExitReason = ExitReason.Retry;
					}
					return;
				}

				// ARRIVE, THEN WAIT. Penetration is NEGOTIATED, not forced.
				//
				// The game runs a handshake: Penetrador raises peneTryingEnterInHole when the tip
				// is at the entrance, Penetraciones.AceptaPenetracion decides whether to accept,
				// and GetNextCoolDown paces retries. Pressing hard through that does not speed it
				// up — it shoves the hole away, so the tip never sits still at the entrance long
				// enough for the check to fire, and the seeker reads the absence of penetration as
				// "press harder". That is the loop we were in.
				//
				// So once the tip is at the threshold, HOLD position and let the game's own
				// detection run. Pressing resumes only if the dwell expires without acceptance,
				// which distinguishes "not yet accepted" from "not actually touching".
				// Hold when the GAME says it is attempting entry — or, failing that signal, when we
				// are close enough that it probably is. The event is the real condition; the
				// distance test is only a fallback for hole types that never raise it.
				// WAIT AT CONTACT, NOT NEAR IT.
				//
				// The distance fallback fired at 2 cm, so the tip parked two centimetres short and
				// held there for the penetration check — a check that cannot pass, because nothing
				// is touching. It waited politely in the wrong place.
				//
				// The event is the real signal and needs no distance at all: the game raises
				// "trying to enter" precisely when contact is being attempted. The fallback is now
				// tight enough (5 mm) to mean touching rather than approaching, and exists only for
				// hole types that never raise the event.
				// CONTACT IS MEASURED, NOT INFERRED.
				//
				// Distance was always a proxy for "are we touching", and a bad one: it cannot tell
				// a tip resting against the entrance from one hovering a centimetre short, and
				// getting that wrong is what produced both the standoff and the pointless dwells.
				// The game answers it outright — Penetraciones.currentHits carries hayHits and a
				// real contact count against the hole's parts (this is the same signal
				// DragControl gates its withdrawal on).
				//
				// So: only hold if something is ACTUALLY in contact. No contact means keep
				// advancing, however close the geometry claims we are.
				// ── CONTACT-PRESSURE CONTROL ─────────────────────────────────────────────────
				//
				// Applies to EVERY hole, not just the boca: contact validates that we are pressing
				// on the right thing, and losing it says we are not.
				//
				//   never had contact      -> keep approaching; we simply have not arrived
				//   contact below target   -> advance gently to BUILD pressure
				//   contact at target      -> hold, and let the game's negotiation run
				//   contact LOST after     -> we slipped off the entrance. Back off and re-aim;
				//   having had it             pressing harder into whatever we slid onto is not
				//                             going to become penetration.
				//
				// That last case is the one geometry cannot diagnose at all. A tip that has ridden
				// up over the entrance still reads as "the right distance away", so distance says
				// press on while contact says we are pushing against nothing.
				int contacts = HoleContactCount();
				bool touching = contacts > 0;
				if (touching) state.ContactSeen = true;

				if (state.ContactSeen && !touching)
				{
					state.NoContactT += Time.deltaTime;
					if (state.NoContactT > ContactLostSeconds)
					{
						state.DockAttempts++;
						logger.Info("[AutoSeek] contact LOST during dock (attempt {0}) - slipped "
							+ "off the entrance; backing off to re-aim rather than pressing on",
							state.DockAttempts);
						state.ContactSeen = false;
						state.NoContactT = 0f;
						state.FinalStage = false;
						state.Retreating = true;   // actually back off, do not just re-run from here
						state.RetreatT = 0f;
						state.PressAccum = 0f;
						state.DwellT = 0f;
						ResetSmoothing();
						if (state.DockAttempts > MaxDockAttempts) state.ExitReason = ExitReason.Retry;
						return;
					}
				}
				else
				{
					state.NoContactT = 0f;
				}

				// Enough pressure already: stop advancing and let the negotiation work. Pushing
				// past this only displaces the hole, which is what prevented entry in the first
				// place.
				bool pressureOk = contacts >= ContactTargetCount;
				bool trying = GameIsTryingToEnter;
				if (trying || pressureOk)
				{
					state.DwellT += Time.deltaTime;
					// While the game is actively trying, keep waiting indefinitely — it is pacing
					// its own retries and interrupting them is exactly the mistake. The timeout
					// applies only to the inferred case, where we might be waiting on nothing.
					if (trying || state.DwellT < SeekDwellSeconds)
					{
						logger.InfoRare(45,
							"[AutoSeek] holding at threshold (dz={0:F4}m, {1}) after {2:F2}s - "
							+ "letting the game's penetration check run", dp.z,
							trying ? "GAME SAYS TRYING" : "inferred from distance", state.DwellT);
						return;
					}
				}
				else
				{
					state.DwellT = 0f;
				}

				// PRESSURE BUILDING vs APPROACH. Once something is touching, advance gently — we
				// are now loading soft tissue, not covering distance, and full approach rate here
				// is what shoves the hole aside instead of seating into it. With no contact at
				// all, keep the full rate: there is nothing to be gentle with yet.
				float pressScale = touching ? ContactPressScale : 1f;

				// Aim beyond the plane so contact resistance cannot leave us asymptotically
				// short.
				float goal = (dp.z + SeekPressDepth) * pressScale;
				float dv3 = TranslateTowards(goal);
				base.Session.Player.Move(new Vector3(0f, 0f, dv3));
				state.PressAccum += Mathf.Abs(dv3);

				// BOUNDED — but a RETRY, not a failure. Pressing this far without penetrating
				// means this attempt's pose was wrong, which a fresh approach can fix; it does not
				// mean the target is unreachable. Reporting it as UnreachableTarget disarmed the
				// whole loop, so one bad attempt ended the session — which is precisely the
				// "loop keeps disarming" behaviour.
				if (state.PressAccum > SeekPressMax)
				{
					logger.Info("[AutoSeek] pressed {0:F3}m without penetrating - re-approaching",
						state.PressAccum);
					state.ExitReason = ExitReason.Retry;
				}
			}
		}

		// ── BALLISTIC VERTICAL MOVE ──────────────────────────────────────────────────────────
		//
		// Command-units-to-world-metres for AddVerticalDelta. NOT a constant we can look up: it
		// varies with character scale, with pose, and with the aperture coupling that tilts the
		// shaft as the pelvis rises (|y| maps to up to 50 degrees), so the tip does not move
		// purely vertically. Measuring it from our own move sidesteps all of that — and because
		// the estimate is refined every attempt, the first move may be 20 % off and the ones after
		// it are not.
		//
		// Seeded at 1.0 because the pelvis ranges are already expressed in metres, so that is the
		// right order of magnitude to start from rather than a guess.
		private float vertGain = 1f;

		/// <summary>Ballistic travel time. Long enough to look like a movement rather than a
		/// teleport, short enough that the target has not meaningfully moved during it.</summary>
		private const float BallisticSeconds = 0.28f;

		/// <summary>Settle time after the move before measuring or trimming. This is the whole
		/// point: measuring while the IK is still catching up is measuring the lag, not the
		/// result — which is the mistake the closed loop was making every frame.</summary>
		private const float BallisticSettleSeconds = 0.22f;

		private float ballisticRemaining;
		private float ballisticCommanded;
		private float ballisticStartTipY;
		private float ballisticSettleT;
		private bool ballisticActive;

		/// <summary>False once any OTHER actuator moved during the window, which makes the
		/// commanded-vs-achieved comparison meaningless.</summary>
		private bool ballisticClean;
		private bool ballisticDone;

		private void ResetBallistic()
		{
			ballisticActive = false;
			ballisticDone = false;
			ballisticRemaining = 0f;
			ballisticCommanded = 0f;
			ballisticSettleT = 0f;
		}

		/// <summary>
		/// Drives the whole vertical correction open-loop, then measures what it achieved.
		/// Returns true while it owns the axis.
		/// </summary>
		private bool UpdateVerticalBallistic(float errY)
		{
			if (ballisticDone) return false;

			if (!ballisticActive)
			{
				// Too small to be worth a ballistic move — let the trim have it.
				if (Mathf.Abs(errY) < BallisticMinDist) { ballisticDone = true; return false; }

				ballisticCommanded = errY / Mathf.Max(0.15f, vertGain);
				ballisticRemaining = ballisticCommanded;
				ballisticStartTipY = PeneClosestPointTo(state?.Hole).y;
					ballisticClean = true;
			ballisticSettleT = 0f;
				ballisticActive = true;
				logger.Info("[AutoSeek] vertical ballistic: err={0:F4}m gain={1:F3} -> command={2:F4}",
					errY, vertGain, ballisticCommanded);
			}

			if (Mathf.Abs(ballisticRemaining) > 1E-05f)
			{
				// Spread over a fixed WINDOW rather than a fixed rate, so the move takes the same
				// time whether it is 2 cm or 20 cm — it reads as one deliberate motion instead of
				// a long crawl for big corrections and a twitch for small ones.
				float step = ballisticCommanded * Time.deltaTime / BallisticSeconds;
				step = Mathf.Clamp(step, -Mathf.Abs(ballisticRemaining), Mathf.Abs(ballisticRemaining));
				if (Mathf.Sign(step) != Mathf.Sign(ballisticRemaining)) step = ballisticRemaining;
				ballisticRemaining -= step;
				ctl.AddVerticalDelta(step);
				return true;
			}

			// Settle, THEN measure. No commands during this window.
			ballisticSettleT += Time.deltaTime;
			if (ballisticSettleT < BallisticSettleSeconds) return true;

			float achieved = PeneClosestPointTo(state?.Hole).y - ballisticStartTipY;
			if (Mathf.Abs(ballisticCommanded) > 1E-04f)
			{
				float measured = achieved / ballisticCommanded;

				// ONLY LEARN FROM A CLEAN WINDOW.
				//
				// The first version attributed the whole tip movement to the vertical command and
				// produced nonsense — ratios of -0.29, +5.34, +3.09, -1.18, 0.065 in a single run.
				// Nothing was wrong with the arithmetic; the EXPERIMENT was confounded. During the
				// window the avatar is also walking and rotating, and for the boca the target is
				// head-parented and moves ~40x more than a pelvis hole. Measuring one actuator
				// while three other things move is not a measurement.
				//
				// Sanity bounds were not enough: they rejected the absurd values and let the merely
				// wrong ones through, which walked the gain from 1.09 down to 0.59 — worse than
				// never learning at all. So learning now requires that NOTHING ELSE was commanded
				// during the window, and that the movement went the way we asked. An unlearned
				// gain of 1.0 costs one trim pass; a mislearned one corrupts every future move.
				bool clean = ballisticClean
					&& Mathf.Sign(achieved) == Mathf.Sign(ballisticCommanded)
					&& measured > 0.4f && measured < 2.5f;
				if (clean)
				{
					vertGain = Mathf.Lerp(vertGain, measured, 0.35f);
				}
				logger.Info("[AutoSeek] vertical ballistic done: commanded={0:F4} achieved={1:F4} "
					+ "ratio={2:F3} clean={3} -> gain {4:F3}", ballisticCommanded, achieved,
					measured, clean, vertGain);
			}
			ballisticDone = true;
			ballisticActive = false;
			return false;
		}

		/// <summary>Below this the ballistic move is not worth its settle time; the trim is both
		/// faster and more accurate at this scale.</summary>
		private const float BallisticMinDist = 0.02f;

		// ── VERTICAL: CONTINUOUS, WITH LAG COMPENSATION ──────────────────────────────────────
		//
		// Replaces the ballistic move-then-settle-then-trim. That worked in the sense that it did
		// not oscillate, but discrete phases are inherently janky — a fast open-loop lunge, a dead
		// pause, then a slow crawl — and the phase boundaries are visible. Three different motions
		// where there should be one.
		//
		// The underlying problem was never "how big a step", it was that dp.y describes a pose we
		// have ALREADY commanded away from: the IK lags, so the error we react to is stale and we
		// keep adding commands for a correction that is already on its way. That is integrator
		// windup, and windup has a standard fix that does not require phases — subtract the
		// commands that have been issued but not yet shown up in the measurement.
		//
		//     effectiveError = error - pending
		//     pending        = (everything commanded) - (everything observed)
		//
		// As the body catches up, pending falls and the controller naturally resumes. It is one
		// continuous motion, it cannot wind up, and it needs no settle window because it is never
		// reacting to a measurement it has already invalidated.
		private float vertPending;

		/// <summary>Previous frame's pelvis y, so the log can report ACHIEVED movement next to the
		/// commanded step. Commanded-vs-achieved is the only pair that separates "we asked for
		/// nothing" from "we asked and were ignored".</summary>
		private float vertLastPelvisY;

		private float vertLastOffY;

		private bool vertPrimed;

		private void ResetVertical()
		{
			vertPending = 0f;
			vertPrimed = false;
			pitchPending = 0f;
			pitchPrimed = false;
		}

		/// <summary>Remaining pelvis z travel in each direction, from the controller's own live
		/// range — not a constant. This is what the old MaxDepth check was reaching for.</summary>
		private void ZRoom(out float plus, out float minus)
		{
			plus = 0f;
			minus = 0f;
			try
			{
				PelvisMovementController.Range zr = ctl.zRange;
				float cur = pelvisTarget.z;
				plus = Mathf.Max(0f, zr.MaxLimited() - cur);
				minus = Mathf.Max(0f, cur - zr.MinLimited());
			}
			catch
			{
				// Unknown range: assume room rather than silently disabling the correction. A
				// wrong "yes" is rate-limited and recoverable; a wrong "no" is invisible, which is
				// the failure mode this whole change exists to remove.
				plus = 1f;
				minus = 1f;
			}
		}

		/// <summary>
		/// Vertical trim. Proportional, with bounded acceleration and a speed cap — nothing else.
		///
		/// THE DEBT MECHANISM IS GONE, and the measurement is why. It existed to compensate a laggy
		/// plant: commands were assumed to take several frames to take effect, so a record was kept
		/// of motion commanded-but-not-yet-observed and subtracted from the error. But the pelvis
		/// achieves essentially all of each command within the same frame —
		///
		///     errY=0.0348  step=0.000114   achieved=0.000148
		///     errY=0.0278  step=0.00037    achieved=0.00047
		///
		/// — so there is no lag to compensate, and the debt only ever cancelled the command. It
		/// converged to pending == errY, effective == 0, and the axis crawled at micrometres per
		/// frame while reporting healthy-looking numbers. Three separate lockups this session were
		/// that equilibrium, and each fix (bound the magnitude, then leak it, then leak it only
		/// while stalled) treated a symptom of a compensator that was not needed.
		///
		/// The oscillation that originally justified it came from the ORIGINAL uncapped
		/// proportional term (`dp.y * Min(1, dt*5*speed)`, up to 100 % of the error in one frame),
		/// not from lag. SmoothAxis already bounds acceleration and top speed, which is the actual
		/// cure for that, and it does not fight the command.
		/// </summary>
		private float VerticalStep(float errY)
		{
			return smoothVert.Step(errY, Time.deltaTime, AutoSeekTuning.ApproachTau,
				SmoothMaxSpeed, SmoothAccel);
		}

		private float VerticalStepLegacy_Unused(float errY)
		{
			if (!vertPrimed) { vertLastOffY = errY; vertPrimed = true; }

			// THE OBSERVATION IS THE ERROR SHRINKING. No pelvis offset needed: any command that
			// has actually taken effect shows up as the remaining error getting smaller, which is
			// exactly the quantity the debt is denominated in. It also makes the compensation
			// indifferent to WHICH actuator ultimately moved the tip.
			//
			// Caveat worth stating rather than hiding: the error also changes when SHE moves, and
			// that gets mis-attributed to our command. The pending clamp bounds how far that can
			// drift, and her motion is slow next to the correction, so it costs a little accuracy
			// rather than stability.
			float observed = vertLastOffY - errY;
			vertLastOffY = errY;
			vertPending -= observed;

			// Bound the debt. An unbounded pending term would let a stuck axis (blocked, at its
			// range limit) accumulate a correction it can never work off, and the moment it came
			// free it would all arrive at once.
			vertPending = Mathf.Clamp(vertPending, -VertPendingMax, VertPendingMax);

			// LEAK THE DEBT — this is the fix for the vertical deadlock.
			//
			// `pending` is motion COMMANDED but not yet OBSERVED, and it is repaid only out of the
			// error shrinking. So if the axis does not move, nothing is ever repaid: pending grows
			// by each step until it equals errY, `effective` becomes zero, and the controller stops
			// commanding while believing the whole correction is already in flight. Measured in
			// game at the moment of lockup:
			//
			//     errY=0.11248  pending=0.11248  step=2e-09  pelvisY frozen  achieved=0
			//
			// pending tracking errY to five decimals is not a coincidence, it is the equilibrium.
			// Clamping the MAGNITUDE (above) does not help, because the failure is not a large
			// wrong correction — it is a permanent zero one.
			//
			// A slow leak makes the debt expire. If the axis is genuinely moving, observed repays
			// it far faster than this and the leak is irrelevant; if the axis is stuck, the loop
			// keeps asking rather than going quiet, and a stuck axis that keeps asking is visible
			// in the logs instead of looking like a converged solution.
			// ...but ONLY WHILE THE AXIS IS ACTUALLY STUCK.
			//
			// An unconditional leak trades one failure for the opposite one. The debt exists so we
			// do not re-command an error the body is still travelling towards; forgiving it on a
			// timer forgives it while the body IS moving, so the correction is issued twice, it
			// overshoots, the error flips sign, and the pelvis squats up and down — which is
			// exactly what a 0.12 m/s leak against a 0.12 m cap produced (full debt cleared in
			// about a second, well inside the plant's response time).
			//
			// "Stuck" is observable and needs no timer heuristics: the error is not shrinking even
			// though we are commanding. Leak only then, and the debt keeps doing its job whenever
			// the axis is live.
			if (Mathf.Abs(observed) < VertStallObserved && Mathf.Abs(errY) > TRANSLATION_PRECISSION)
			{
				vertStallT += Time.deltaTime;
			}
			else
			{
				vertStallT = 0f;
			}
			if (vertStallT > VertStallSeconds)
			{
				vertPending = Mathf.MoveTowards(vertPending, 0f,
					VertPendingLeakPerSec * Time.deltaTime);
			}

			float effective = errY - vertPending;
			float step = smoothVert.Step(effective, Time.deltaTime, AutoSeekTuning.ApproachTau,
				SmoothMaxSpeed, SmoothAccel);
			vertPending += step;
			return step;
		}

		/// <summary>Cap on commanded-but-unrealised vertical motion.</summary>
		// ── Timed dock cycle ────────────────────────────────────────────────────────────────
		/// <summary>Range at which the timed cycle takes over from plain transit.</summary>
		private const float DockCycleRange = 0.09f;

		/// <summary>Range at which the cycle gives up the near field. Must be comfortably beyond
		/// DockCycleRange: the retreat deliberately drives outward, so a single threshold makes the
		/// cycle cancel itself the moment it starts working.</summary>
		private const float DockCycleExitRange = 0.16f;

		/// <summary>Where the cycle aligns from: far enough out that the geometry is clean, close
		/// enough that the commit is short and its deadline meaningful.</summary>
		private const float DockStandoff = 0.03f;

		private const float DockStandoffTol = 0.008f;

		/// <summary>How close the tip must physically get to the calibration point before any dock
		/// is attempted. Owner-specified: 3 mm. This is a POSITION gate — the failure it exists to
		/// catch is a shaft that is perfectly parallel to the axis and centimetres off it.</summary>
		private const float DockCalTolerance = 0.003f;

		/// <summary>
		/// Above this hole speed the station is not stationary and a missed calibration point is
		/// the target's doing, not the controller's. Same threshold the stroke audit uses to call a
		/// run INCONCLUSIVE rather than print a pass/fail it cannot justify (ALIGNMENT_CAPABILITY_MAP
		/// V8): an instrument that cannot see a stationary target cannot judge a stationary-target
		/// theory.
		/// </summary>
		private const float HoleSpeedStationary = 0.05f;

		/// <summary>Compression rise over the unloaded baseline that counts as bearing on the hole.
		/// Small: this is "touching", not "pushing".</summary>
		private const float CommitBendRise = 0.015f;

		/// <summary>Rise at which we are bowing the shaft rather than entering, and must back off.
		/// The gentleness bound — without it, "press until something happens" deforms the chain.</summary>
		private const float CommitBendAbort = 0.07f;

		/// <summary>Compression only counts as hole contact this close. Further out, a rise is the
		/// shaft meeting her thigh or belly, which is exactly what we must not mistake for a dock.</summary>
		private const float CommitPressRange = 0.022f;

		/// <summary>Time constant for tracking the unloaded bend baseline while out of contact
		/// range. Fast enough to follow pose drift, slow enough not to chase per-frame noise.</summary>
		private const float CommitBendRefTau = 0.25f;

		/// <summary>How far past the entrance COMMIT presses. Small on purpose — enough for firm
		/// contact so the game's penetration check has something to accept, not a shove.</summary>
		private const float DockPressPast = 0.005f;

		/// <summary>How long to hold at the entrance while the game negotiates entry. Generous:
		/// entry is on the game's own retry cooldown, and withdrawing early throws away a correct
		/// position and restarts the negotiation.</summary>
		private const float DockPressHoldSeconds = 3f;

		/// <summary>How fast the dock setpoint may travel between stations, m/s. Covers the 3.5 cm
		/// between the calibration point and the press point in about 0.2 s — quick enough to add
		/// no real delay, slow enough that no single frame ever sees a step.</summary>
		private const float DockTargetSlewPerSec = 0.18f;

		/// <summary>Top speed of the unified near-field move, m/s before SpeedScale. Modest: this
		/// is placement within a few centimetres, where smoothness beats haste.</summary>
		private const float DockUnifiedMaxSpeed = 0.25f;

		/// <summary>Acceleration limit for the same, m/s². Bounding acceleration rather than only
		/// speed is what removes the visible snap at the start and end of each move.</summary>
		private const float DockUnifiedAccel = 1.2f;

		/// <summary>Time constant for the unified near-field move. Short — these are centimetre
		/// moves inside timed stages, and the smoothness comes from the acceleration bound above,
		/// not from a long constant. ApproachTau (0.35) made a 3 cm move take about a second and
		/// caused stages to time out.</summary>
		private const float DockUnifiedTau = 0.10f;

		/// <summary>Multiplies the dock time constant for the boca. The head moves an order of
		/// magnitude more than the hips, so the loop must follow its average position rather than
		/// track every excursion.</summary>
		private const float BocaDockTauScale = 1.3f;

		/// <summary>Head tilt below this is ignored entirely — the boca axis is treated as level.
		/// Covers the constant small motion that was being chased as target movement.</summary>
		private const float BocaLevelFreeDeg = 12f;

		/// <summary>Tilt at or above this is taken at face value: a deliberately raised or lowered
		/// head is real, and levelling it would aim at somewhere the mouth is not.</summary>
		private const float BocaLevelFullDeg = 40f;

		/// <summary>Nose-up presentation angle for the boca. Her head turns in response to the
		/// player's hip angle, so this is an INPUT to where the target ends up, not just an aim.</summary>
		private const float BocaUpTiltDeg = 8f;

		/// <summary>Peak speed of the direct lip tap, m/s. Applied as a velocity through
		/// Player.Move, so the excursion is roughly BocaTapSpeed / (2*pi*BocaTapHz) — about a
		/// centimetre at these values.</summary>
		private const float BocaTapSpeed = 0.28f;

		/// <summary>Tapping time before backing off to re-present.</summary>
		private const float BocaAttemptSeconds = 5f;

		/// <summary>How long to stay backed off before returning. Brief — the withdrawal exists to
		/// change the presentation, not to wait.</summary>
		private const float BocaBackoffSeconds = 0.25f;

		/// <summary>Angle allowance for committing to the boca. Wide by design: the 8 degree
		/// presentation tilt plus the hip-pitch lock leave a standing angle the controller is
		/// deliberately not allowed to null, so gating tightly on it can only ever fail.</summary>
		private const float BocaCommitAngleDeg = 30f;

		/// <summary>How far to withdraw between attempts.</summary>
		private const float BocaBackoffDist = 0.01f;

		/// <summary>Half-excursion of the lip tap, metres. Millimetres — it should read as the tip
		/// resting and touching, not as a stroke.</summary>
		private const float BocaTapAmplitude = 0.010f;

		/// <summary>How far PAST the lip surface the tap is centred. The oscillation then spends
		/// most of each cycle in contact rather than only grazing at the extreme, which is what
		/// survives the smoother's lag and amplitude attenuation.</summary>
		private const float BocaTapPress = 0.008f;

		/// <summary>Tap rate, Hz. Slow enough to look deliberate and to give her time to respond
		/// between touches.</summary>
		private const float BocaTapHz = 1.1f;

		/// <summary>Pitch error at which the hips stop adjusting for the boca. Generous, because
		/// "close enough to present" is the goal — precision here comes from translation, which
		/// does not move her head.</summary>
		private const float BocaPitchLockDeg = 8f;

		/// <summary>Pitch error that releases the lock. The gap from the lock threshold is the
		/// hysteresis that stops the hips hunting.</summary>
		private const float BocaPitchReleaseDeg = 22f;

		/// <summary>Alignment gate for COMMITTING — deliberately tighter than the advisory
		/// collinearity tolerance, because this is the number the whole approach is bet on.</summary>
		private const float DockTightDeg = 6f;

		private const float DockTightLateral = 0.006f;

		/// <summary>Lateral miss still worth pressing from once ALIGN has had its full time. Wider
		/// than the clean gate, well under the entrance, and paired with the tight ANGLE gate so a
		/// parallel-but-badly-offset shaft still gets sent back.</summary>
		private const float DockLooseLateral = 0.015f;

		/// <summary>How long alignment must HOLD before committing. One frame inside tolerance is
		/// noise; the reason for aligning at a fixed standoff is to let it settle.</summary>
		private const float DockAlignDwell = 0.15f;

		private const float DockAlignMaxSeconds = 2.5f;

		/// <summary>Metres of "score" per degree, so angle and position share one progress measure.
		/// A degree is worth about a millimetre here — roughly their relative importance to whether
		/// the dock will succeed.</summary>
		private const float AlignAngleWeight = 0.001f;

		/// <summary>Improvement that counts as progress. Above measurement noise, well below the
		/// gates.</summary>
		private const float AlignProgressEps = 0.0004f;

		/// <summary>No improvement for this long means genuinely stuck, not slow. THIS is the align
		/// failure condition; the elapsed-time limit only decides when to start checking.</summary>
		private const float AlignStallSeconds = 1.2f;

		private const float DockRetreatMaxSeconds = 1.2f;

		/// <summary>Commit speed, m/s. The standoff is covered in DockStandoff/this seconds — about
		/// half a second for 3 cm — which is what sets the deadline.</summary>
		private const float DockCommitSpeed = 0.06f;

		/// <summary>Slack on the commit deadline for plant lag. Above 2 it stops being a deadline.</summary>
		private const float DockCommitGrace = 1.5f;

		/// <summary>Fraction of a measured miss fed back as aim bias. Well under 1 so the
		/// correction converges instead of oscillating between over-corrections.</summary>
		private const float DockBiasGain = 0.6f;

		private const float DockBiasMax = 0.04f;

		private const float VertPendingMax = 0.12f;

		/// <summary>Metres per second of pending-debt forgiveness. Sized so a genuinely moving axis
		/// repays via observation much faster than this (making the leak a no-op), while a stuck one
		/// clears a full VertPendingMax debt in about a second instead of never.</summary>
		private const float VertPendingLeakPerSec = 0.12f;

		/// <summary>Per-frame error shrink below which the axis counts as not responding.</summary>
		private const float VertStallObserved = 0.0002f;

		/// <summary>How long the axis must fail to respond before the debt starts expiring. Long
		/// enough that normal plant lag never trips it, short enough that a real block is not a
		/// visible hang.</summary>
		private const float VertStallSeconds = 0.3f;

		/// <summary>Time the vertical axis has been commanded without the error responding.</summary>
		private float vertStallT;

		/// <summary>Cap on commanded-but-unrealised PITCH motion, in pelvis z units.</summary>
		private const float PitchPendingMax = 0.10f;

		private float pitchPending;

		private float pitchLastErr;

		private bool pitchPrimed;

		/// <summary>Shaft bow, 0 = straight. The game's own matched length pair, same measure
		/// AutoThrust throttles on.</summary>
		private float BendNow()
		{
			try
			{
				Penis p = base.Session.Player.Character.pene;
				float ideal = p.worldLengthFromUnderSkin;
				if (ideal <= 0.0001f) return 0f;
				return Mathf.Clamp01(1f - p.realCurrentWorldLengthFromUnderSkin / ideal);
			}
			catch
			{
				return 0f;
			}
		}

		/// <summary>
		/// Move toward a target with a PROPORTIONAL approach rather than a constant crawl.
		///
		/// The original moved a fixed 0.1*dt regardless of how far away the target was, so a 20 cm
		/// leg and a 2 mm leg proceeded at exactly the same speed — long legs took forever and
		/// short ones micro-adjusted, which is the mechanical, stop-start feel. Approaching at a
		/// rate proportional to the remaining distance (capped, so it can never exceed the old
		/// maximum) is fast when far and identical when close.
		///
		/// The cap is still per-SECOND. Several steps elsewhere in this file move a fixed amount
		/// per TICK, which silently makes their speed depend on frame rate — 1.44 m/s at 144 fps
		/// versus 0.3 m/s at 30 — so the same placement behaves differently on different machines.
		/// </summary>
		// ── MOTION SMOOTHING ─────────────────────────────────────────────────────────────────
		//
		// Every axis commanded a POSITION DELTA per frame, so motion was a sequence of discrete
		// jumps whose size changed abruptly whenever the error or a rate limit changed. No state
		// carried between frames means no continuity — which is what "a thousand little teleports"
		// looks like. Smoothing the step SIZE would not fix it either: the discontinuity is in the
		// derivative, not the value.
		//
		// So each axis keeps a velocity that may only change at a bounded rate, and the step is
		// that velocity integrated over the frame. Starts ease in, stops ease out, and a sudden
		// change of target produces a curve instead of a corner. Accuracy is unaffected — the
		// velocity still converges to zero at the target — but the path is now C1-continuous
		// rather than piecewise constant.
		private sealed class SmoothAxis
		{
			private float v;

			public void Reset() { v = 0f; }

			public float Step(float err, float dt, float tau, float maxSpeed, float maxAccel)
			{
				if (dt <= 0f) return 0f;
				float want = Mathf.Clamp(err / Mathf.Max(0.02f, tau), -maxSpeed, maxSpeed);

				// BRAKING LIMIT — never carry more speed than you can shed before the target.
				//
				// The per-frame clamp below stops the COMMAND overshooting in a single tick, but it
				// cannot stop the axis arriving too fast: deceleration is bounded by maxAccel, so a
				// velocity picked up while the error was large may be more than can be bled off over
				// the distance that remains. It reaches zero error still moving, and the next frame's
				// correction is in the opposite direction — the residual vertical overshoot.
				//
				// The distance needed to stop from speed v at deceleration a is v^2/(2a), so the
				// speed that can still be stopped within |err| is sqrt(2*a*|err|). Clamping to that
				// makes arrival-at-rest a property of the controller instead of something the gains
				// have to be detuned to approximate. It only binds near the target; far away
				// tau/maxSpeed still govern, so nothing gets slower.
				float brake = Mathf.Sqrt(2f * Mathf.Max(0.0001f, maxAccel) * Mathf.Abs(err));
				want = Mathf.Clamp(want, -brake, brake);

				v = Mathf.MoveTowards(v, want, maxAccel * dt);
				float step = v * dt;
				// Never overshoot within a frame: smoothing must not cost accuracy, or it just
				// trades one visible artefact for another.
				if (Mathf.Abs(step) > Mathf.Abs(err)) { step = err; v = (dt > 0f) ? err / dt : 0f; }
				return step;
			}
		}

		/// <summary>
		/// ONE smoother for the whole near-field move — the straight-line-stroke pattern from
		/// ALIGNMENT_CAPABILITY_MAP §12.2, applied to placement instead of to the stroke.
		///
		/// The independent smoothers below (flat for x/z, vertical for y) each have their own time
		/// constant, speed cap and acceleration limit, so they converge at DIFFERENT rates and
		/// finish at DIFFERENT times. Two consequences, both visible: the tip travels an arc rather
		/// than a line, and when the faster axis saturates or completes, the remaining motion
		/// changes direction abruptly — the teleporting hitch.
		///
		/// Smoothing ONE magnitude and decomposing it across the actuators keeps the direction
		/// fixed for the whole move: every axis is a fixed fraction of the same scalar, so they
		/// start together, end together, and the path between is straight by construction.
		/// </summary>
		private readonly SmoothAxis smoothDock = new SmoothAxis();

		private readonly SmoothAxis smoothFlat = new SmoothAxis();

		private readonly SmoothAxis smoothVert = new SmoothAxis();

		private readonly SmoothAxis smoothPitch = new SmoothAxis();

		private readonly SmoothAxis smoothYaw = new SmoothAxis();

		private void ResetSmoothing()
		{
			smoothFlat.Reset();
			smoothVert.Reset();
			smoothPitch.Reset();
			smoothYaw.Reset();
		}

		/// <summary>Acceleration bound, units/s^2. Scales with SpeedScale so raising the speed also
		/// raises how briskly it may build to it, instead of making it lurch.</summary>
		private float SmoothAccel => 1.2f * Mathf.Max(0.25f, AutoSeekTuning.SpeedScale);

		private float SmoothMaxSpeed => TRANSLATION_SPEED * Mathf.Max(0.05f, AutoSeekTuning.SpeedScale);

		private float TranslateTowards(float target, float dampAt = 0f)
		{
			float maxDelta = TRANSLATION_SPEED * Mathf.Max(0.05f, AutoSeekTuning.SpeedScale) * Time.deltaTime;
			if (Mathf.Abs(target) < dampAt)
			{
				maxDelta /= 10f;
			}
			// Proportional term: close the remaining distance on a ~0.35 s time constant, bounded
			// by the same maximum rate as before so this can only ever be faster where it was
			// needlessly slow, never faster than the old ceiling.
			float proportional = Mathf.Abs(target) * Time.deltaTime / Mathf.Max(0.02f, AutoSeekTuning.ApproachTau);
			float step = Mathf.Min(maxDelta, Mathf.Max(proportional, maxDelta * 0.15f));
			return Mathf.MoveTowards(0f, target, step);
		}

		private bool UpdateRotation()
		{
			Transform root = base.Session.Player.RootMotion;
			float orientation = UnityUtils.FromToAxisAngle(root.forward, -state.Hole.forward, root.right);
			if (Mathf.Abs(orientation) > 80f)
			{
				state.ExitReason = ExitReason.VerticalAngleTooWide;
				return true;
			}
			float angle = UnityUtils.FromToAxisAngle(root.forward, -state.Hole.forward, root.up);
			if (Mathf.Abs(angle) < 1f)
			{
				return false;
			}
			// Yaw goes through the same bounded-acceleration smoother as the translation axes. It
			// was the one channel still using a raw MoveTowards, so while everything else eased in
			// and out the body still snapped between headings — and rotation is the most visible
			// motion of the lot, which is why it stood out.
			//
			// Sub-linear speed scaling for the same reason as pitch: this is a feedback loop, and
			// scaling a feedback rate linearly with SpeedScale is what made the other two
			// oscillate at 4x.
			float yawRate = AutoSeekTuning.RotateDegPerSec
				* Mathf.Sqrt(Mathf.Max(0.05f, AutoSeekTuning.SpeedScale)) * LeverScale()
				* (TargetIsBoca() ? BocaGainDamping : 1f);
			base.Session.Player.Rotate(smoothYaw.Step(angle, Time.deltaTime,
				AutoSeekTuning.ApproachTau, yawRate, yawRate * 3f));
			return true;
		}

		// THE AIM POINT IS THE END OF THE SHAFT, NOT THE START OF THE TIP SEGMENT.
		//
		// partePunta.position is the ORIGIN of the tip part — it sits worldTipPartLength BEHIND
		// the actual end of the pene. Aiming that transform at the hole therefore parks the real
		// tip a whole tip-length past the entrance, and because the shaft is rarely horizontal
		// the shortfall has a vertical component too: the seeker behaves as though the tip were
		// lower than it is, misses the entrance, and then overshoots on the correction.
		//
		// The old code half-knew this — UpdatePlacement carried `dp.z += worldTipPartLength * 0.1f`,
		// one TENTH of the offset, applied to the depth axis only so the vertical part was
		// dropped entirely. That fudge is removed; the offset is now applied in full, as a
		// vector, everywhere the tip is used.
		//
		// AutoThrust reads the tip as pene.punta.physicBone.position. Deriving it geometrically
		// here instead keeps the seeker working before the physics chain is live (it runs while
		// NOT penetrating, which is the whole point of it), and the two agree once it is.
		// CORRECTION (owner report: "tip doesn't get close enough now"). Adding the FULL
		// worldTipPartLength overshot in the other direction — the seeker then believed the tip
		// reached further forward than it does and stopped short. Both my estimate and the
		// original 0.1 fudge were guesses at an offset that does not need guessing: the game
		// already publishes the tip's own physics bone, which is what AutoThrust reads. Use it,
		// and fall back to the geometric estimate only when the physics chain is not live —
		// which does happen here, because the seeker runs while NOT penetrating.
		// STOP CALCULATING THE TIP — MEASURE IT (owner, after two failed derivations).
		//
		// History, because both wrong answers were wrong in opposite directions and a third
		// guess would be worthless:
		//   original   partePunta.position + worldTipPartLength * 0.1  on z only   -> undershot
		//   attempt 1  partePunta.position + worldTipPartLength (full vector)      -> overshot
		//   attempt 2  punta.physicBone.position                                   -> overshot
		// Every one of them models where the tip OUGHT to be from a length and a transform. The
		// question the seeker actually asks is different and purely geometric: which point of the
		// pene is nearest the hole, and how far away is it. Colliders answer that directly —
		// Collider.ClosestPoint is the real surface, whatever the bone lengths say — so the aim
		// point is now measured off the collision geometry the game itself penetrates with.
		//
		// Fallback chain preserved for when no collider is live (the seeker runs before contact):
		// physics bone, then the geometric estimate.
		private Collider[] peneColliders;

		/// <summary>Extra seating past first contact, metres along the shaft axis. Small enough
		/// to sit inside the soft body's give rather than forcing anything.</summary>
		/// <summary>Standoff distance out along the hole axis: the pose the dock launches from and
		/// retreats to. Far enough to unload the shaft, near enough that re-negotiating is quick.</summary>
		private const float StandoffDist = 0.02f;

		/// <summary>Back-off-and-retry cycles before giving up and re-approaching entirely.</summary>
		private const int MaxDockAttempts = 4;

		/// <summary>How far the true tip may sit from the intended entry point during a dock before
		/// the attempt is abandoned. Position ground-truth, independent of angle and bend.</summary>
		private const float DockTipMissMax = 0.035f;

		/// <summary>Contacts that count as seated. DragControl gates its withdrawal at 4, which is
		/// a reasonable independent read on what "properly engaged" means here.</summary>
		private const int ContactTargetCount = 3;

		/// <summary>Grace period before absent contact counts as a slip rather than a bounce.
		/// Contact flickers frame to frame on a physics chain; reacting to one empty frame would
		/// abort constantly.</summary>
		private const float ContactLostSeconds = 0.25f;

		/// <summary>Advance rate once touching. Loading tissue, not covering distance.</summary>
		private const float ContactPressScale = 0.35f;

		/// <summary>
		/// Forward authority retained while ACQUIRING. High on purpose: calibration happens WHILE
		/// approaching, not instead of it. At 0.35 the last few centimetres still crawled; the
		/// correction terms work perfectly well against a body that is also moving forward, and
		/// closing distance is what gives them the geometry to converge in.
		/// </summary>
		private const float AcquireAimFloor = 0.65f;

		/// <summary>Forward authority retained WHILE IN CONTACT, so the body follows a moving
		/// entrance instead of holding still in world space and losing it. Low: this is tracking,
		/// not pressing — the pressure controller owns how hard we push.</summary>
		private const float ContactFollowGain = 0.25f;

		/// <summary>Remaining distance at which the creep-to-contact begins.</summary>
		private const float ContactCreepStart = 0.03f;

		/// <summary>Creep speed, m/s. Slow: this is hunting for a surface, not travelling.</summary>
		private const float ContactCreepRate = 0.05f;

		/// <summary>Total creep allowed before concluding the hole is not where we think.</summary>
		private const float ContactCreepMax = 0.06f;

		/// <summary>
		/// Beyond this range the seeker is in TRANSIT: closing ground, not placing. No alignment
		/// gating, high speed, and the target is a point on the hole axis.
		///
		/// 8 cm, not 30: calibration wants to happen CLOSE, and it wants to happen WHILE still
		/// closing. Handing over early means a long slow crawl over ground that needed no care;
		/// handing over late means arriving with no room left to correct in.
		/// </summary>
		private const float TransitDist = 0.02f;

		/// <summary>How far out along the axis the transit standoff sits. Arriving here means
		/// arriving ON the line, so the fine approach never has to fix a large lateral error.</summary>
		private const float TransitStandoff = 0.015f;

		/// <summary>Transit speed ceiling, m/s before SpeedScale. A metre in about a second.</summary>
		private const float TransitSpeed = 0.9f;

		/// <summary>Transit time constant — brisk, because precision is not the goal yet.</summary>
		/// <summary>Forward gain during TOUCH: deliberate, not a crawl and not a lunge. The tip is
		/// aligned and the last couple of centimetres are the ones that matter.</summary>
		/// <summary>Smoothing for the tip-to-target distance.</summary>
		private const float TipDistTau = 0.12f;

		/// <summary>Smoothing for its rate. Slower than the distance: a derivative on a physics
		/// chain is mostly noise, and a jumpy rate would trip constantly.</summary>
		private const float TipRateTau = 0.30f;

		/// <summary>Growth faster than this counts as diverging, m/s. Above the noise floor of a
		/// shaft settling, below any real loss of the target.</summary>
		private const float TipDivergeRate = 0.012f;

		/// <summary>How long divergence must persist. She moves and the chain springs, so brief
		/// growth is normal and only a sustained trend is evidence.</summary>
		private const float TipDivergeSeconds = 0.4f;

		private const float TouchApproachGain = 0.45f;

		private const float TransitTau = 0.30f;

		/// <summary>Distance from the standoff that counts as "clear" during a retreat.</summary>
		private const float RetreatArrivedTol = 0.02f;

		/// <summary>Retreat watchdog. If getting clear is itself blocked, resume anyway rather
		/// than sitting in a recovery that cannot complete — a stuck recovery is just a
		/// different deadlock.</summary>
		private const float RetreatMaxSeconds = 1.5f;

		private const float SeekOvershoot = 0.012f;

		/// <summary>How far PAST the entrance the final stage aims. The tip has to displace skin
		/// to get in, so a goal exactly at the plane is a goal the contact can hold us short of
		/// indefinitely.</summary>
		private const float SeekPressDepth = 0.05f;

		/// <summary>Hard cap on total press distance. Past this, more force is not the answer.</summary>
		private const float SeekPressMax = 0.18f;

		/// <summary>Bow fraction at which pressing is abandoned. Below this the shaft is merely
		/// compressing against soft tissue; above it, it is buckling and will not enter.</summary>
		private const float SeekBendAbort = 0.12f;

		/// <summary>How far to retreat before re-approaching.</summary>
		private const float SeekBackoffDist = 0.06f;

		/// <summary>Tip-to-entrance distance counted as "at the threshold". Inside this we stop
		/// advancing and let the game decide.</summary>
		private const float SeekDwellDist = 0.005f;

		/// <summary>How long to wait there. Penetraciones paces its own retries via
		/// GetNextCoolDown, so this has to outlast at least one of those cycles.</summary>
		private const float SeekDwellSeconds = 1.2f;

		/// <summary>How long a "trying to enter" signal stays meaningful. Penetraciones paces its
		/// own retries, so a signal from a moment ago still means the attempt is in progress.</summary>
		private const float TryingSignalStaleSeconds = 0.75f;

		/// <summary>Pelvis Z rate while driving pitch, units per second. Rate-limited rather than
		/// proportional-uncapped, because the pelvis lags and commanding a whole correction in one
		/// frame is what made the vertical axis oscillate.</summary>
		private const float PitchRatePerSec = 0.35f;

		/// <summary>Bow above which tip-minus-base stops describing the aim. Deliberately TIGHTER
		/// than SeekBendAbort: we should stop trusting the direction well before the bend is bad
		/// enough to abandon the dock, or we spend the interval steering on a bad number.</summary>
		private const float SeekDirTrustBend = 0.06f;

		/// <summary>Pelvis reset speed, metres per SECOND. Was a fixed 0.01 per tick, i.e. frame-rate
		/// dependent — 0.6 m/s at 60 fps is the equivalent rate on this machine.</summary>
		private const float ResetRatePerSec = 0.6f;

		/// <summary>Depth-reset tolerance. Was 0.005, which is well inside the measured 0.06-0.08
		/// swing of an externally driven signal, so the test could never pass.</summary>
		private const float ResetTolerance = 0.025f;

		/// <summary>Hard bound on the depth reset. It precedes every actuator, so an unbounded
		/// wait here disables the whole feature — the difference between a slow start and a dead
		/// one.</summary>
		private const float ResetMaxSeconds = 1.0f;
		/// <summary>Distance over which forward motion is throttled by perpendicular error, and the
		/// range at which the approach hands over to the press.</summary>
		private const float ApproachGateDist = 0.08f;

		/// <summary>
		/// How far PAST the entrance to aim, along the hole's own axis (negative = into her).
		/// Identified by comparing against the three entrance transforms rather than by any stored
		/// flag, so it stays correct if the hole is reselected mid-session.
		/// </summary>
		/// <summary>
		/// Perpendicular distance from the true tip to the hole's AXIS LINE — the second half of
		/// collinearity, and the half that was missing.
		///
		/// Angle says which way we point; this says whether we are on the line at all. A shaft can
		/// be perfectly parallel and still slide past the entrance a few centimetres to the side,
		/// and no amount of angular correction fixes that, because the error is positional.
		/// </summary>
		private float LateralMissFromAxis(Transform hole)
		{
			return LateralOffsetFromAxis(hole).magnitude;
		}

		/// <summary>
		/// Offset from the hole's axis line to the true tip, as a VECTOR. The magnitude says how
		/// far off-line we are; the direction says which way to move to fix it — which is what
		/// makes this drivable instead of merely checkable.
		/// </summary>
		/// <summary>
		/// The timed dock cycle: RETREAT -> ALIGN -> COMMIT, with a deadline on the commit and a
		/// learned aim bias so repeated failures correct themselves instead of repeating.
		/// </summary>
		private void UpdateDockCycle(float rangeToHole)
		{
			state.DockStageT += Time.deltaTime;
			Vector3 shaft = PeneClosestPointTo(state.Hole)
				- ((IPene)base.Session.Player.Character.pene).parteBase.position;
			float aimErr = (shaft.sqrMagnitude > 1E-08f)
				? Vector3.Angle(shaft.normalized, -DockAxisOut()) : 180f;
			float lateral = LateralMissFromAxis(state.Hole);
			bool touching = HoleContactCount() > 0;

			if (state.DockStage == DockStage.None)
			{
				EnterDockStage(DockStage.Retreat, "entered the near field");
				return;
			}

			switch (state.DockStage)
			{
				case DockStage.Retreat:
					// MUST ARRIVE AT THE CALIBRATION POINT, not merely be somewhere past a
					// distance. Bounded in time too: a retreat blocked by her body must not hang
					// the cycle, and aligning slightly off-station beats never aligning at all —
					// but the timeout now announces itself instead of masquerading as arrival.
				{
					float calErrRetreat =
						(CalibrationPoint() - PeneClosestPointTo(state.Hole)).magnitude;
					bool arrived = calErrRetreat <= DockStandoffTol;
					if (arrived || state.DockStageT > DockRetreatMaxSeconds)
					{
						// Say WHICH exit fired. "at standoff 0.087m" while the standoff is 0.030
						// was the message that hid a retreat which never arrived and always timed
						// out — a status line that reports the same thing for success and failure
						// is worse than none.
						EnterDockStage(DockStage.Align, arrived
							? string.Format("arrived at the calibration point ({0:F4}m)", calErrRetreat)
							: string.Format("TIMED OUT after {0:F1}s still {1:F4}m from the "
								+ "calibration point - aligning from here anyway",
								DockRetreatMaxSeconds, calErrRetreat));
					}
					break;
				}

				case DockStage.Align:
				{
					// THE CALIBRATION POINT IS A HARD GATE ON POSITION, NOT ON ANGLE.
					//
					// The persistent failure is a shaft parallel to the hole's axis but a couple of
					// centimetres BELOW it — collinear by every angular measure and still aimed at
					// the wrong place. Angle and perpendicular-offset tolerances cannot catch that,
					// because the approach was allowed to keep closing while it corrected, so
					// "aligned" got satisfied somewhere the tip never actually was.
					//
					// So the tip must PHYSICALLY REACH a point on the hole's own axis, one standoff
					// out, to within DockCalTolerance. A waypoint that is occupied, not merely aimed
					// at. If it cannot get there it does not get to attempt a dock, however good its
					// angles look — and being on the line at a known distance is precisely what
					// makes the rest of the approach a straight run down that line.
					float calErr = (CalibrationPoint() - PeneClosestPointTo(state.Hole)).magnitude;

					// BOCA'S ANGLE IS A CHOICE, NOT AN ERROR.
					//
					// Two deliberate decisions make the strict angle gate unsatisfiable here, and
					// they are both correct on their own: the axis is tilted 8 degrees for
					// presentation, and the hip pitch is LOCKED once that presentation is reached
					// so it stops re-moving her head. Together they leave a standing angle the
					// controller is not permitted to remove — measured at a rock-steady 16.8
					// degrees while position sat at 0.0007 m against a 0.003 gate, failing every
					// attempt on the one term it was told not to touch.
					//
					// For the mouth, position and presentation are what matter; the remaining angle
					// is negotiated by tapping, not by pointing. So the gate keeps its full
					// strictness on POSITION and widens on angle.
					float angleGate = TargetIsBoca() ? BocaCommitAngleDeg : DockTightDeg;
					if (calErr <= DockCalTolerance && aimErr <= angleGate)
					{
						state.DockAlignHoldT += Time.deltaTime;
					}
					else
					{
						state.DockAlignHoldT = 0f;
					}

					if (state.DockAlignHoldT >= DockAlignDwell)
					{
						// Capture the unloaded bend HERE — on the line, at the standoff, demonstrably
						// touching nothing. Everything COMMIT reads is a rise from this.
						state.CommitBendRef = BendNow();
						EnterDockStage(DockStage.Commit, string.Format(
							"ON calibration point (err {0:F4}m <= {1:F3}m, angle {2:F1}deg, "
							+ "unloaded bend {3:F3})",
							calErr, DockCalTolerance, aimErr, state.CommitBendRef));
					}
					// STILL CONVERGING IS NOT A FAILURE.
					//
					// A fixed 2.5 s window assumes every approach starts near-parallel. From a
					// deliberately bad angle the corrections need longer, and the stage was expiring
					// mid-convergence — angle 24.3 -> 13.7 -> 13.2 and dropping, calErr 0.0064
					// against a 0.003 gate — then spending an attempt and retreating, which throws
					// away all of that progress and starts the same slow convergence again.
					//
					// So the deadline measures PROGRESS, not elapsed time: any meaningful
					// improvement in either term refreshes the clock. Only genuine stalling — no
					// improvement for AlignStallSeconds — is a real failure worth re-solving from
					// the standoff. Both terms count, because the angle often closes while the
					// position holds and vice versa.
					float score = calErr + aimErr * AlignAngleWeight;
					if (!state.AlignScorePrimed || score < state.AlignBestScore - AlignProgressEps)
					{
						state.AlignScorePrimed = true;
						state.AlignBestScore = score;
						state.AlignLastImproveT = state.DockStageT;
					}
					bool stalled = state.DockStageT - state.AlignLastImproveT > AlignStallSeconds;

					if (state.DockStageT > DockAlignMaxSeconds && !stalled)
					{
						logger.InfoRare(60, "[AutoSeek] DOCK align: over time but still converging "
							+ "(calErr {0:F4}m, angle {1:F1}deg) - extending rather than restarting",
							calErr, aimErr);
					}
					else if (state.DockStageT > DockAlignMaxSeconds)
					{
						// Do not spend an attempt on a target that is running away. Attempts are
						// the budget for OUR errors; a station moving faster than the approach
						// closes is a different problem, and burning all four on it ends the seek
						// for a reason that has nothing to do with placement.
						float holeSpeed = HoleVelocity().magnitude;
						// AIMED WELL BUT NOT PERFECTLY STATIONED -> PRESS ANYWAY.
						//
						// Retreating exists to fix BAD GEOMETRY. When the shaft is already within
						// the angular gate and close to the line, withdrawing 3 cm to re-approach
						// discards a correct pose and re-runs the negotiation for nothing — that is
						// the jarring back-and-forth, and it kept happening on approaches that were
						// good enough to enter (and did enter, once allowed to press).
						//
						// The 3 mm calibration gate remains the standard for a CLEAN dock; this is
						// the fallback after the stage has had its full time, and it still demands
						// real alignment — it just stops treating "not perfect" as "start over".
						// POSITION IS THE GATE THAT MATTERS; ANGLE GETS THE ORIGINAL TOLERANCE.
						//
						// Observed parked dead on station — calErr 0.0007 m against a 3 mm gate,
						// lateral 0.0005 m — and refusing to press because the shaft sat at 6.81
						// degrees against a 6 degree gate. Stable to two decimals across seconds, so
						// that is the shaft's achievable straightness in this pose, not a
						// convergence still in progress: waiting longer cannot help, and all four
						// attempts burned on 0.8 of a degree.
						//
						// 6 degrees was my invention. The feature's own long-standing entry gate is
						// CollinearEnterDeg (12), and with the tip ON the axis to half a millimetre,
						// a 7 degree shaft is a few millimetres of drift across the remaining
						// centimetre — well inside an entrance. The tight pair still defines a CLEAN
						// dock above; this fallback keeps the strict POSITION requirement and
						// relaxes only the angle, which is the term that was over-specified.
						if (aimErr <= Mathf.Max(AutoSeekTuning.CollinearEnterDeg, angleGate)
							&& lateral <= DockLooseLateral)
						{
							state.CommitBendRef = BendNow();
							EnterDockStage(DockStage.Commit, string.Format(
								"good enough after {0:F1}s (angle {1:F1}deg vs clean gate {2:F0}, "
								+ "lateral {3:F4}m, calErr {4:F4}m) - pressing rather than "
								+ "re-approaching", DockAlignMaxSeconds, aimErr, DockTightDeg,
								lateral, calErr));
							break;
						}
						if (holeSpeed > HoleSpeedStationary)
						{
							logger.InfoRare(30, "[AutoSeek] DOCK align: target is MOVING at "
								+ "{0:F3}m/s (calErr {1:F4}m) - waiting for it to settle rather "
								+ "than spending an attempt", holeSpeed, calErr);
							state.DockStageT = DockAlignMaxSeconds * 0.5f;   // extend, do not fail
						}
						else
						{
							FailDockAttempt(string.Format(
								"never reached the calibration point in {0:F1}s (off by {1:F4}m, "
								+ "angle {2:F1}deg, lateral {3:F4}m, hole {4:F3}m/s)",
								DockAlignMaxSeconds, calErr, aimErr, lateral, holeSpeed));
						}
					}
					else
					{
						// HOLE SPEED IS PART OF THE VERDICT, NOT CONTEXT.
						//
						// The calibration point is anchored to Hole.position and Hole.forward, so
						// it travels with her animation. If the station is moving faster than the
						// approach can converge, "never reached it" says nothing about the
						// controller — it is a statement about the target, and tuning gains against
						// it is chasing a number that was never ours to close.
						// range AND contacts alongside calErr. The residual is almost purely AXIAL
						// (miss z = 0.031 with lateral 0.005), i.e. the tip is at the right place on
						// the line but the wrong distance along it — sitting at the entrance,
						// unable to back off to the standoff. The question that separates the two
						// possible causes is whether the MEASURED TIP moves when the body does:
						// PeneClosestPointTo returns the collider point nearest the hole, which
						// saturates at the surface on contact, so the tip would stop tracking the
						// pelvis entirely and calErr could never close no matter how far we retreat.
						// axisY and shaftY make the tilt sign READABLE instead of arguable: the
						// outward axis must point slightly DOWN (negative y) for the shaft, which
						// runs opposite it, to present nose-up.
						Vector3 shaftNow = PeneClosestPointTo(state.Hole)
							- ((IPene)base.Session.Player.Character.pene).parteBase.position;
						logger.InfoRare(45, "[AutoSeek] DOCK align: calErr={0:F4}m (need {1:F3}) "
							+ "angle={2:F1}deg lateral={3:F4}m range={4:F4}m contacts={5} "
							+ "hold={6:F2}s holeSpeed={7:F4}m/s axisY={8:F3} shaftY={9:F3}",
							calErr, DockCalTolerance, aimErr, lateral, rangeToHole,
							HoleContactCount(), state.DockAlignHoldT, HoleVelocity().magnitude,
							DockAxisOut().y, shaftNow.normalized.y);
					}
					break;
				}

				case DockStage.Commit:
				{
					// CONTACT BY LOAD, NOT ONLY BY GEOMETRY.
					//
					// The hit counter is authoritative when it fires — it is the same signal
					// Penetraciones.AceptaPenetracion uses — but it says nothing while it reads
					// zero, and "hovering a millimetre short" and "pressing on her without the
					// counter agreeing" look identical from position alone. Compression tells them
					// apart: the shaft foreshortens the moment it bears against anything.
					//
					// Measured against a BASELINE taken at the calibration point rather than an
					// absolute threshold, because the shaft already bows from pose and gravity, so
					// an absolute figure would mean something different in every position. The
					// baseline is captured where we know the tip is free and on the line, which
					// makes any RISE from it load we just applied.
					// KEEP THE BASELINE FRESH WHILE IT CAN STILL BE TRUSTED.
					//
					// Bend changes moment to moment with pose, gravity and her motion, so a value
					// captured once at the calibration point is stale within a fraction of a second
					// — and a stale baseline reads pose drift as contact, or hides real contact
					// under a baseline that has drifted up with it.
					//
					// While the tip is further out than contact is physically possible, any bend is
					// by definition NOT us pressing on the hole, so it belongs in the baseline:
					// track it. Once inside that range the reading becomes evidence and the
					// baseline freezes, because from here a rise is the thing we are trying to
					// detect. Free-space drift is absorbed continuously; contact is never
					// absorbed.
					if (rangeToHole >= CommitPressRange)
					{
						state.CommitBendRef = Mathf.Lerp(state.CommitBendRef, BendNow(),
							Mathf.Clamp01(Time.deltaTime / CommitBendRefTau));
					}
					float bendRise = BendNow() - state.CommitBendRef;
					bool pressing = bendRise > CommitBendRise && rangeToHole < CommitPressRange;

					if (bendRise > CommitBendAbort)
					{
						// Bearing hard without the hole accepting it: that is the shaft bowing
						// against her, not entering. Stop before it deforms further — this is the
						// gentleness half of "press in gently".
						FailDockAttempt(string.Format(
							"pressing too hard (bend +{0:F3} over baseline, range {1:F4}m) - "
							+ "backing off rather than bowing the shaft", bendRise, rangeToHole));
						break;
					}

					if (touching || pressing)
					{
						// KEEP HOLDING. DO NOT HAND OVER ON CONTACT.
						//
						// This used to drop to DockStage.None the instant it touched, handing the
						// last millimetres to the original final-stage path. That path does not
						// hold — its own phase machine flapped Touch/Negotiate/Hold and then
						// RETREATED to 2 cm, at which point this cycle re-entered and docked again.
						// Roughly once a second, forever. The dock was never failing; the handover
						// was throwing it away:
						//
						//     DOCK: contact after 0.50s (angle 1.5deg, lateral 0.0019m)
						//     Negotiate -> Touch -> Hold -> Transit   range 0.0045 -> 0.0217
						//
						// Entry is the GAME's decision (peneTryingEnterInHole ->
						// AceptaPenetracion, on its own retry cooldown). Our job once touching is
						// to stay put and keep gentle pressure while that runs — so hold station
						// and only leave when penetration has actually happened. The bend abort
						// above still bounds the force, so holding cannot become grinding.
						if (!state.ContactSeen)
						{
							logger.Info("[AutoSeek] DOCK: contact after {0:F2}s (angle {1:F1}deg, "
								+ "lateral {2:F4}m, attempt {3}) - holding for the game's entry "
								+ "negotiation", state.DockStageT, aimErr, lateral,
								state.DockAttempts + 1);
							state.ContactSeen = true;
						}
						if (base.Session.Player.Character.pene.isPenetrating)
						{
							logger.Info("[AutoSeek] DOCK: ENTERED after {0:F2}s of contact",
								state.DockStageT);
							state.DockStage = DockStage.None;
							state.FinalStage = true;
						}
						else
						{
							// Do not let the press-hold deadline expire while we are demonstrably
							// in contact and simply waiting on the game's cooldown.
							state.DockStageT = Mathf.Min(state.DockStageT,
								DockPressHoldSeconds * 0.5f);
						}
						break;
					}
					// THE DEADLINE. Sized to the distance being covered, not picked: at
					// DockCommitSpeed the standoff takes DockStandoff/DockCommitSpeed seconds, and
					// the grace factor covers plant lag. Past that, the tip is demonstrably not
					// going where it was aimed.
					// PRESS AND HOLD — do not retreat on a stopwatch.
					//
					// This used a 0.75 s deadline sized to the travel distance, which was right for
					// "am I still moving toward it" and wrong for what actually happens at the end:
					// the game NEGOTIATES entry (peneTryingEnterInHole -> AceptaPenetracion, on its
					// own retry cooldown), and that takes as long as it takes. Retreating the moment
					// the timer expired turned the endgame into repeated lunge-and-withdraw — the
					// jarring back-and-forth — and each withdrawal threw away a correct position and
					// restarted the negotiation from scratch.
					//
					// So hold station just past the surface and let the handshake run. The bend
					// abort above is what keeps this honest: pressure is bounded, so holding cannot
					// become grinding. Only a long silence — no contact, no load, nothing happening
					// — counts as a real failure worth re-solving from the standoff.
					if (state.DockStageT > DockPressHoldSeconds)
					{
						FailDockAttempt(string.Format(
							"held at the entrance for {0:F1}s with no entry (range {1:F4}m, "
							+ "bend +{2:F3} over baseline)",
							DockPressHoldSeconds, rangeToHole, bendRise));
					}
					else
					{
						logger.InfoRare(20, "[AutoSeek] DOCK commit: range={0:F4}m contacts={1} "
							+ "bend={2:F3} (+{3:F3} over baseline) t={4:F2}s",
							rangeToHole, HoleContactCount(), BendNow(), bendRise, state.DockStageT);
					}
					break;
				}
			}
		}

		private void EnterDockStage(DockStage next, string why)
		{
			logger.Info("[AutoSeek] DOCK {0} -> {1}: {2}", state.DockStage, next, why);
			state.DockStage = next;
			state.DockStageT = 0f;
			state.DockAlignHoldT = 0f;
			state.AlignScorePrimed = false;   // progress is measured per stage, not per sequence
			state.AlignLastImproveT = 0f;
			// NO ResetSmoothing() HERE. Zeroing the motion velocity at a stage boundary makes the
			// character stop dead and then accelerate again — a visible hitch at every transition,
			// several per approach. The stages share one continuous trajectory now: the setpoint
			// slews between stations, so the velocity that carried us into a stage is exactly the
			// velocity that should carry us out of it. Discarding it was fighting the slew.
		}

		/// <summary>
		/// A dock attempt failed. LEARN FROM THE MISS, then retreat and retry.
		///
		/// Repeating an identical attempt after a failure is the definition of a stuck loop, and
		/// the miss itself says which way to correct: the perpendicular offset of the tip from the
		/// hole's axis IS the error, as a vector. Feeding a fraction of it back as an aim bias
		/// makes the next attempt aim off-centre by exactly the amount the last one missed — if the
		/// tip sits high, the next approach aims low. Bounded, because an unbounded learned offset
		/// is just a slow runaway, and reset whenever a dock succeeds.
		/// </summary>
		private void FailDockAttempt(string why)
		{
			state.DockAttempts++;
			// BIAS LEARNING DISABLED — it was correcting for the wrong cause.
			//
			// It assumes a timeout means a systematic AIM error, and answers by moving the
			// calibration point in the direction of the miss. But the misses were not aim: the tip
			// was travelling toward the right point and simply not arriving, because the vertical
			// axis was throttled to micrometres per frame by the anti-windup debt. Feeding that
			// residual back moved the target FURTHER from where the tip could reach — bias.y grew
			// to 0.0399, pinned at its 0.04 cap, adding four centimetres of unreachable height to
			// every attempt.
			//
			// A learned offset is only meaningful once the axis can actually satisfy the gate.
			// Re-enable it if a systematic miss survives with a healthy vertical; do not use it to
			// paper over an actuator that is not moving.
			state.DockBias = Vector3.zero;
			// LEARN FROM WHERE THE TIP ACTUALLY IS, not from an angle.
			//
			// The residual is the vector from the tip to the point it was supposed to occupy. If
			// the tip keeps parking a few cm low, that residual points UP, and adding it to the
			// bias makes the next attempt aim correspondingly higher. A systematic offset is
			// exactly the error an iterative correction removes, and it is invisible to any angular
			// measure — which is why the previous version, learning from the perpendicular offset,
			// could not fix a miss that was already parallel.
			Vector3 residual = CalibrationPoint() - PeneClosestPointTo(state.Hole);
			Vector3 miss = residual;   // reported, not applied — see the note above
			logger.Info("[AutoSeek] DOCK FAILED (attempt {0}/{1}): {2}. miss=({3:F4},{4:F4},{5:F4}) "
				+ "-> aim bias now ({6:F4},{7:F4},{8:F4})",
				state.DockAttempts, MaxDockAttempts, why, miss.x, miss.y, miss.z,
				state.DockBias.x, state.DockBias.y, state.DockBias.z);

			if (state.DockAttempts >= MaxDockAttempts)
			{
				logger.Info("[AutoSeek] giving up after {0} dock attempts", state.DockAttempts);
				// Retry, NOT UnreachableTarget. Per the enum's own note, UnreachableTarget is a
				// GEOMETRY VERDICT that disarms the loop — the right answer for an angle the hips
				// can never reach, and the wrong one for a dock that simply did not land, which a
				// fresh approach routinely fixes. Using it here ended the whole seek outright: the
				// feature appeared to cut out for no reason.
				state.ExitReason = ExitReason.Retry;
				state.DockStage = DockStage.None;
				return;
			}
			EnterDockStage(DockStage.Retreat, "backing off to re-solve");
		}

		/// <summary>Deadline for the commit: the time it takes to cover the standoff at the commit
		/// speed, plus grace for plant lag.</summary>
		private float DockCommitSeconds()
		{
			return DockStandoff / Mathf.Max(0.01f, DockCommitSpeed) * DockCommitGrace;
		}

		/// <summary>
		/// The calibration point: a fixed station ON the hole's axis, one standoff out, shifted by
		/// whatever bias previous attempts have taught us. The tip must occupy this to within
		/// DockCalTolerance before any dock is attempted — see the ALIGN stage.
		/// </summary>
		/// <summary>Dead zone in force right now — tight while docking, coarse otherwise.</summary>
		private float MovePrecision()
		{
			return (state != null && state.DockStage != DockStage.None)
				? DockMovePrecision : TRANSLATION_PRECISSION;
		}

		/// <summary>
		/// Where COMMIT presses to: a little PAST the entrance, along the same axis.
		///
		/// The previous target was 12 mm in, driven from a 0.75 s deadline that retreated the
		/// moment it expired — so the endgame was a repeated lunge-and-withdraw against her, which
		/// is both the jarring motion on screen and a worse way to enter than simply arriving and
		/// waiting. Penetration is NEGOTIATED by the game (peneTryingEnterInHole ->
		/// AceptaPenetracion, paced by its own cooldown); it is not something a deeper push
		/// achieves. So press just past the surface, keep gentle contact, and hold while that
		/// handshake runs.
		///
		/// Boca keeps its outward offset — its target is the lip surface, not a depth inside.
		/// </summary>
		/// <summary>
		/// Move the tip toward the dock setpoint along a STRAIGHT LINE, at one smoothed speed.
		///
		/// This is the straight-line-stroke idea from ALIGNMENT_CAPABILITY_MAP §12.2 applied to
		/// placement: express the move as a single magnitude in the direction we want to travel,
		/// then split THAT across the actuators, rather than giving each actuator its own error and
		/// its own controller.
		///
		/// Why it matters here. Independent controllers converge at different rates — different
		/// tau, different speed cap, different acceleration limit — so the direction of travel
		/// changes continuously as one axis outruns another, and changes ABRUPTLY when one of them
		/// completes or saturates. That is the arc into the hole and the teleporting hitch, and no
		/// amount of retuning the individual gains removes it, because the problem is that there
		/// are three of them.
		///
		/// With one magnitude, every axis is a fixed fraction of the same number: they start
		/// together, finish together, and the direction is constant for the whole move.
		///
		/// Returns false if there is nothing to do, so the per-axis paths can take over.
		/// </summary>
		private bool DockUnifiedMove(Vector3 dpLocal)
		{
			float mag = dpLocal.magnitude;
			if (mag <= DockMovePrecision)
			{
				smoothDock.Reset();
				return true;   // on station; still OURS, so the per-axis paths stay down
			}

			Vector3 dir = dpLocal / mag;
			// ITS OWN TIME CONSTANT, NOT ApproachTau.
			//
			// ApproachTau is 0.35 s, which on a 3 cm move yields ~0.1 m/s and about a second to
			// converge — the whole move is tau-limited and never reaches its speed cap. The old
			// per-axis controllers each had shorter effective constants, so folding them into one
			// made placement markedly SLOWER, and retreat and align began running out their
			// windows and burning dock attempts. Smoothness came from bounding ACCELERATION; it
			// never required being this sluggish.
			// BOCA IS A MOVING TARGET AND NEEDS A GENTLER LOOP.
			//
			// The hips are nearly stationary during placement; the head is not — measured at
			// 0.03 m/s and swinging, roughly an order of magnitude more than the vag entrance. A
			// loop tuned for a still target chases every one of those movements at full gain, which
			// is the whipping-around: the controller is not unstable, it is faithfully tracking
			// something that keeps moving. Slower response and a lower ceiling let it follow the
			// AVERAGE position instead of every excursion — the same reasoning behind the existing
			// BocaGainDamping and ShaftDirTauBoca, which the dock cycle was not honouring at all.
			bool boca = TargetIsBoca();
			float tau = boca ? DockUnifiedTau * BocaDockTauScale : DockUnifiedTau;
			float speedScale = Mathf.Max(0.25f, AutoSeekTuning.SpeedScale)
				* (boca ? BocaGainDamping : 1f);
			float step = smoothDock.Step(mag, Time.deltaTime, tau,
				DockUnifiedMaxSpeed * speedScale, DockUnifiedAccel * speedScale);
			Vector3 delta = dir * step;

			// Vertical goes to the pelvis, horizontal to the avatar — the same division of labour
			// as before, but both scaled from the SAME step, so their ratio is exactly the
			// direction we intend to travel.
			if (Mathf.Abs(delta.y) > 1E-06f && ctl != null)
			{
				ctl.AddVerticalDelta(delta.y);
			}
			Vector3 flat = new Vector3(delta.x, 0f, delta.z);
			if (flat.sqrMagnitude > 1E-12f)
			{
				base.Session.Player.Move(flat);
			}
			ballisticClean = false;

			// THE BOCA TAP — APPLIED DIRECTLY, NOT THROUGH THE SMOOTHER.
			//
			// Expressing the tap as a moving setpoint failed for a mechanical reason: a first-order
			// smoother attenuates any oscillation whose period approaches its time constant, so the
			// commanded excursion arrived at the tip shrunken and late, and the result was an
			// approach that spent all day ALMOST touching her lips.
			//
			// Moving the avatar directly sidesteps that entirely. This is a VELOCITY — cos, the
			// derivative of the sine position — so it integrates to zero displacement over each
			// cycle: it oscillates about wherever the station has put us rather than walking the
			// character anywhere. Horizontal only, so it cannot fight the vertical hold that keeps
			// the tip level with her lips.
			if (TargetIsBoca() && state.DockStage == DockStage.Commit)
			{
				// ROOT-LOCAL, NOT WORLD. Player.Move translates relative to the ActorController's
				// transform, so a world-space direction gets re-interpreted through the root's
				// rotation — which turned "toward her mouth" into a sideways shuffle. The unified
				// move above is correct only because dpLocal was already root-local; this vector
				// comes from the hole in world space and has to be converted.
				Vector3 inAxis = state.RootTransform.InverseTransformDirection(-DockAxisOut());
				Vector3 tapDir = new Vector3(inAxis.x, 0f, inAxis.z);
				if (tapDir.sqrMagnitude > 1E-06f)
				{
					float tapPhase = state.DockStageT * BocaTapHz * 2f * Mathf.PI;
					base.Session.Player.Move(tapDir.normalized
						* (Mathf.Cos(tapPhase) * BocaTapSpeed * Time.deltaTime));
				}
			}

			logger.InfoRare(30, "[AutoSeek/dock-move] stage={0} |dp|={1:F4} step={2:F5} "
				+ "dir=({3:F2},{4:F2},{5:F2})", state.DockStage, mag, step, dir.x, dir.y, dir.z);
			return true;
		}

		private Vector3 DockPressPoint()
		{
			float depth = TargetIsBoca() ? AutoSeekTuning.BocaTargetOut : -DockPressPast;
			return state.Hole.position + DockAxisOut() * depth + state.DockBias;
		}

		private Vector3 CalibrationPoint()
		{
			return state.Hole.position + DockAxisOut() * DockStandoff + state.DockBias;
		}

		/// <summary>
		/// The hole's outward axis, AS MEASURED BY THE THING THAT WORKS.
		///
		/// The dock cycle first used HoleOutDirection() here, and read the shaft as 124 degrees off
		/// axis on the same frame the collinearity check — using -Hole.forward — read 4.6. Those are
		/// not a 13-degree disagreement, they are different directions entirely: a calibration point
		/// placed along worldOutHoleDirection is not on the hole's axis at all, so the tip could
		/// never reach it and the cycle failed all four attempts without converging.
		///
		/// Every gate and every waypoint must come from ONE axis, and it must be the one the
		/// working measurements already use. UseOutHoleAxis still switches the experiment so the
		/// question stays answerable live, but it is off by default.
		/// </summary>
		private Vector3 DockAxisOut()
		{
			Vector3 axis = state.Hole.forward.normalized;
			if (AutoSeekTuning.UseOutHoleAxis)
			{
				Vector3 d = HoleOutDirection();
				if (d.sqrMagnitude > 1E-06f) axis = d.normalized;
			}
			return TargetIsBoca() ? LevelBiased(axis) : axis;
		}

		/// <summary>
		/// Pull an axis toward horizontal — a PREFERENCE the head has to overcome, not a limit.
		///
		/// The boca's entrance direction follows the head, which tilts constantly, and every degree
		/// of that tilt was being tracked as a change in the target line. That is most of why the
		/// boca approach whips around while the hips-based holes are steady: the line itself will
		/// not sit still.
		///
		/// Level is the right default. A mouth being approached is usually met roughly horizontally,
		/// so small head tilts should be IGNORED rather than chased — but a genuinely turned or
		/// raised head is real intent and must be honoured, or the seeker would aim at a place the
		/// mouth is not.
		///
		/// So: below <see cref="BocaLevelFreeDeg"/> of tilt the axis is flattened to horizontal
		/// outright; above <see cref="BocaLevelFullDeg"/> it is taken as given; between the two it
		/// blends. Small tilts cost nothing, large ones win — the head has to convince us.
		/// </summary>
		private static Vector3 LevelBiased(Vector3 axis)
		{
			Vector3 flat = new Vector3(axis.x, 0f, axis.z);
			if (flat.sqrMagnitude < 1E-06f)
			{
				return axis;   // straight up or down; there is no horizontal version to prefer
			}
			flat = flat.normalized;
			// Elevation above the horizontal plane, degrees, regardless of sign.
			float tiltDeg = Mathf.Abs(90f - Vector3.Angle(axis, Vector3.up));
			float respect = Mathf.InverseLerp(BocaLevelFreeDeg, BocaLevelFullDeg, tiltDeg);
			Vector3 biased = Vector3.Slerp(flat, axis, respect).normalized;

			// A FEW DEGREES NOSE-UP, DELIBERATELY.
			//
			// Her head turns in response to the PLAYER'S HIP ANGLE, so the approach direction is
			// not merely an aiming choice — it is an input to where the target ends up. Presenting
			// slightly upward is what produces the head pose that accepts the approach; dead level
			// is aiming at a mouth that has not turned to meet it. This is the one place where the
			// controller is steering the target rather than tracking it.
			Vector3 right = Vector3.Cross(Vector3.up, biased);
			if (right.sqrMagnitude < 1E-06f)
			{
				return biased;
			}
			// SIGN VERIFIED IN GAME: this produced 5 degrees NOSE-DOWN with the opposite sign. The
			// reasoning that led there ("the axis points outward, so tilt it down") was right about
			// the inversion and wrong about which way AngleAxis turns it about this particular
			// right vector — the kind of thing that is faster to observe than to derive.
			return (Quaternion.AngleAxis(-AutoSeekTuning.BocaUpTilt, right.normalized)
				* biased).normalized;
		}

		private Vector3 LateralOffsetFromAxis(Transform hole)
		{
			try
			{
				if (hole == null) return Vector3.zero;
				// WHICH AXIS IS THE HOLE'S AXIS — live-switchable because it is an open question,
				// not a settled one, and the two answers differ by ~13 degrees.
				//
				//   false (default): -hole.forward, the bone axis. The long-standing behaviour.
				//   true:            chain.worldOutHoleDirection, what the game itself uses and
				//                    what the collinearity readout and the cyan line are drawn
				//                    from — so `true` makes the measurement and the picture agree.
				//
				// The argument for `true` is that a perpendicular measured against a TILTED line
				// has a residual that never reaches zero, so the controller chases a phantom
				// offset. The argument for `false` is that it is the configuration the seeker was
				// last known to work in. Switching it blind broke placement from every position,
				// which is precisely why it is a flag now: flip it live and watch, rather than
				// rebuilding to ask the question.
				//
				//     curl -X POST -d '' ".../set?path=T:AutoSeekTuning.UseOutHoleAxis&value=true"
				Vector3 axis = AutoSeekTuning.UseOutHoleAxis ? HoleOutDirection() : -hole.forward;
				if (axis.sqrMagnitude < 1E-06f) axis = -hole.forward;
				axis = axis.normalized;
				Vector3 toTip = PeneClosestPointTo(hole) - hole.position;
				// Strip the along-axis component; the remainder is the perpendicular miss.
				return toTip - axis * Vector3.Dot(toTip, axis);
			}
			catch
			{
				return Vector3.zero;
			}
		}

		/// <summary>
		/// Collinearity tolerance as a function of how far out we are.
		///
		/// A fixed tolerance is wrong at both ends: far away it demands precision that does not
		/// matter yet and stalls the approach, and at the entrance it permits slop that guarantees
		/// a miss. Scaling with distance makes the requirement a funnel — wide where the tip
		/// enters it, converging to the entrance — so there is always a path inward rather than a
		/// wall to stall against.
		/// </summary>
		private float ProgressiveLineTol(float axialDist)
		{
			float len = 0.2f;
			try { len = Mathf.Max(0.05f, base.Session.Player.Character.pene.worldLength); }
			catch { }
			// At a pene length out: generous. At the entrance: tight.
			float t = Mathf.Clamp01(axialDist / len);
			float near = TargetIsBoca() ? CollinearLineTol * BocaAngleTightening : CollinearLineTol;
			float far = TargetIsBoca() ? CollinearLineFar * BocaAngleTightening : CollinearLineFar;
			return Mathf.Lerp(near, far, t);
		}

		/// <summary>Lateral tolerance at a pene length out, where being off-line is easily fixed
		/// by continuing to approach.</summary>
		private const float CollinearLineFar = 0.05f;

		/// <summary>How strongly translation works to cancel the off-axis offset. Modest: it acts
		/// every frame alongside the approach, so it does not need to be aggressive.</summary>
		private const float LateralCorrectGain = 0.6f;

		/// <summary>Lateral gain while in contact — HIGHER than the approach gain. Once touching,
		/// staying on the axis is the whole job, and the target may be sweeping through an arc as
		/// she turns; tracking that needs more authority than a leisurely approach does.</summary>
		private const float LateralTrackGain = 1.1f;

		/// <summary>Lateral offset treated as on-line. Below this the entrance geometry itself
		/// guides the tip in.</summary>
		private const float CollinearLineTol = 0.008f;

		/// <summary>Lateral offset at which forward motion is fully throttled.</summary>
		private const float CollinearLineAbort = 0.05f;

		/// <summary>
		/// Number of live contacts between the pene and the target hole's parts — lips, teeth and
		/// tongue for the boca; the equivalent surfaces elsewhere.
		///
		/// This is the honest answer to "is it touching", which distance can only ever approximate.
		/// It also separates the two failures that look identical from geometry alone: contacts
		/// with no penetration means we are pressing on something that is not letting us in (back
		/// off and re-aim), while zero contacts means we simply have not arrived yet (keep going).
		/// </summary>
		private int HoleContactCount()
		{
			try
			{
				BoneStretchedChain chain = ChainForCurrentHole();
				if (chain == null) return 0;
				return chain.penetraciones.currentHits.cantidadRealDeHitsContraPartes;
			}
			catch
			{
				// Unknown: report NO contact so the approach keeps moving. Reporting contact would
				// stall it against a signal we do not actually have.
				return 0;
			}
		}

		/// <summary>The BoneStretchedChain behind the entrance transform the seeker is targeting.
		/// Matched by entrance position, since state.Hole is an IK proxy that mirrors entrada
		/// rather than being it.</summary>
		private BoneStretchedChain ChainForCurrentHole()
		{
			try
			{
				if (state == null || state.Hole == null) return null;
				FemaleChar ch = base.Session.Guest.Impl;
				var candidates = new[] { ch.vagHole, ch.anusHole, ch.bocaHole };
				foreach (BoneStretchedChain c in candidates)
				{
					if (c == null || c.entrada == null) continue;
					Transform proxy = base.Session.Guest.Puppet.GetIKBoneTransform(c.entrada);
					if (proxy != null
						&& (proxy.position - state.Hole.position).sqrMagnitude < 0.0004f)
					{
						return c;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		/// <summary>
		/// True when the seeker is targeting the mouth. Boca wants tighter tolerances than the
		/// pelvis holes for two reasons that both push the same way: its entrance transform is
		/// Labios.Closing_Entrada — the LIPS — which is a small feature compared with the vaginal
		/// or anal entrance, and it rides on the head, which moves ~40x more. A tolerance that is
		/// comfortable on a pelvis hole is most of the target's width here.
		/// </summary>
		private bool TargetIsBoca()
		{
			try
			{
				if (state == null || state.Hole == null) return false;
				FemaleChar ch = base.Session.Guest.Impl;
				Transform boca = base.Session.Guest.Puppet.GetIKBoneTransform(ch.bocaHole.entrada);
				return boca != null
					&& (boca.position - state.Hole.position).sqrMagnitude < 0.0004f;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>Collinearity angle tolerance for the current target — tighter for the lips.</summary>
		/// <summary>
		/// Angular gain scale for the shaft acting as a LEVER: tip travel is roughly length x angle,
		/// so the same correction that is gentle on a short pene flings a long one off target. This
		/// is the opposite end of the length question from the pitch MAP — length does not change
		/// degrees-per-hip-z, but it entirely governs how far the TIP moves for those degrees.
		///
		/// Referenced to 0.2 m, so a 0.4 m shaft gets half the angular authority and its tip moves
		/// about the same distance per correction as a short one would.
		/// </summary>
		private float LeverScale()
		{
			try
			{
				float len = base.Session.Player.Character.pene.worldLength;
				if (len > 0.01f) return Mathf.Clamp(0.2f / len, 0.25f, 1.5f);
			}
			catch
			{
			}
			return 1f;
		}

		private float EnterDegFor()
		{
			float baseDeg = Mathf.Max(2f, AutoSeekTuning.CollinearEnterDeg);
			return TargetIsBoca() ? baseDeg * BocaAngleTightening : baseDeg;
		}

		/// <summary>Boca demands this fraction of the normal angular and lateral slack.</summary>
		/// <summary>
		/// Angular and lateral gains are MULTIPLIED by this for the boca. The head sweeps ~40x more
		/// than a pelvis hole, so a gain that is calm against the vag is a whip against the mouth:
		/// the controller chases a target moving faster than it can settle, and every correction
		/// arrives after the target has moved again. Halving the tolerance WITHOUT halving the gain
		/// was the error - it demanded more precision and gave the loop more authority to hunt for
		/// it. Track the mean, not every twitch; the velocity feed-forward covers the sweep.
		/// </summary>
		private const float BocaGainDamping = 0.35f;

		private const float BocaAngleTightening = 0.5f;

		/// <summary>Outward direction of the targeted hole, from the game's own
		/// worldOutHoleDirection where available.</summary>
		// ── TARGET VELOCITY FEED-FORWARD ─────────────────────────────────────────────────────
		//
		// Chasing a moving target by POSITION alone guarantees a permanent lag: by the time the
		// correction lands, she has moved again. On the boca that is the whole problem — the head
		// never stops, so a position-only controller is always aiming where the lips WERE.
		//
		// Feeding her measured velocity forward means aiming where the lips are GOING. Smoothed
		// hard, because differentiating a noisy position gives a very noisy velocity, and a jittery
		// feed-forward is worse than none.
		private Vector3 holeVel;

		private Vector3 holeLastPos;

		private bool holeVelPrimed;

		private Vector3 HoleVelocity()
		{
			try
			{
				if (state == null || state.Hole == null) return Vector3.zero;
				Vector3 p = state.Hole.position;
				float dt = Time.deltaTime;
				if (!holeVelPrimed || dt <= 0f) { holeLastPos = p; holeVelPrimed = true; return Vector3.zero; }
				Vector3 inst = (p - holeLastPos) / dt;
				holeLastPos = p;
				holeVel = Vector3.Lerp(holeVel, inst, Mathf.Clamp01(dt / HoleVelTau));
				// Clamp: a teleport or a pose swap would otherwise inject an enormous phantom
				// velocity and fling the body across the room.
				return Vector3.ClampMagnitude(holeVel, HoleVelMax);
			}
			catch
			{
				return Vector3.zero;
			}
		}

		/// <summary>Velocity EMA constant. Slow: a noisy derivative is worse than none.</summary>
		private const float HoleVelTau = 0.18f;

		/// <summary>Ceiling on the tracked velocity, m/s — guards against pose swaps.</summary>
		private const float HoleVelMax = 1.2f;

		/// <summary>How far ahead to lead, seconds. Roughly the loop's own reaction time: lead by
		/// less and the lag remains, lead by more and it overshoots ahead of her.</summary>
		private const float HoleVelLead = 0.12f;

		private Vector3 HoleOutDirection()
		{
			try
			{
				BoneStretchedChain chain = ChainForCurrentHole();
				if (chain != null)
				{
					Vector3 d = chain.worldOutHoleDirection;
					if (d.sqrMagnitude > 1E-06f) return d.normalized;
				}
			}
			catch
			{
			}
			return state != null && state.Hole != null
				? state.Hole.forward.normalized : Vector3.down;
		}

		private float PressInFor(Transform hole)
		{
			try
			{
				FemaleChar ch = base.Session.Guest.Impl;
				Transform boca = base.Session.Guest.Puppet.GetIKBoneTransform(ch.bocaHole.entrada);
				if (boca != null && hole != null
					&& (boca.position - hole.position).sqrMagnitude < 0.0004f)
				{
					// BOCA AIMS OUTWARD, NOT INWARD.
					//
					// Labios.Closing_Entrada sits INSIDE the lips, so aiming at it — let alone
					// pressing further past it, which the old -BocaPressIn did — asks the tip to
					// occupy a point behind a surface it has not passed yet. The approach then
					// spends its time driving at somewhere unreachable, which is why the mouth
					// "struggled" while the pelvis holes did not: those entrances sit much closer
					// to the surface the tip actually meets.
					//
					// Positive = out along the axis, onto the lip surface, which is where contact
					// belongs and where the negotiation should begin.
					return AutoSeekTuning.BocaTargetOut;
				}
			}
			catch
			{
			}
			return -DefaultPressIn;
		}

		/// <summary>Intrusion past the entrance for vag/anus — soft tissue that gives.</summary>
		private const float DefaultPressIn = 0.012f;

		/// <summary>Intrusion for the boca. Smaller: the target is on a moving head.</summary>
		private const float BocaPressIn = 0.005f;

		/// <summary>EMA time constant for the shaft direction. The chain sags and springs; the
		/// controller wants its settled direction, not its current one.</summary>
		private const float ShaftDirTau = 0.25f;

		/// <summary>Shaft-direction smoothing for the boca — much slower. Steering on a jittery
		/// reading against a fast target is what makes it whip.</summary>
		private const float ShaftDirTauBoca = 0.55f;

		private Vector3 shaftDirSmoothed;

		private bool shaftDirPrimed;

		private Vector3 PeneTipPoint()
		{
			return PeneClosestPointTo(null);
		}

		/// <summary>Point on the pene's own collision geometry nearest <paramref name="target"/>
		/// (or nearest the hole when null). This is "the pene mesh closest to the model" —
		/// measured, not derived from bone lengths.</summary>
		private Vector3 PeneClosestPointTo(Transform target)
		{
			IPene pene = base.Session.Player.Character.peneDeCharacter;
			Transform tp = pene.partePunta;
			try
			{
				if (peneColliders == null || peneColliders.Length == 0)
				{
					Transform root = tp;
					while (root.parent != null && root.parent.GetComponent<Penis>() != null)
					{
						root = root.parent;
					}
					peneColliders = tp.root.GetComponentsInChildren<Collider>(includeInactive: false);
				}
				// FURTHEST FROM THE BASE, not nearest the target (owner correction). "Nearest the
				// hole" picks whatever part of the shaft happens to face her — mid-shaft when the
				// approach is oblique — so the seeker docks a body's width short on one hole and
				// fine on another, which is exactly the asymmetry reported. The tip is defined by
				// the pene's OWN geometry: the surface point furthest along the shaft from the
				// base. That is the same point regardless of where the hole is.
				// PROBE ALONG THE SHAFT, NOT AT THE HOLE. Asking each collider for its closest
				// point to the HOLE returns a point on the side facing her — mid-shaft on an
				// oblique approach — and then picking whichever of those lay furthest from the
				// base compounded the error instead of fixing it. That is why this got worse.
				//
				// The tip is a property of the pene alone, so probe from a point far out along
				// the shaft axis: every collider then reports its own FAR end, and the furthest
				// of those from the base is the actual tip of the mesh. The hole is not involved.
				Vector3 basePos = ((IPene)base.Session.Player.Character.pene).parteBase.position;
				Vector3 shaftDir = (tp.position - basePos);
				shaftDir = (shaftDir.sqrMagnitude > 1E-08f) ? shaftDir.normalized : tp.forward;
				Vector3 goal = basePos + shaftDir * 10f;
				Vector3 best = Vector3.zero;
				float bestFromBase = -1f;
				for (int i = 0; i < peneColliders.Length; i++)
				{
					Collider c = peneColliders[i];
					if (c == null || !c.enabled || !c.gameObject.activeInHierarchy) continue;
					// Only colliders belonging to the pene hierarchy, not the whole avatar.
					if (!c.transform.IsChildOf(tp.root) || c.GetComponentInParent<Penis>() == null) continue;
					// Probe each collider from beyond the tip so ClosestPoint returns its
					// far surface rather than its near one, then keep whichever probe lands
					// furthest from the base.
					Vector3 p = c.ClosestPoint(goal);
					float fromBase = (p - basePos).sqrMagnitude;
					if (fromBase > bestFromBase) { bestFromBase = fromBase; best = p; }
				}
				if (bestFromBase >= 0f)
				{
					// Draw the EXACT point being used, as a cross. This is the same value the
					// placement maths consumes — not a separate estimate — so if the seeker docks
					// wrong, the cross shows whether the tip is misidentified or the placement is.
					Tracer.DrawLineOnTop(best - Vector3.up * 0.02f, best + Vector3.up * 0.02f,
						Color.white, 0.2f, 0.006f);
					Tracer.DrawLineOnTop(best - Vector3.right * 0.02f, best + Vector3.right * 0.02f,
						Color.white, 0.2f, 0.006f);
					Tracer.DrawLineOnTop(best - Vector3.forward * 0.02f, best + Vector3.forward * 0.02f,
						Color.white, 0.2f, 0.006f);
					// NO SEATING OFFSET ON A MEASUREMENT.
					//
					// This used to return best + shaftDir * SeekOvershoot, which corrupted the
					// reading itself: the seeker then believed the tip reached 1.2 cm further
					// forward than it does, so dp hit zero while the real tip was still short —
					// and it parked there. That IS the standoff that would not go away.
					//
					// A sensor reports where things ARE. Anything we want to push past the surface
					// belongs on the TARGET, where it reads as intent rather than being baked into
					// the number every other calculation derives from.
					return best;
				}
			}
			catch
			{
			}
			try
			{
				Penis p2 = base.Session.Player.Character.pene;
				if (p2 != null && p2.punta != null && p2.punta.physicBone != null)
				{
					return p2.punta.physicBone.position;
				}
			}
			catch
			{
			}
			return tp.position + tp.forward * base.Session.Player.Character.pene.worldTipPartLength;
		}

		private Transform GetClosestHole()
		{
			FemaleChar ch = base.Session.Guest.Impl;
			IPene pene = base.Session.Player.Character.peneDeCharacter;
			Transform hole = base.Session.Guest.Puppet.GetIKBoneTransform(ch.vagHole.entrada);
			float distance = Vector3.Distance(PeneClosestPointTo(hole), hole.position);
			TestTransform(ch.anusHole.entrada, ref distance, ref hole);
			TestTransform(ch.bocaHole.entrada, ref distance, ref hole);
			return hole;
		}

		private void TestTransform(Transform t, ref float distance, ref Transform hole)
		{
			IPene pene = base.Session.Player.Character.peneDeCharacter;
			t = base.Session.Guest.Puppet.GetIKBoneTransform(t);
			float tmpdistance = Vector3.Distance(PeneClosestPointTo(t), t.position);
			if (tmpdistance < distance)
			{
				distance = tmpdistance;
				hole = t;
			}
		}
	}

	private class AutoplacerState
	{
		public Transform Hole { get; set; }

		public Transform RootTransform { get; set; }

		public Vector3 TranslateInto { get; internal set; }

		public ExitReason ExitReason { get; set; }

		public bool FinalStage { get; internal set; }

		public bool ResetComplete { get; set; }

		/// <summary>Distance pressed forward since the final stage began. Bounded, because an
		/// unbounded "keep pushing until it works" is how the lateral corrector once walked the
		/// player across the room.</summary>
		public float PressAccum { get; internal set; }

		/// <summary>Distance retreated after detecting bow, so the back-off is bounded too — an
		/// unbounded retreat is just the same runaway pointing the other way.</summary>
		public float BackoffAccum { get; internal set; }

		/// <summary>Time spent holding at the threshold while the game's own penetration check
		/// runs. Waiting is the action here, so it needs to be state like any other.</summary>
		public float DwellT { get; internal set; }

		/// <summary>Dock attempts since the approach last completed. Bounds the back-off-and-retry
		/// cycle so it stays a recovery rather than becoming a loop.</summary>
		public int DockAttempts { get; internal set; }

		/// <summary>True once anything has actually touched during this dock. Distinguishes "not
		/// arrived yet" from "was touching and lost it" — two states that look identical from
		/// distance alone and call for opposite responses.</summary>
		public bool ContactSeen { get; internal set; }

		/// <summary>How long contact has been absent since it was last present.</summary>
		public float NoContactT { get; internal set; }

		/// <summary>Distance crept past the computed target while hunting for contact. Bounded, so
		/// a hole that is not where we believe it is fails rather than grinding forward.</summary>
		public float CreepAccum { get; internal set; }

		/// <summary>True while actively retreating to the on-axis standoff after a violation. A
		/// real backoff has to MOVE; clearing a stage flag only re-ran the approach from the very
		/// jammed pose it was meant to escape.</summary>
		public bool Retreating { get; internal set; }

		/// <summary>Current placement phase — the single source of truth for what the seeker is
		/// doing, replacing four booleans that could contradict each other.</summary>
		public SeekPhase Phase { get; internal set; }

		/// <summary>Time spent retreating, so a blocked retreat cannot hang the sequence.</summary>
		public float RetreatT { get; internal set; }

		/// <summary>Time spent in the depth reset, so a reset that cannot converge cannot hang the
		/// whole feature the way it did.</summary>
		public float ResetT { get; internal set; }

		/// <summary>Current stage of the timed dock cycle.</summary>
		public DockStage DockStage { get; internal set; }

		/// <summary>Time in the current dock stage — what the deadlines are measured against.</summary>
		public float DockStageT { get; internal set; }

		/// <summary>How long alignment has held inside the tight gate.</summary>
		public float DockAlignHoldT { get; internal set; }

		/// <summary>Learned aim offset, accumulated from the misses of previous attempts, so a
		/// repeated failure corrects itself rather than repeating identically.</summary>
		public Vector3 DockBias { get; internal set; }

		/// <summary>Shaft compression measured at the calibration point, where the tip is free. The
		/// commit reads load as a RISE from this, so pose- and gravity-induced bow does not read as
		/// contact.</summary>
		public float CommitBendRef { get; internal set; }

		/// <summary>Rate-limited setpoint. The dock stages sit 3.5 cm apart, and stepping between
		/// them makes the character lurch; this travels between them instead.</summary>
		public Vector3 DockTargetSlewed { get; internal set; }

		public bool DockTargetPrimed { get; internal set; }

		/// <summary>Best combined align error seen this stage, and when it was seen. The align
		/// deadline measures lack of progress rather than elapsed time.</summary>
		public float AlignBestScore { get; internal set; }

		public float AlignLastImproveT { get; internal set; }

		public bool AlignScorePrimed { get; internal set; }

		/// <summary>True once the boca presentation angle is set and the hips should stop pitching.
		/// Her head responds to hip pose, so continuing to correct pitch re-moves the target.</summary>
		public bool BocaPitchLocked { get; internal set; }

		/// <summary>Time within the current boca tap-then-back-off attempt cycle.</summary>
		public float BocaAttemptT { get; internal set; }

		/// <summary>Divergence tracking for the measured tip (the white cross). Everything else in
		/// the seeker reacts to how BIG the error is; these react to it getting WORSE, which is the
		/// only signal that separates "slowly converging" from "quietly losing ground".</summary>
		public bool TipDistPrimed { get; internal set; }

		public float TipDistSlow { get; internal set; }

		public float TipRateSlow { get; internal set; }

		public float DivergeT { get; internal set; }

		public float GetRotation2()
		{
			float angle = UnityUtils.ToEuler(Quaternion.LookRotation(-Hole.forward, RootTransform.up)).y;
			float angle2 = UnityUtils.ToEuler(Quaternion.LookRotation(RootTransform.forward, RootTransform.up)).y;
			float targetAngle = angle - angle2;
			float absAngle = Mathf.Abs(targetAngle);
			if (absAngle < 90f && absAngle > 1f)
			{
				return targetAngle;
			}
			return 0f;
		}

		public float GetRotation()
		{
			float angle2 = UnityUtils.FromToAxisAngle(RootTransform.forward, -Hole.forward, RootTransform.forward);
			return UnityUtils.FromToAxisAngle(RootTransform.forward, -Hole.forward, RootTransform.up);
		}
	}

	/// <summary>
	/// Explicit placement phases. Enumerated so the illegal combinations the old interacting
	/// booleans permitted — advancing while retreating, pinned while transiting, dwelling with
	/// nothing touching — cannot be represented at all.
	/// </summary>
	private enum SeekPhase { Transit, Hold, Touch, Negotiate }

	/// <summary>Stages of the timed dock cycle. Explicit state, because deriving the near-field
	/// behaviour from instantaneous conditions is what let it flap between phases forever.</summary>
	private enum DockStage { None, Retreat, Align, Commit }

	private enum ExitReason
	{
		None,
		UnreachableTarget,
		VerticalAngleTooWide,
		Manual,
		Completed,
		NoTool,
		/// <summary>
		/// This attempt failed in a way a FRESH APPROACH can fix — the shaft bowed on contact, or
		/// pressed without entering. Deliberately distinct from UnreachableTarget: that one is a
		/// geometry verdict and must disarm the loop, because retrying it would grind forever at
		/// the same impossible angle. Conflating the two is what made one bad attempt end the
		/// whole session.
		/// </summary>
		Retry
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<KeyboardShortcut> hotkey;

	private ConfigEntry<bool> autothrust;

	// SEEKLAB: always on. The lab binds the SAME config keys as BetterExperience
	// (Features/AutoSeekerEnabled, EnableAutoThrust, EnableMissionControl), which are set false
	// to stop BE registering its copies — and that switched the lab off with it. PluginService
	// skips OnStart for a disabled feature, so no factory was registered and nothing ran at all:
	// no Mission Control, no seek, and a log line saying only "waiting for a guest".
	// The lab's presence IS its enable switch; remove SeekLab.dll to turn it off.
	public override bool Enabled => true;

	public override void Configure(ConfigFile config)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		enableFeature = config.Bind<bool>("Features", "AutoSeekerEnabled", true, "Enable auto seeker: automated hole seeker");
		hotkey = config.Bind<KeyboardShortcut>("AutoSeeker", "Hotkey", new KeyboardShortcut(KeyCode.Space, Array.Empty<KeyCode>()), "Autoplacer: start/stop hotkey");
		autothrust = config.Bind<bool>("AutoSeeker", "EnableAutothrust", true, "Auto seeker: Start auto-thrust sequence when ready");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(autothrust, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(hotkey, base.Scope, PluginOptionsService.SettingsType.player);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new AutoSeekerService
		{
			HotkeyCfg = hotkey,
			Autothrust = autothrust
		});
	}
}
