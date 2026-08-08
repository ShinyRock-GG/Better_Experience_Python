using System;
using Assets._ReusableScripts.CuchiCuchi;
using Assets._ReusableScripts.CuchiCuchi.AI;
using Assets._ReusableScripts.CuchiCuchi.AI.Emociones;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.Controllers;
using Assets._ReusableScripts.CuchiCuchi.Dependentes.FinalIk;
using Assets._ReusableScripts.CuchiCuchi.PhysicsAndBonesScripts;
using BepInEx.Configuration;
// SEEKLAB: implicit while this file lived in BetterExperience.Features — see the note in this
// folder's AutoSeekerFeature.cs. `using BetterExperience` in particular is what makes the
// unqualified `Logger` resolve to BE's logger again instead of BepInEx's unrelated one.
using BetterExperience;
using BetterExperience.Features;
using BetterExperience.Features.Overlay;
using BetterExperience.Features.PluginOptions;
using BetterExperience.GameScopes;
using BetterExperience.Utils;
using HarmonyLib;
using UnityEngine;
// SEEKLAB: UnityEngine also publishes a `Logger`. In BetterExperience.Features the enclosing
// namespace won that contest for free; as two peer using-directives they are merely ambiguous.
using Logger = BetterExperience.Logger;

// SEEKLAB COPY. Byte-identical to BetterExperience.Features/AutoThrustFeature.cs except for the
// changes forced by living in another assembly, each marked "SEEKLAB:".
//
// SEEKLAB: namespace — see the note in this folder's AutoSeekerFeature.cs.
namespace SeekLab;

/// <summary>
/// REMOTE TEST CONTROL — statics only, on purpose.
///
/// The dev probe resolves statics through its T: root with no service lookup, whereas BE's
/// SessionServices live in a ScopeSupport registry the probe's object-graph search cannot reach
/// within its budget. So a static request surface is the difference between "an agent can run
/// this experiment" and "a human has to click a toggle and read a log".
///
/// The pattern is request/ack rather than direct invocation: a setter writes a Request flag, the
/// service picks it up on its own update and clears it, and results are published back here.
/// Nothing off-thread ever touches Unity state, which is the same rule the probe's own pump
/// follows and for the same reason.
///
///     curl -X POST -d '' "http://localhost:8910/set?path=T:BeTestControl.RequestFreeCal&amp;value=true"
///     curl "http://localhost:8910/get?path=T:BeTestControl.FreeCalComplete"
///     curl "http://localhost:8910/get?path=T:BeTestControl.Summary"
/// </summary>
public static class BeTestControl
{
	/// <summary>Set true to start FREE CALIBRATE. Cleared by the service when it begins.</summary>
	public static bool RequestFreeCal;

	/// <summary>Set true to start the stroke audit. Cleared when it begins.</summary>
	public static bool RequestAudit;

	/// <summary>True while a calibration sweep is in progress.</summary>
	public static bool FreeCalRunning;

	/// <summary>Set when a sweep finishes. Clear it before requesting another, or a stale true
	/// reads as "already done" and the next poll returns instantly on the previous run.</summary>
	public static bool FreeCalComplete;

	/// <summary>True once the 2-D pitch surface has at least two measured height levels.</summary>
	public static bool SurfaceReady;

	/// <summary>Per-level measured slopes, deg/unit — the headline numbers, published so a
	/// result can be read directly instead of parsed out of the log.</summary>
	public static float SlopeLevel0, SlopeLevel1, SlopeLevel2;

	public static float LevelY0, LevelY1, LevelY2;

	/// <summary>Conditions the last calibration ran under. A result without its conditions is not
	/// a result — this is what makes two runs comparable.</summary>
	public static float AtScale, AtPeneLength;

	/// <summary>One-line human-readable summary of the last completed run.</summary>
	public static string Summary = "(no run yet)";
}

internal class AutoThrustFeature : PluginFeature
{
	public class AutoThrustService : SessionService
	{
		public class SequenceState
		{
			public float Velocity { get; set; }

			public BoneStretchedChain hole { get; set; }

			public Transform HoleEntrance { get; set; }

			public float MaxPRatio { get; set; }

			public MotionType Motion { get; set; }

			// maximaProfundidadVirtualAlcanzada is [Obsolete("controlado por internals", true)] in
			// SMA 23.1 and this was stubbed to false, so atLimit could never fire. The live
			// successor is maximaProfundidadPhysicsAlcanzada — the game's own
			// "penetratedDepthLocalInternals >= maxProfundidadPhysicsLocal" (BoneStretchedChain.cs:531).
			public bool HoleDepthLimit => hole != null && hole.maximaProfundidadPhysicsAlcanzada;

			public bool HoleDiameterLimit => hole.maximaAnchuraVirtualAlcanzada;

			public int Step { get; set; }

			public float NonDeformedExitPRatio { get; set; }

			public float ExitDeformation { get; set; }

			public int Ticks { get; internal set; }

			public bool ExitDueToMotionLimit { get; internal set; }

			public bool RampUpVelocity { get; set; } = true;

			internal float UpdatePRatio(float pRatio)
			{
				if (MaxPRatio > pRatio)
				{
					MaxPRatio = Mathf.Lerp(MaxPRatio, pRatio, 0.1f);
				}
				else
				{
					MaxPRatio = Mathf.Lerp(MaxPRatio, pRatio, 0.3f);
				}
				return MaxPRatio;
			}

			internal void UpdateVelocity(float targetVelocity)
			{
				if (RampUpVelocity)
				{
					Velocity = LerpVelocity(Velocity, targetVelocity);
				}
				else
				{
					Velocity = targetVelocity;
				}
			}

			public float LerpVelocity(float actual, float target)
			{
				float velocity = Mathf.Lerp(actual, target, 0.1f);
				return Mathf.Min(velocity, actual + (float)Math.Sign(target - actual) * Mathf.Min(0.07f, Mathf.Abs(target - actual)));
			}
		}

		private const float MAX_DEFORMATION_FACTOR = 0.6f;

		private OverlayService overlay;

		private IInputHandle hotkeyHandle;

		private PelvisMovementController controller;

		private LocalEffectorOffset controllerOffsets;

		private Traverse<float> controllerSmoothTime;

		private float defaultControllerMaxSpeed;

		private float defaultControllerSmoothTime;

		private PlacerBase pleasure;

		private ConfigEntry<KeyboardShortcut> hotkey;

		private ConfigEntry<bool> useConstantVelocity;

		private ConfigEntry<bool> reduceSmoothTime;

		private ConfigEntry<bool> targetVelocityScale;

		private float lastDepth;

		private float lastTickDepth;

		private bool firstThrust;

		public int DepthLookahead { get; set; } = 1;

		public float MaxDepth { get; set; } = 0.2f;

		public float MaxBalancedVelocity { get; set; } = 0.7f;

		public float MinVelocity { get; set; } = 0.05f;

		public float MaxVelocity { get; set; } = 0.7f;

		public float MaxSafeVelocity { get; set; } = 0.15f;

		// Ceiling on WITHDRAWAL speed (AIChat MAX_SAFE_OUT_VEL). Distinct from MaxSafeVelocity
		// above, which is a minimum applied to the per-depth velocity — do not conflate them.
		public float MaxSafeOutVelocity { get; set; } = 0.6f;

		// Adaptive-clamp state (see Thrust): last outward command actually issued, and the
		// penetratingWorldLength observed when it was issued.
		private float lastOutStep;

		private float lastPenLen;

		// Smoothed command→position ratio. Persists ACROSS strokes on purpose: it describes the
		// rig, not the stroke, so the first outward tick of every withdrawal is already clamped.
		private float posPerStep;

		/// <summary>
		/// This hole's own depth capacity in internals space, 0 when unknown. maxProfundidadPhysicsLocal
		/// is what the game itself compares penetratedDepthLocalInternals against
		/// (maximaProfundidadPhysicsAlcanzada), so the two are guaranteed to share a space.
		/// </summary>
		private float HoleDepthCapacity()
		{
			BoneStretchedChain h = (Sequence != null) ? Sequence.hole : null;
			if (h == null) return 0f;
			float v = h.maxProfundidadPhysicsLocal;
			return (v > 0.0001f) ? v : 0f;
		}

		/// <summary>The floor as a FRACTION of the usable span: user setting, never shallower than
		/// the pop-out margin, lifted by the speed margin. Shared by both floor spaces.</summary>
		/// <summary>
		/// True once the withdrawal has reached (or passed) the turnaround point, in whichever
		/// space is active. This is the out-stroke's terminator — it must not depend on
		/// deformation, which never fully settles to 1.
		/// </summary>
		private bool AtEntrance()
		{
			float cap = HoleDepthCapacity();
			if (cap > 0f)
			{
				return InternalsDepth() <= cap * FloorFraction();
			}
			PeneLens L = ReadPeneLens();
			if (!L.valid) return false;
			return L.pen <= GetMinPenetrationExpectation();
		}

		// WORLD→INTERNALS SCALE, measured rather than assumed.
		// The tip (worldTipPartLength) is a world length; the floor now lives in the hole's
		// internals space. Nothing converts between them by a constant — but penetratingWorldLength
		// and penetratedDepthLocalInternals describe the SAME insertion at the same instant, so
		// their ratio is the live scale factor. Sampled only when both are well clear of zero
		// (near the entrance the ratio is noise) and smoothed.
		private float internalsPerWorld;

		private void SampleInternalsPerWorld()
		{
			try
			{
				if (HoleDepthCapacity() <= 0f) return;
				float pen = base.Session.Player.Character.pene.penetratingWorldLength;
				float ints = InternalsDepth();
				if (pen < 0.01f || ints < 0.0005f) return;
				float r = ints / pen;
				if (r <= 0f || float.IsNaN(r) || float.IsInfinity(r)) return;
				internalsPerWorld = (internalsPerWorld <= 0f) ? r : Mathf.Lerp(internalsPerWorld, r, 0.1f);
			}
			catch
			{
			}
		}

		/// <summary>
		/// The tip segment as a fraction of this hole's depth, 0 when not yet measurable.
		/// Natural withdrawal leaves the TIP inside, so this is what the floor should reserve.
		/// </summary>
		private float TipFractionOfCapacity()
		{
			float cap = HoleDepthCapacity();
			if (cap <= 0f || internalsPerWorld <= 0f) return 0f;
			try
			{
				float tipWorld = base.Session.Player.Character.pene.worldTipPartLength;
				if (tipWorld <= 0.0001f) return 0f;
				return Mathf.Clamp01(tipWorld * internalsPerWorld / cap);
			}
			catch
			{
				return 0f;
			}
		}

		// ══ ALIGNMENT PROBE ══════════════════════════════════════════════════════════════════
		// Self-driving experiment for ALIGNMENT_THEORY.md. Arm it from Mission Control, start
		// thrusting, and it runs the whole protocol by itself, logging one parseable line per
		// tick plus a summary per phase.
		//
		// It calibrates BEFORE it solves. Nothing in the codebase establishes which world
		// direction AddVerticalDelta/AddHorizontalDelta actually move the base in, nor the
		// metres-per-unit scale — assuming it is why every previous attempt needed a sign
		// learner. The probe measures both by commanding each axis alone and watching the base
		// move, then solves e-perp in that MEASURED basis.
		public bool AlignTest { get; set; }

		// ══ FREE-SPACE CHARACTERISATION ══════════════════════════════════════════════════════
		// Measures what the CHARACTER can do, with no hole involved. Every earlier calibration
		// was taken mid-stroke inside her, so it was contaminated by the stroke's own motion
		// (rawDot came back POSITIVE), confounded by warm-up moving d, limited to whatever
		// headroom existed at that instant, and unable to sweep a full range without risking
		// pop-out. None of that applies out here.
		//
		// What it produces is a forward kinematic map: pelvis (x,y,z) → shaft direction and base
		// position, expressed in ROOT-LOCAL coordinates so it is independent of where the player
		// stands or faces. With that map, aligning to a hole stops being a search and becomes an
		// inversion. Run it once per character; it needs no partner and no penetration.
		public bool FreeCal { get; set; }

		// Each axis returns to where it started BEFORE the next one moves. Without that, every leg
		// after the first was measured from a pose left at the previous axis's extreme — a
		// confound, and it looks wrong on screen too.
		private enum FreePhase
		{
			Idle,
			YUp, YDown, YReturn,
			XUp, XDown, XReturn,
			ZUp, ZDown, ZReturn,
			YawPlus, YawMinus, YawReturn,
			Coupling,
			Done
		}

		// COUPLING: the owner's point that back/forward travel is limited BY height and by
		// left/right. If true the reachable space is an envelope, not a box, and a solver that
		// treats the axes as independent will demand poses that do not exist. Measured by driving
		// y to low / mid / high in turn and sweeping z fully at each, recording the z extent
		// actually achieved.
		private int coupStage;      // 0=goto y, 1=sweep z+, 2=sweep z-
		private int coupLevel;      // 0=low, 1=mid, 2=high
		private float coupTargetY, coupZMin, coupZMax;

		/// <summary>
		/// Sweep rate, command units/sec. Live-tunable because calibration speed costs nothing in
		/// accuracy here: samples are BINNED BY POSITION, not by time, so a fast sweep and a slow
		/// one produce the same curve — only the sample count per bin changes. The z range is
		/// about 1.0 units, so 0.15 took ~7 s per leg and the full run several minutes; 0.6 does
		/// the same sweep in under 2 s.
		///
		/// The real limit is the PLANT, not the sampler: the pelvis IK lags, so past some rate the
		/// rig stops tracking the command and the "position" a sample is binned against is not
		/// where the body actually is. The turnaround-bin filter catches the worst of that, but if
		/// curves start disagreeing between runs at high rate, that is the ceiling and the fix is
		/// to slow down rather than to sample harder.
		/// </summary>
		public static float FreeCalRateLive = 0.6f;

		private static float FreeCalRate => Mathf.Clamp(FreeCalRateLive, 0.05f, 2f);
		private const float FreeCalMaxSeconds = 20f;  // per-leg watchdog

		private FreePhase freePhase = FreePhase.Idle;
		private float freeT;
		private int freeRun;
		private float freeSpanMin, freeSpanMax;
		private Vector3 freeDirAtMin, freeDirAtMax;
		private float freeValAtMin, freeValAtMax;

		// Yaw rotates the WHOLE avatar, so it must be returned to where it started — and it is
		// invisible in root-local coordinates (the frame turns with the body), which is why the
		// yaw legs are judged on the shaft's WORLD direction instead.
		private const float FreeYawRatePerSec = 10f;
		private const float FreeYawLimitDeg = 20f;
		private float freeYawAccum;

		private Vector3 freeOriginOff;

		// SCALE. Every distance measured here — ranges, base travel, metres per command unit — is
		// in the character's CURRENT scale, and the player is scalable (AutoSeeker even calls
		// Session.Player.AddScale). A gain stored raw would silently be wrong the moment the
		// character is resized. Logged alongside every result so measurements can be normalised
		// rather than re-derived.
		private float PlayerScaleNow()
		{
			try
			{
				float s = (controller.character?.escala).GetValueOrDefault(0f);
				if (s > 0.0001f) return s;
				return base.Session.Player.Character.animatorRootMotionTransform.lossyScale.y;
			}
			catch
			{
				return 1f;
			}
		}

		private float PeneLengthNow()
		{
			try { return base.Session.Player.Character.pene.worldLengthFromUnderSkin; }
			catch { return 0f; }
		}

		// ══ LIVE ENVELOPE MONITOR ════════════════════════════════════════════════════════════
		// The free-space map (ALIGNMENT_CAPABILITY_MAP.md) is a REST-STATE measurement. Under load
		// the reachable envelope is smaller and dynamic: the partner's body blocks travel, the
		// hole constrains the tip, and PelvisMovementLimitSegunHoleFondo actively pushes the
		// pelvis on the same axes we command. A solver trusting the rest-state box will keep
		// requesting poses that cannot happen.
		//
		// This runs during REAL use, costs a handful of float ops per tick, and answers one
		// question per axis: when we command movement, do we actually get it? Sustained
		// achieved/commanded well below 1 means that direction is blocked HERE AND NOW, whatever
		// the nominal range says. Logging is throttled; nothing here steers (R10).
		private Vector3 envLastOff;
		private bool envPrimed;
		private Vector3 envCmdAccum, envGotAccum;
		private Vector3 envObservedMin, envObservedMax;
		private float envWindowT;
		private int envBlockedMask;

		private void UpdateEnvelopeMonitor(float dt)
		{
			Vector3 off;
			try { off = controllerOffsets.leftThighOffset; } catch { return; }

			if (!envPrimed)
			{
				envLastOff = off;
				envObservedMin = envObservedMax = off;
				envPrimed = true;
				return;
			}

			Vector3 got = off - envLastOff;
			envLastOff = off;
			envGotAccum += new Vector3(Mathf.Abs(got.x), Mathf.Abs(got.y), Mathf.Abs(got.z));
			envObservedMin = Vector3.Min(envObservedMin, off);
			envObservedMax = Vector3.Max(envObservedMax, off);
			envWindowT += dt;

			if (envWindowT < 4f) return;

			// Effectiveness per axis over the window: achieved / commanded, 1 = free, 0 = pinned.
			float ex = (envCmdAccum.x > 1e-5f) ? envGotAccum.x / envCmdAccum.x : -1f;
			float ey = (envCmdAccum.y > 1e-5f) ? envGotAccum.y / envCmdAccum.y : -1f;
			float ez = (envCmdAccum.z > 1e-5f) ? envGotAccum.z / envCmdAccum.z : -1f;
			int mask = ((ex >= 0f && ex < 0.25f) ? 1 : 0)
				| ((ey >= 0f && ey < 0.25f) ? 2 : 0)
				| ((ez >= 0f && ez < 0.25f) ? 4 : 0);

			// Announce immediately when the blocked set CHANGES; otherwise report rarely.
			if (mask != envBlockedMask)
			{
				logger.Info(
					"[ENVELOPE] blocked-change mask={0} (bit0=x bit1=y bit2=z) effX={1:F2} effY={2:F2} "
					+ "effZ={3:F2} off=({4:F3},{5:F3},{6:F3}) - a blocked axis cannot deliver the pose "
					+ "the rest-state map promises",
					mask, ex, ey, ez, off.x, off.y, off.z);
				envBlockedMask = mask;
			}
			else
			{
				logger.InfoRare(6,
					"[ENVELOPE] effX={0:F2} effY={1:F2} effZ={2:F2} usedX=[{3:F3},{4:F3}] "
					+ "usedY=[{5:F3},{6:F3}] usedZ=[{7:F3},{8:F3}] blockedMask={9}",
					ex, ey, ez, envObservedMin.x, envObservedMax.x, envObservedMin.y,
					envObservedMax.y, envObservedMin.z, envObservedMax.z, mask);
			}

			envCmdAccum = Vector3.zero;
			envGotAccum = Vector3.zero;
			envWindowT = 0f;
		}

		/// <summary>Every command this feature issues is registered here, so effectiveness is
		/// measured against what we ACTUALLY asked for rather than what we intended.</summary>
		private void NoteCommand(float x, float y, float z)
		{
			envCmdAccum += new Vector3(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z));
		}

		private bool TryRootLocal(out Vector3 baseLocal, out Vector3 shaftLocal, out Vector3 offsets)
		{
			baseLocal = shaftLocal = offsets = Vector3.zero;
			try
			{
				Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
				if (rm == null) return false;
				Vector3 B = PeneBase();
				Vector3 T = PeneTip();
				if ((T - B).sqrMagnitude < 1e-9f) return false;
				baseLocal = rm.InverseTransformPoint(B);
				shaftLocal = rm.InverseTransformDirection((T - B).normalized);
				offsets = controllerOffsets.leftThighOffset;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void FreeAdvance(FreePhase next)
		{
			freePhase = next;
			freeT = 0f;
			freeSpanMin = float.MaxValue; freeSpanMax = float.MinValue;
		}

		// ── Z -> PITCH CURVE, SAMPLED PER HEIGHT ─────────────────────────────────────────────
		//
		// One bucket per 1 cm of z travel, holding the mean pitch observed there. Binning rather
		// than keeping every tick makes the sweep rate irrelevant: a slow pass and a fast pass
		// produce the same table, so curves from different levels are comparable even though the
		// sweeps take different times.
		private const int CoupBins = 40;

		private const float CoupBinSize = 0.025f;

		private readonly float[] coupBinSum = new float[CoupBins];
		private readonly int[] coupBinN = new int[CoupBins];
		private float coupBinBase;
		private bool coupBinPrimed;

		// ── PITCH SURFACE: pitch = f(hipZ, hipY) ─────────────────────────────────────────────
		//
		// Measured 2026-08-07, and it settled a design question rather than confirming one. The
		// z->pitch response is NOT one curve:
		//
		//     y = -1.61 (crouched)   ~28 deg/unit, linear
		//     y = -0.79 (mid)        ~54 deg/unit, linear
		//     y =  0.00 (standing)   16 -> 56 deg/unit, strongly non-linear
		//
		// Height changes the gain by ~2x, shifts the whole curve (pitch at z = -0.5 runs +7.3,
		// -9.4, -17.7 across the three) and even changes its SHAPE. So a single slope — or a
		// single 1-D table — is wrong everywhere except the height it happened to be measured at,
		// which is why the "absolute" map kept disagreeing with itself between runs.
		//
		// This also retires an older conclusion. The capability map called the axes independent
		// because z SPAN is identical at every height (0.985/0.995/0.996 again this run). True,
		// and irrelevant: span is REACH, response is what the pelvis does to the angle inside that
		// reach. The box is the same at every height; the field inside it is not.
		private sealed class PitchSurface
		{
			public const int Levels = 3;

			public readonly float[] LevelY = new float[Levels];
			public readonly float[][] Bins = new float[Levels][];
			public readonly bool[][] BinValid = new bool[Levels][];
			public readonly bool[] LevelSet = new bool[Levels];
			public float BinBase;
			public float BinSize = 0.025f;

			public bool Ready
			{
				get
				{
					int n = 0;
					for (int i = 0; i < Levels; i++) if (LevelSet[i]) n++;
					return n >= 2;
				}
			}

			public void Clear()
			{
				for (int i = 0; i < Levels; i++) { LevelSet[i] = false; Bins[i] = null; BinValid[i] = null; }
			}

			private float PitchAtLevel(int lv, float z)
			{
				if (!LevelSet[lv] || Bins[lv] == null) return float.NaN;
				float fi = (z - BinBase) / BinSize - 0.5f;
				int i0 = Mathf.FloorToInt(fi);
				int i1 = i0 + 1;
				float[] b = Bins[lv];
				bool[] v = BinValid[lv];
				if (i0 < 0 || i1 >= b.Length || !v[i0] || !v[i1]) return float.NaN;
				return Mathf.Lerp(b[i0], b[i1], Mathf.Clamp01(fi - i0));
			}

			/// <summary>Pitch at (z, y): interpolate each bracketing level at z, then blend by y.
			/// Levels are stored in sweep order, so the bracket is found by VALUE not index.</summary>
			public float PitchAt(float z, float y)
			{
				int lo = -1, hi = -1;
				for (int i = 0; i < Levels; i++)
				{
					if (!LevelSet[i]) continue;
					if (LevelY[i] <= y && (lo < 0 || LevelY[i] > LevelY[lo])) lo = i;
					if (LevelY[i] >= y && (hi < 0 || LevelY[i] < LevelY[hi])) hi = i;
				}
				if (lo < 0 && hi < 0) return float.NaN;
				// Outside the measured height range, CLAMP to the nearest level rather than
				// extrapolate: extrapolating a surface whose shape changes with height is how you
				// invent numbers that look plausible.
				if (lo < 0) return PitchAtLevel(hi, z);
				if (hi < 0) return PitchAtLevel(lo, z);
				if (lo == hi) return PitchAtLevel(lo, z);
				float a = PitchAtLevel(lo, z), b2 = PitchAtLevel(hi, z);
				if (float.IsNaN(a)) return b2;
				if (float.IsNaN(b2)) return a;
				float t = Mathf.Clamp01((y - LevelY[lo]) / Mathf.Max(1E-05f, LevelY[hi] - LevelY[lo]));
				return Mathf.Lerp(a, b2, t);
			}

			/// <summary>
			/// Invert: which hip z produces targetPitch at height y. Scans the measured span for a
			/// sign change and interpolates inside that bin pair — no curve fitting, and no
			/// assumption of monotonicity beyond the bracket actually found.
			/// </summary>
			public bool TrySolveZ(float targetPitch, float y, out float z)
			{
				z = 0f;
				if (!Ready) return false;
				int n = 0;
				for (int i = 0; i < Levels; i++) if (Bins[i] != null) { n = Bins[i].Length; break; }
				if (n == 0) return false;

				float prevZ = 0f, prevP = float.NaN;
				for (int i = 0; i < n; i++)
				{
					float zi = BinBase + (i + 0.5f) * BinSize;
					float pi = PitchAt(zi, y);
					if (float.IsNaN(pi)) { prevP = float.NaN; prevZ = zi; continue; }
					if (!float.IsNaN(prevP))
					{
						float d0 = prevP - targetPitch, d1 = pi - targetPitch;
						if ((d0 <= 0f && d1 >= 0f) || (d0 >= 0f && d1 <= 0f))
						{
							float t = (Mathf.Abs(d1 - d0) < 1E-06f) ? 0f : d0 / (d0 - d1);
							z = Mathf.Lerp(prevZ, zi, Mathf.Clamp01(t));
							return true;
						}
					}
					prevZ = zi;
					prevP = pi;
				}
				return false;
			}
		}

		private readonly PitchSurface pitchSurface = new PitchSurface();

		/// <summary>True once at least two height levels have been measured.</summary>
		public bool PitchSurfaceReady => pitchSurface.Ready;

		/// <summary>
		/// Hip z that yields the requested pitch at the CURRENT hip height, from the measured
		/// surface. Returns false when uncalibrated, or when the target lies outside what was
		/// actually measured — callers then fall back to the slope rather than being handed an
		/// invented z.
		/// </summary>
		public bool TrySolveHipZForPitch(float targetPitchDeg, out float z)
		{
			z = 0f;
			if (!pitchSurface.Ready) return false;
			float y;
			try { y = controllerOffsets.leftThighOffset.y; }
			catch { return false; }
			return pitchSurface.TrySolveZ(targetPitchDeg, y, out z);
		}

		private void CoupResetCurve()
		{
			for (int i = 0; i < CoupBins; i++) { coupBinSum[i] = 0f; coupBinN[i] = 0; }
			coupBinPrimed = false;
		}

		private void CoupSample(float z, float pitch)
		{
			if (!coupBinPrimed) { coupBinBase = z - (CoupBins * CoupBinSize * 0.5f); coupBinPrimed = true; }
			int i = Mathf.Clamp(Mathf.FloorToInt((z - coupBinBase) / CoupBinSize), 0, CoupBins - 1);
			coupBinSum[i] += pitch;
			coupBinN[i]++;
		}

		/// <summary>
		/// Emit the curve for one height, plus per-third slopes.
		///
		/// The thirds are the point: a single deg/unit averaged over the whole range hid a 37 vs
		/// 100 deg/unit split, and averaging is exactly how a strong non-linearity disappears into
		/// a plausible-looking constant. Reporting low/mid/high separately makes the shape visible
		/// without any fitting, and lining the three heights up side by side answers whether pitch
		/// depends on height at all.
		/// </summary>
		private void EmitCoupCurve(int level, float atY)
		{
			// DISCARD THE TURNAROUND BINS.
			//
			// The end bins of a sweep catch the rig CHANGING LEVELS, not travelling along z, and
			// the pitch jumps discontinuously inside a single 2.5 cm bucket. Run 1 showed exactly
			// this: -4.5 -> 25.6 in one bin, reported as slopeHigh = 206 deg/unit, which then
			// dragged slopeAll to 91 for a level whose real slope is about 30. A transition
			// sample is not a measurement of the axis being swept.
			//
			// They are identifiable without heuristics about the values: a bin the sweep merely
			// passed through holds far fewer samples than one it travelled across at rate. Anything
			// under a third of the median count is discarded as a turnaround.
			int medianN = 0;
			{
				var counts = new System.Collections.Generic.List<int>();
				for (int i = 0; i < CoupBins; i++) if (coupBinN[i] > 0) counts.Add(coupBinN[i]);
				if (counts.Count > 0)
				{
					counts.Sort();
					medianN = counts[counts.Count / 2];
				}
			}
			int minN = Mathf.Max(2, medianN / 3);
			int dropped = 0;
			for (int i = 0; i < CoupBins; i++)
			{
				if (coupBinN[i] > 0 && coupBinN[i] < minN) { coupBinN[i] = 0; coupBinSum[i] = 0f; dropped++; }
			}

			var sb = new System.Text.StringBuilder();
			int first = -1, last = -1;
			for (int i = 0; i < CoupBins; i++)
			{
				if (coupBinN[i] == 0) continue;
				if (first < 0) first = i;
				last = i;
				float z = coupBinBase + (i + 0.5f) * CoupBinSize;
				sb.Append(z.ToString("F3")).Append(':')
				  .Append((coupBinSum[i] / coupBinN[i]).ToString("F1")).Append(' ');
			}
			if (first < 0)
			{
				logger.Info("[FREECAL] ZPITCH level={0} atY={1:F3} - no samples", level, atY);
				CoupResetCurve();
				return;
			}

			float Slope(int a, int b)
			{
				if (a < 0 || b < 0 || a == b || coupBinN[a] == 0 || coupBinN[b] == 0) return float.NaN;
				float za = coupBinBase + (a + 0.5f) * CoupBinSize;
				float zb = coupBinBase + (b + 0.5f) * CoupBinSize;
				if (Mathf.Abs(zb - za) < 1E-05f) return float.NaN;
				return ((coupBinSum[b] / coupBinN[b]) - (coupBinSum[a] / coupBinN[a])) / (zb - za);
			}

			int t1 = first + (last - first) / 3;
			int t2 = first + 2 * (last - first) / 3;
			logger.Info("[FREECAL] ZPITCH level={0} atY={1:F3} slopeLow={2:F1} slopeMid={3:F1} "
				+ "slopeHigh={4:F1} slopeAll={5:F1} deg/unit scale={6:F3} len={7:F4} normAll={8:F1} "
				+ "(compare the three levels: differing slopes = pitch depends on HEIGHT too, "
				+ "and a 1-D z table would be wrong away from the height it was measured at)",
				level, atY, Slope(first, t1), Slope(t1, t2), Slope(t2, last), Slope(first, last),
				PlayerScaleNow(), PeneLengthNow(), Slope(first, last) * PeneLengthNow());
			logger.Info("[FREECAL] ZPITCH level={0} curve z:pitch = {1} (dropped {2} turnaround bins)",
				level, sb.ToString().Trim(), dropped);

			// Store this level into the surface. Cleaned bins only, so the turnaround samples that
			// produced 206 deg/unit cannot reach the solver.
			if (level >= 0 && level < PitchSurface.Levels)
			{
				var bins = new float[CoupBins];
				var valid = new bool[CoupBins];
				for (int i = 0; i < CoupBins; i++)
				{
					valid[i] = coupBinN[i] > 0;
					bins[i] = valid[i] ? coupBinSum[i] / coupBinN[i] : 0f;
				}
				pitchSurface.BinBase = coupBinBase;
				pitchSurface.BinSize = CoupBinSize;
				pitchSurface.LevelY[level] = atY;
				pitchSurface.Bins[level] = bins;
				pitchSurface.BinValid[level] = valid;
				pitchSurface.LevelSet[level] = true;
				logger.Info("[FREECAL] ZPITCH surface level {0} stored at y={1:F3} ({2} usable bins) "
					+ "- surface ready: {3}", level, atY, last - first + 1, pitchSurface.Ready);

				// Publish for remote reading. Slope over the CLEANED span, so the turnaround bins
				// that produced 206 deg/unit cannot reach this either.
				float slopeAll = Slope(first, last);
				if (level == 0) { BeTestControl.SlopeLevel0 = slopeAll; BeTestControl.LevelY0 = atY; }
				else if (level == 1) { BeTestControl.SlopeLevel1 = slopeAll; BeTestControl.LevelY1 = atY; }
				else if (level == 2) { BeTestControl.SlopeLevel2 = slopeAll; BeTestControl.LevelY2 = atY; }
				BeTestControl.SurfaceReady = pitchSurface.Ready;
				BeTestControl.AtScale = PlayerScaleNow();
				BeTestControl.AtPeneLength = PeneLengthNow();
				BeTestControl.Summary = string.Format(
					"scale={0:F3} len={1:F4} slopes(y): {2:F1}@{3:F2} {4:F1}@{5:F2} {6:F1}@{7:F2} ready={8}",
					BeTestControl.AtScale, BeTestControl.AtPeneLength,
					BeTestControl.SlopeLevel0, BeTestControl.LevelY0,
					BeTestControl.SlopeLevel1, BeTestControl.LevelY1,
					BeTestControl.SlopeLevel2, BeTestControl.LevelY2,
					pitchSurface.Ready);
			}
			CoupResetCurve();
		}

		private void UpdateFreeCal()
		{
			if (!FreeCal)
			{
				if (freePhase != FreePhase.Idle) freePhase = FreePhase.Idle;
				return;
			}
			float dt = Time.deltaTime;

			if (freePhase == FreePhase.Idle)
			{
				// PRECONDITION: the pene must be out and erect, or base/tip are meaningless and the
				// whole map is garbage. Fail loudly instead of logging plausible nonsense.
				float lenChk = 0f, chordChk = 0f;
				try
				{
					Penis pk = base.Session.Player.Character.pene;
					lenChk = pk.worldLengthFromUnderSkin;
					chordChk = pk.realCurrentWorldLengthFromUnderSkin;
				}
				catch
				{
				}
				if (lenChk < 0.02f || chordChk < 0.02f || chordChk < lenChk * 0.5f)
				{
					logger.Info(
						"[FREECAL] NOT READY - expose the pene and let it become erect first "
						+ "(worldLengthFromUnderSkin={0:F4} chord={1:F4}). Toggle off and on to retry.",
						lenChk, chordChk);
					freePhase = FreePhase.Done;
					return;
				}
				freeRun++;
				freeYawAccum = 0f;
				try { freeOriginOff = controllerOffsets.leftThighOffset; } catch { freeOriginOff = Vector3.zero; }
				// LOG THE ACTUAL RANGES. "Does the pelvis go backwards from centre?" is a question
				// about zRange.min, and asserting it without reading it is how this session has
				// gone wrong repeatedly. If a min is 0, that direction genuinely does not exist on
				// this axis and no sweep will produce it.
				try
				{
					PelvisMovementController.Range rx = controller.xRange;
					PelvisMovementController.Range ry = controller.yRange;
					PelvisMovementController.Range rz = controller.zRange;
					logger.Info(
						"[FREECAL] run={0} RANGES-RAW x=[{1:F4},{2:F4}] y=[{3:F4},{4:F4}] z=[{5:F4},{6:F4}] "
						+ "RANGES-LIMITED x=[{7:F4},{8:F4}] y=[{9:F4},{10:F4}] z=[{11:F4},{12:F4}] "
						+ "startOff=({13:F4},{14:F4},{15:F4}) len={16:F4} "
						+ "(if z-min is ~0 the pelvis has NO backward travel on that axis at all)",
						freeRun, rx.min, rx.max, ry.min, ry.max, rz.min, rz.max,
						rx.MinLimited(), rx.MaxLimited(), ry.MinLimited(), ry.MaxLimited(),
						rz.MinLimited(), rz.MaxLimited(),
						freeOriginOff.x, freeOriginOff.y, freeOriginOff.z, lenChk);
				}
				catch
				{
					logger.Info("[FREECAL] run={0} RANGES unreadable len={1:F4}", freeRun, lenChk);
				}
				FreeAdvance(FreePhase.YUp);
				return;
			}
			if (freePhase == FreePhase.Done) return;

			if (!TryRootLocal(out Vector3 baseLocal, out Vector3 shaftLocal, out Vector3 off))
			{
				logger.Info("[FREECAL] run={0} ABORT no-pene-geometry", freeRun);
				freePhase = FreePhase.Done;
				return;
			}

			freeT += dt;
			AxisHeadroom(out float xP, out float xM, out float yP, out float yM);

			// Shaft pitch/yaw in the ROOT frame — the character's own capability, hole-independent.
			float pitchDeg = Mathf.Asin(Mathf.Clamp(shaftLocal.y, -1f, 1f)) * Mathf.Rad2Deg;
			float yawDeg = Mathf.Atan2(shaftLocal.x, Mathf.Max(1e-6f, shaftLocal.z)) * Mathf.Rad2Deg;

			// Z headroom was missing entirely, so the depth legs never detected their limit and
			// just burned the 20 s watchdog sitting saturated. Depth runs BOTH ways, same as the
			// others.
			float zP = 0f, zM = 0f;
			try
			{
				PelvisMovementController.Range zr = controller.zRange;
				zP = Mathf.Max(0f, zr.max - off.z);
				zM = Mathf.Max(0f, off.z - zr.min);
			}
			catch
			{
			}

			float cmdX = 0f, cmdY = 0f, cmdZ = 0f, cmdYaw = 0f;
			float tracked = 0f;
			switch (freePhase)
			{
				case FreePhase.YUp:   cmdY = FreeCalRate * dt; tracked = off.y; break;
				case FreePhase.YDown: cmdY = -FreeCalRate * dt; tracked = off.y; break;
				case FreePhase.XUp:   cmdX = FreeCalRate * dt; tracked = off.x; break;
				case FreePhase.XDown: cmdX = -FreeCalRate * dt; tracked = off.x; break;
				case FreePhase.ZUp:   cmdZ = FreeCalRate * dt; tracked = off.z; break;
				case FreePhase.ZDown: cmdZ = -FreeCalRate * dt; tracked = off.z; break;
				case FreePhase.YReturn:
					cmdY = Mathf.Clamp(freeOriginOff.y - off.y, -FreeCalRate * dt, FreeCalRate * dt);
					if (Mathf.Abs(freeOriginOff.y - off.y) < 0.002f) cmdY = 0f;
					tracked = off.y;
					break;
				case FreePhase.XReturn:
					cmdX = Mathf.Clamp(freeOriginOff.x - off.x, -FreeCalRate * dt, FreeCalRate * dt);
					if (Mathf.Abs(freeOriginOff.x - off.x) < 0.002f) cmdX = 0f;
					tracked = off.x;
					break;
				case FreePhase.ZReturn:
					cmdZ = Mathf.Clamp(freeOriginOff.z - off.z, -FreeCalRate * dt, FreeCalRate * dt);
					if (Mathf.Abs(freeOriginOff.z - off.z) < 0.002f) cmdZ = 0f;
					tracked = off.z;
					break;
				case FreePhase.Coupling:
				{
					tracked = off.z;
					if (coupStage == 0)
					{
						float dy = coupTargetY - off.y;
						cmdY = Mathf.Clamp(dy, -FreeCalRate * dt, FreeCalRate * dt);
						cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, yP) : Mathf.Max(cmdY, -yM);
						// Reached, or the axis refuses to go further — either way start sweeping.
						if (Mathf.Abs(dy) < 0.004f || cmdY == 0f)
						{
							coupStage = 1;
							coupZMin = float.MaxValue; coupZMax = float.MinValue;
							freeT = 0f;
						}
					}
					else
					{
						cmdZ = (coupStage == 1) ? FreeCalRate * dt : -FreeCalRate * dt;
						cmdZ = (cmdZ >= 0f) ? Mathf.Min(cmdZ, zP) : Mathf.Max(cmdZ, -zM);
						if (off.z < coupZMin) coupZMin = off.z;
						if (off.z > coupZMax) coupZMax = off.z;

						// SAMPLE THE Z->PITCH CURVE, AT THIS HEIGHT.
						//
						// The coupling phase already sweeps z at min/mid/max height, but only
						// reported the z SPAN per level — enough to show the reachable envelope is
						// the same, which is not the same claim as the pitch RESPONSE being the
						// same. Those are different questions and only the first was ever answered.
						//
						// Sampling the curve at each height answers the second, and decides the
						// shape of the map the solver needs: identical curves mean one 1-D table
						// suffices; curves that differ by height mean pitch is a function of BOTH
						// axes and a 1-D table would be wrong everywhere except the height it
						// happened to be measured at.
						CoupSample(off.z, pitchDeg);
						if (cmdZ == 0f || freeT > 8f)
						{
							if (coupStage == 1) { coupStage = 2; freeT = 0f; }
							else
							{
								logger.Info(
									"[FREECAL] run={0} COUPLING-RESULT level={1} atY={2:F4} "
									+ "zMin={3:F4} zMax={4:F4} zSpan={5:F4} "
									+ "(compare zSpan across levels - shrinking span = coupled envelope)",
									freeRun, coupLevel, off.y, coupZMin, coupZMax, coupZMax - coupZMin);
								EmitCoupCurve(coupLevel, off.y);
								coupLevel++;
								coupStage = 0;
								freeT = 0f;
								if (coupLevel <= 2)
								{
									try
									{
										PelvisMovementController.Range yrc = controller.yRange;
										float lo = yrc.MinLimited(), hi = yrc.MaxLimited();
										coupTargetY = (coupLevel == 1) ? (lo + hi) * 0.5f : hi;
									}
									catch { coupTargetY = off.y; }
								}
							}
						}
					}
					break;
				}
				case FreePhase.YawPlus:
					cmdYaw = (freeYawAccum < FreeYawLimitDeg) ? FreeYawRatePerSec * dt : 0f;
					tracked = freeYawAccum;
					break;
				case FreePhase.YawMinus:
					cmdYaw = (freeYawAccum > -FreeYawLimitDeg) ? -FreeYawRatePerSec * dt : 0f;
					tracked = freeYawAccum;
					break;
				case FreePhase.YawReturn:
					cmdYaw = (Mathf.Abs(freeYawAccum) > 0.2f)
						? Mathf.Clamp(-freeYawAccum, -FreeYawRatePerSec * dt, FreeYawRatePerSec * dt) : 0f;
					tracked = freeYawAccum;
					break;
			}
			cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, yP) : Mathf.Max(cmdY, -yM);
			cmdX = (cmdX >= 0f) ? Mathf.Min(cmdX, xP) : Mathf.Max(cmdX, -xM);
			cmdZ = (cmdZ >= 0f) ? Mathf.Min(cmdZ, zP) : Mathf.Max(cmdZ, -zM);

			if (cmdY != 0f) controller.AddVerticalDelta(cmdY);
			if (cmdX != 0f) controller.AddHorizontalDelta(cmdX);
			if (cmdZ != 0f) controller.AddProfundidadDelta(cmdZ);
			if (cmdYaw != 0f)
			{
				try { base.Session.Player.Rotate(cmdYaw); freeYawAccum += cmdYaw; } catch { }
			}

			if (tracked < freeSpanMin) { freeSpanMin = tracked; freeDirAtMin = shaftLocal; freeValAtMin = tracked; }
			if (tracked > freeSpanMax) { freeSpanMax = tracked; freeDirAtMax = shaftLocal; freeValAtMax = tracked; }

			// Shaft direction in WORLD too: yaw turns the root frame with the body, so a body
			// rotation is invisible in root-local coordinates and can only be seen out here.
			Vector3 shaftWorld = (PeneTip() - PeneBase()).normalized;
			float worldYawDeg = Mathf.Atan2(shaftWorld.x, shaftWorld.z) * Mathf.Rad2Deg;

			logger.Info(
				"[FREECAL] run={0} phase={1} t={2:F2} off=({3:F4},{4:F4},{5:F4}) "
				+ "baseLocal=({6:F4},{7:F4},{8:F4}) shaftLocal=({9:F3},{10:F3},{11:F3}) "
				+ "pitch={12:F1} yaw={13:F1} worldYaw={14:F1} yawAccum={15:F1} "
				+ "cmd=({16:F5},{17:F5},{18:F5},yaw{19:F3})",
				freeRun, freePhase, freeT, off.x, off.y, off.z,
				baseLocal.x, baseLocal.y, baseLocal.z,
				shaftLocal.x, shaftLocal.y, shaftLocal.z, pitchDeg, yawDeg, worldYawDeg,
				freeYawAccum, cmdX, cmdY, cmdZ, cmdYaw);

			// A leg ends when its axis stops moving (range reached) or the watchdog fires. The
			// coupling phase runs its own sub-state machine and ends when all levels are done.
			bool stalled;
			if (freePhase == FreePhase.Coupling)
			{
				stalled = coupLevel > 2;
			}
			else
			{
				stalled = (cmdX == 0f && cmdY == 0f && cmdZ == 0f && cmdYaw == 0f)
					|| freeT > FreeCalMaxSeconds;
			}
			if (!stalled) return;

			float pMin = Mathf.Asin(Mathf.Clamp(freeDirAtMin.y, -1f, 1f)) * Mathf.Rad2Deg;
			float pMax = Mathf.Asin(Mathf.Clamp(freeDirAtMax.y, -1f, 1f)) * Mathf.Rad2Deg;
			float span = Mathf.Abs(freeValAtMax - freeValAtMin);
			float scaleNow = PlayerScaleNow();
			float lenNow = PeneLengthNow();
			logger.Info(
				"[FREECAL] run={0} LEG-RESULT phase={1} axisFrom={2:F4} axisTo={3:F4} axisSpan={4:F4} "
				+ "pitchAtMin={5:F1} pitchAtMax={6:F1} pitchSpan={7:F1}deg degPerUnit={8:F1} "
				+ "scale={9:F4} len={10:F4} spanPerScale={11:F4} degPerUnit_x_len={12:F2} "
				+ "(character capability, no hole; last two are the scale/length-normalised forms)",
				freeRun, freePhase, freeValAtMin, freeValAtMax, span,
				pMin, pMax, Mathf.Abs(pMax - pMin),
				(span > 1e-5f) ? Mathf.Abs(pMax - pMin) / span : 0f,
				scaleNow, lenNow,
				(scaleNow > 1e-4f) ? span / scaleNow : 0f,
				(span > 1e-5f) ? (Mathf.Abs(pMax - pMin) / span) * lenNow : 0f);

			// FEED THE SOLVER ITS ANGLE MAP. This leg already sweeps the depth axis through its
			// entire range while tracking the pene's angle — precisely the measurement the pose
			// solve needs — so the solver should read it instead of carrying 69 deg/unit as a
			// constant lifted from one character on one run. Slope AND intercept both fall out of
			// the same two endpoints, which is what makes the map ABSOLUTE: hip z alone then
			// determines the pene angle, with no integration and no dependence on the pose the
			// solve happens to start from.
			if ((freePhase == FreePhase.ZUp || freePhase == FreePhase.ZDown) && span > 1E-05f)
			{
				float denom = freeValAtMax - freeValAtMin;
				float slope = (Mathf.Abs(denom) > 1E-05f) ? ((pMax - pMin) / denom) : 0f;
				if (Mathf.Abs(slope) > 1f)
				{
					solveSlopeZ = slope;
					solveInterceptZ = pMin - slope * freeValAtMin;
					solveMapCalibrated = true;
					solveMapAtLength = lenNow;
					solveMapAtScale = scaleNow;
					logger.Info(
						"[FREECAL] run={0} ANGLE-MAP CAPTURED peneAngle = {1:F2}*hipZ + {2:F2} deg "
						+ "at len={3:F4} scale={4:F3} (valid for THIS size; marked stale and reverts to the "
						+ "default if the character resizes, since the size law is unmeasured)",
						freeRun, solveSlopeZ, solveInterceptZ, lenNow, scaleNow);
				}
			}

			switch (freePhase)
			{
				case FreePhase.YUp: FreeAdvance(FreePhase.YDown); break;
				case FreePhase.YDown: FreeAdvance(FreePhase.YReturn); break;
				case FreePhase.YReturn: FreeAdvance(FreePhase.XUp); break;
				case FreePhase.XUp: FreeAdvance(FreePhase.XDown); break;
				case FreePhase.XDown: FreeAdvance(FreePhase.XReturn); break;
				case FreePhase.XReturn: FreeAdvance(FreePhase.ZUp); break;
				case FreePhase.ZUp: FreeAdvance(FreePhase.ZDown); break;
				case FreePhase.ZDown: FreeAdvance(FreePhase.ZReturn); break;
				case FreePhase.ZReturn: FreeAdvance(FreePhase.YawPlus); break;
				case FreePhase.YawPlus: FreeAdvance(FreePhase.YawMinus); break;
				case FreePhase.YawMinus: FreeAdvance(FreePhase.YawReturn); break;
				case FreePhase.YawReturn:
					coupLevel = 0; coupStage = 0; freeT = 0f;
					try
					{
						PelvisMovementController.Range yrc0 = controller.yRange;
						coupTargetY = yrc0.MinLimited();
					}
					catch { coupTargetY = off.y; }
					FreeAdvance(FreePhase.Coupling);
					break;
				case FreePhase.Coupling:
					logger.Info(
						"[FREECAL] run={0} DONE - kinematic map logged (yaw returned to {1:F1} deg). "
						+ "SCALE CAVEAT: every number here is valid for THIS character at scale={2:F4} "
						+ "and pene length {3:F4} only. Both are user-adjustable, so gains must be "
						+ "normalised by them rather than stored raw.",
						freeRun, freeYawAccum, PlayerScaleNow(), PeneLengthNow());
					freePhase = FreePhase.Done;
					// Signal completion LAST, after every level has been stored — a caller polling
					// this must never see "complete" while the surface is still half-written.
					BeTestControl.FreeCalRunning = false;
					BeTestControl.FreeCalComplete = true;
					logger.Info("[FREECAL] remote summary: {0}", BeTestControl.Summary);
					break;
			}
		}

		private enum ProbePhase
		{
			Idle,
			Baseline,
			CalYPlus,
			CalYMinus,
			CalXPlus,
			CalXMinus,
			YSweep,
			XSweep,
			GoTo,
			StrokeScan,
			Solve,
			Verify,
			Done,
			Aborted
		}

		private const float ProbeCalRate = 0.03f;      // command units per second during calibration
		private const float ProbeSolveRate = 0.02f;    // metres per SECOND of base travel during the solve
		private const float ProbeSweepRate = 0.05f;   // command units/sec during the y→angle sweep
		private static readonly float[] ProbePhaseSeconds =
			{ 0f, 2f, 0.8f, 0.8f, 0.8f, 0.8f, 12f, 12f, 10f, 15f, 8f, 3f };

		// StrokeScan: linear regression of shaft angle on DEPTH across several full strokes.
		// Everything before this is measured at whatever depth happened to prevail — run 2 sat at
		// d=0.101..0.103, a 2 mm window, so the deep end was never sampled at all. Slope tells us
		// whether misalignment is depth-INDEPENDENT (one y setpoint suffices) or grows with depth
		// (the stroke axis diverges from the hole axis and translation alone cannot fix it).
		private int scanN;
		private float scanSumD, scanSumA, scanSumDD, scanSumDA;
		private float scanDMin = float.MaxValue, scanDMax = float.MinValue;
		private float scanAngAtDMin, scanAngAtDMax;

		// Transfer-curve state. Reused by both sweeps; captured per axis when each ends.
		private float sweepMinAng = float.MaxValue, sweepMaxAng = float.MinValue;
		private float sweepYAtMin, sweepYAtMax;

		// MEASURED gains — degrees of shaft angle per command unit, and the y that minimised the
		// angle. NOT constants: authority goes roughly as 1/(L-d), so a longer pene gets less
		// angle per unit. Baking 186 deg/unit would be wrong for most characters, so GoTo uses
		// whatever this run measured.
		private float gainDegPerY, gainDegPerX;
		private float bestAngY, bestAngX;
		private float bestAngValue = float.MaxValue;

		private ProbePhase probePhase = ProbePhase.Idle;
		private float probePhaseT;
		private int probeRun;
		private Vector3 probePhaseStartBase;
		private Vector3 probeOriginBase;
		private Vector3 probeUy, probeUx;   // measured world direction per +unit of each command
		private float probeKy, probeKx;     // measured metres per commanded unit
		// DIFFERENTIAL calibration: the + and - phases are each contaminated by the stroke's own
		// motion of the base. That motion is common to both, so (plus - minus)/2 cancels it. Run 1
		// showed exactly this: Y flipped correctly while X/Z drifted the SAME way in both phases.
		private Vector3 probeMovedYPlus, probeMovedYMinus, probeMovedXPlus, probeMovedXMinus;
		private float probeBaselineEPerp, probeBaselineD, probeBaselineBend;
		private float probeEPerpSum, probeBendSum, probeDSum;
		private int probeSamples;

		private Vector3 PeneBase()
		{
			try { return base.Session.Player.Character.pene.@base.physicBone.position; }
			catch { return Vector3.zero; }
		}

		private Vector3 PeneTip()
		{
			try { return base.Session.Player.Character.pene.punta.physicBone.position; }
			catch { return Vector3.zero; }
		}

		/// <summary>e-perp from ALIGNMENT_THEORY §1: (E - a(L-d) - B), minus its axial part.</summary>
		private bool TryComputeEPerp(out Vector3 ePerp, out Vector3 axis, out float L, out float d)
		{
			ePerp = Vector3.zero; axis = Vector3.forward; L = 0f; d = 0f;
			try
			{
				if (Sequence == null || Sequence.HoleEntrance == null) return false;
				Penis pene = base.Session.Player.Character.pene;
				if (pene == null) return false;
				L = pene.worldLengthFromUnderSkin;
				d = pene.penetratingWorldLength;
				if (L <= 0.0001f) return false;
				Vector3 E = Sequence.HoleEntrance.position;
				axis = (-Sequence.HoleEntrance.forward).normalized;
				Vector3 B = PeneBase();
				Vector3 idealBase = E - axis * (L - d);
				Vector3 e = idealBase - B;
				ePerp = e - axis * Vector3.Dot(e, axis);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>Remaining travel on each pelvis axis, in COMMAND units, toward +/-.</summary>
		// Headroom uses the LIMITED range, not the raw min/max. Range exposes MinLimited()/
		// MaxLimited() precisely because the effective bounds are modifiable at runtime, and
		// CalculeAperture takes the whole target vector — so the reachable space is a coupled
		// envelope, not a fixed box. Reading raw min/max overstates the available travel.
		private void AxisHeadroom(out float xPlus, out float xMinus, out float yPlus, out float yMinus)
		{
			xPlus = xMinus = yPlus = yMinus = 0f;
			try
			{
				Vector3 cur = controllerOffsets.leftThighOffset;
				PelvisMovementController.Range xr = controller.xRange;
				PelvisMovementController.Range yr = controller.yRange;
				xPlus = Mathf.Max(0f, xr.MaxLimited() - cur.x);
				xMinus = Mathf.Max(0f, cur.x - xr.MinLimited());
				yPlus = Mathf.Max(0f, yr.MaxLimited() - cur.y);
				yMinus = Mathf.Max(0f, cur.y - yr.MinLimited());
			}
			catch
			{
			}
		}

		private void ProbeLog(string phase, float cmdX, float cmdY, Vector3 ePerp, Vector3 axis,
			float bend, float L, float d)
		{
			Vector3 B = PeneBase();
			Vector3 T = PeneTip();
			Vector3 E = (Sequence != null && Sequence.HoleEntrance != null)
				? Sequence.HoleEntrance.position : Vector3.zero;
			// Shaft direction vs hole axis — the independent check that the axis convention is
			// right. With bend ~1 % the shaft is straight, so this angle IS the misalignment.
			Vector3 shaft = T - B;
			float shaftAngle = (shaft.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaft, axis) : -1f;
			logger.Info(
				"[ALIGNTEST] run={0} phase={1} t={2:F2} B=({3:F4},{4:F4},{5:F4}) T=({6:F4},{7:F4},{8:F4}) "
				+ "E=({9:F4},{10:F4},{11:F4}) axis=({12:F3},{13:F3},{14:F3}) shaftAng={15:F1} "
				+ "d={16:F4} L={17:F4} ePerp=({18:F4},{19:F4},{20:F4}) ePerpMag={21:F4} bend={22:F4} "
				+ "cmdX={23:F5} cmdY={24:F5} dBase={25:F4}",
				probeRun, phase, probePhaseT, B.x, B.y, B.z, T.x, T.y, T.z, E.x, E.y, E.z,
				axis.x, axis.y, axis.z, shaftAngle, d, L,
				ePerp.x, ePerp.y, ePerp.z, ePerp.magnitude, bend, cmdX, cmdY,
				(B - probeOriginBase).magnitude);
		}

		private void ProbeAbort(string why)
		{
			logger.Info("[ALIGNTEST] run={0} ABORT reason={1} phase={2}", probeRun, why, probePhase);
			probePhase = ProbePhase.Aborted;
			probePhaseT = 0f;
		}

		private void ProbeAdvance(ProbePhase next)
		{
			probePhase = next;
			probePhaseT = 0f;
			probePhaseStartBase = PeneBase();
			probeEPerpSum = 0f; probeBendSum = 0f; probeDSum = 0f; probeSamples = 0;
		}

		private void UpdateAlignProbe()
		{
			if (!AlignTest)
			{
				if (probePhase != ProbePhase.Idle) probePhase = ProbePhase.Idle;
				return;
			}
			if (Sequence == null) return;

			float dt = Time.deltaTime;

			if (probePhase == ProbePhase.Idle)
			{
				probeRun++;
				probeOriginBase = PeneBase();
				probeUy = Vector3.zero; probeUx = Vector3.zero; probeKy = 0f; probeKx = 0f;
				logger.Info("[ALIGNTEST] run={0} START", probeRun);
				ProbeAdvance(ProbePhase.Baseline);
				return;
			}
			if (probePhase == ProbePhase.Done || probePhase == ProbePhase.Aborted) return;

			if (!TryComputeEPerp(out Vector3 ePerp, out Vector3 axis, out float L, out float d))
			{
				ProbeAbort("no-geometry");
				return;
			}
			if (d <= 0.0005f)
			{
				ProbeAbort("not-penetrating");
				return;
			}
			// No fixed travel leash any more: run 1 aborted on it while pursuing a legitimate
			// 10 cm error. Reach is now bounded by the pelvis's OWN range headroom, checked below.

			float bend = BendDeflection;
			probePhaseT += dt;
			probeEPerpSum += ePerp.magnitude; probeBendSum += bend; probeDSum += d; probeSamples++;

			float cmdX = 0f, cmdY = 0f;
			switch (probePhase)
			{
				case ProbePhase.Baseline:
					break;
				case ProbePhase.CalYPlus:
					cmdY = ProbeCalRate * dt;
					break;
				case ProbePhase.CalYMinus:
					cmdY = 0f - ProbeCalRate * dt;
					break;
				case ProbePhase.CalXPlus:
					cmdX = ProbeCalRate * dt;
					break;
				case ProbePhase.CalXMinus:
					cmdX = 0f - ProbeCalRate * dt;
					break;
				case ProbePhase.YSweep:
				{
					// Y→ANGLE TRANSFER CURVE. PelvisMovementController has NO rotation API, but its
					// CalculeAperture maps |y| to an aperture angle up to maxAperture (50°),
					// saturating at half the y range — so vertical is an angular control in
					// disguise. Calibration measured only where the BASE moved and would have
					// missed that entirely. Ramp y across its range and record the SHAFT angle at
					// each point; the slope is the real angular authority per unit of y.
					float dir = (probePhaseT < 3f) ? 1f : ((probePhaseT < 9f) ? -1f : 1f);
					AxisHeadroom(out float hxP, out float hxM, out float hyP, out float hyM);
					cmdY = dir * ProbeSweepRate * dt;
					cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, hyP) : Mathf.Max(cmdY, -hyM);

					float yNow = 0f;
					float aperture = 0f;
					try
					{
						yNow = controllerOffsets.leftThighOffset.y;
						// The game's own formula, reproduced so the log carries its value directly.
						float f = Mathf.Abs(controller.yRange.MinLimited() * 0.5f);
						aperture = (f > 1e-6f) ? Mathf.Clamp(50f * Mathf.Abs(yNow) / f, 0f, 50f) : 0f;
					}
					catch
					{
					}

					Vector3 shaftV = PeneTip() - PeneBase();
					float shaftAng = (shaftV.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaftV, axis) : -1f;
					if (shaftAng >= 0f)
					{
						if (shaftAng < sweepMinAng) { sweepMinAng = shaftAng; sweepYAtMin = yNow; }
						if (shaftAng > sweepMaxAng) { sweepMaxAng = shaftAng; sweepYAtMax = yNow; }
					}
					logger.Info(
						"[ALIGNTEST] run={0} YSWEEP t={1:F2} y={2:F4} aperture={3:F1} shaftAng={4:F1} "
						+ "ePerpMag={5:F4} d={6:F4} bend={7:F4} cmdY={8:F5}",
						probeRun, probePhaseT, yNow, aperture, shaftAng, ePerp.magnitude, d, bend, cmdY);
					break;
				}
				case ProbePhase.XSweep:
				{
					// Same treatment for lateral. Run 1's differential calibration for X was badly
					// contaminated by the stroke (rawDot +0.66, axes 66 deg apart instead of 90);
					// a slow sweep against the MEASURED shaft angle avoids that entirely.
					float dirx = (probePhaseT < 3f) ? 1f : ((probePhaseT < 9f) ? -1f : 1f);
					AxisHeadroom(out float sxP, out float sxM, out float syP, out float syM);
					cmdX = dirx * ProbeSweepRate * dt;
					cmdX = (cmdX >= 0f) ? Mathf.Min(cmdX, sxP) : Mathf.Max(cmdX, -sxM);

					float xNow = 0f;
					try { xNow = controllerOffsets.leftThighOffset.x; } catch { }
					Vector3 shaftVx = PeneTip() - PeneBase();
					float shaftAngX = (shaftVx.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaftVx, axis) : -1f;
					if (shaftAngX >= 0f)
					{
						if (shaftAngX < sweepMinAng) { sweepMinAng = shaftAngX; sweepYAtMin = xNow; }
						if (shaftAngX > sweepMaxAng) { sweepMaxAng = shaftAngX; sweepYAtMax = xNow; }
					}
					logger.Info(
						"[ALIGNTEST] run={0} XSWEEP t={1:F2} x={2:F4} shaftAng={3:F1} ePerpMag={4:F4} "
						+ "d={5:F4} bend={6:F4} cmdX={7:F5}",
						probeRun, probePhaseT, xNow, shaftAngX, ePerp.magnitude, d, bend, cmdX);
					break;
				}
				case ProbePhase.GoTo:
				{
					// DEMONSTRATION: drive the shaft angle to zero using the gain THIS run measured.
					// One dimension at a time, proportional, deadbanded, rate-limited. The sign is
					// the sign of the error — no learning, because the sweep established polarity.
					Vector3 shaftG = PeneTip() - PeneBase();
					float ang = (shaftG.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaftG, axis) : 0f;
					AxisHeadroom(out float gxP, out float gxM, out float gyP, out float gyM);
					float towardY = 0f;
					try { towardY = bestAngY - controllerOffsets.leftThighOffset.y; } catch { }
					if (ang > 2f && Mathf.Abs(towardY) > 0.002f)
					{
						cmdY = Mathf.Clamp(towardY, -ProbeSweepRate * dt, ProbeSweepRate * dt);
						cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, gyP) : Mathf.Max(cmdY, -gyM);
					}
					logger.Info(
						"[ALIGNTEST] run={0} GOTO t={1:F2} shaftAng={2:F1} targetY={3:F4} towardY={4:F4} "
						+ "cmdY={5:F5} ePerpMag={6:F4} d={7:F4} bend={8:F4}",
						probeRun, probePhaseT, ang, bestAngY, towardY, cmdY, ePerp.magnitude, d, bend);
					break;
				}
				case ProbePhase.StrokeScan:
				{
					// Hold the aligned y and let the stroke run its FULL range, recording how the
					// shaft angle varies with depth. No correction beyond holding y — the point is
					// to observe, not to steer.
					float towardYs = 0f;
					try { towardYs = bestAngY - controllerOffsets.leftThighOffset.y; } catch { }
					if (Mathf.Abs(towardYs) > 0.002f)
					{
						AxisHeadroom(out float kxP, out float kxM, out float kyP, out float kyM);
						cmdY = Mathf.Clamp(towardYs, -ProbeSweepRate * dt, ProbeSweepRate * dt);
						cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, kyP) : Mathf.Max(cmdY, -kyM);
					}

					Vector3 shaftS = PeneTip() - PeneBase();
					float angS = (shaftS.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaftS, axis) : -1f;
					if (angS >= 0f)
					{
						scanN++;
						scanSumD += d; scanSumA += angS; scanSumDD += d * d; scanSumDA += d * angS;
						if (d < scanDMin) { scanDMin = d; scanAngAtDMin = angS; }
						if (d > scanDMax) { scanDMax = d; scanAngAtDMax = angS; }
					}
					logger.Info(
						"[ALIGNTEST] run={0} SCAN t={1:F2} d={2:F4} shaftAng={3:F1} ePerpMag={4:F4} "
						+ "bend={5:F4} cmdY={6:F5}",
						probeRun, probePhaseT, d, angS, ePerp.magnitude, bend, cmdY);
					break;
				}
				case ProbePhase.Solve:
				{
					// Project e-perp onto the MEASURED basis, convert to command units by the
					// MEASURED scale, then rate-limit by base travel per second and clamp to the
					// axis headroom that actually remains. Nothing here assumes a frame or a unit.
					AxisHeadroom(out float xPlus, out float xMinus, out float yPlus, out float yMinus);
					float maxTravelThisTick = ProbeSolveRate * dt;

					if (probeKy > 1e-6f && probeUy.sqrMagnitude > 1e-9f)
					{
						float wantM = Vector3.Dot(ePerp, probeUy.normalized);
						float stepM = Mathf.Clamp(wantM, -maxTravelThisTick, maxTravelThisTick);
						cmdY = stepM / probeKy;
						cmdY = (cmdY >= 0f) ? Mathf.Min(cmdY, yPlus) : Mathf.Max(cmdY, -yMinus);
					}
					if (probeKx > 1e-6f && probeUx.sqrMagnitude > 1e-9f)
					{
						float wantM2 = Vector3.Dot(ePerp, probeUx.normalized);
						float stepM2 = Mathf.Clamp(wantM2, -maxTravelThisTick, maxTravelThisTick);
						cmdX = stepM2 / probeKx;
						cmdX = (cmdX >= 0f) ? Mathf.Min(cmdX, xPlus) : Mathf.Max(cmdX, -xMinus);
					}
					break;
				}
				case ProbePhase.Verify:
					break;
			}

			if (cmdY != 0f) controller.AddVerticalDelta(cmdY);
			if (cmdX != 0f) controller.AddHorizontalDelta(cmdX);
			ProbeLog(probePhase.ToString(), cmdX, cmdY, ePerp, axis, bend, L, d);

			int idx = (int)probePhase;
			float limit = (idx >= 0 && idx < ProbePhaseSeconds.Length) ? ProbePhaseSeconds[idx] : 1f;
			if (probePhaseT < limit) return;

			// Phase complete — record what it measured, then advance.
			Vector3 moved = PeneBase() - probePhaseStartBase;
			float meanE = (probeSamples > 0) ? probeEPerpSum / probeSamples : 0f;
			float meanBend = (probeSamples > 0) ? probeBendSum / probeSamples : 0f;
			float meanD = (probeSamples > 0) ? probeDSum / probeSamples : 0f;
			float commanded = ProbeCalRate * limit;

			logger.Info(
				"[ALIGNTEST] run={0} SUMMARY phase={1} moved=({2:F4},{3:F4},{4:F4}) |moved|={5:F4} "
				+ "commanded={6:F4} meanEPerp={7:F4} meanBend={8:F4} meanD={9:F4}",
				probeRun, probePhase, moved.x, moved.y, moved.z, moved.magnitude,
				commanded, meanE, meanBend, meanD);

			switch (probePhase)
			{
				case ProbePhase.Baseline:
					probeBaselineEPerp = meanE; probeBaselineD = meanD; probeBaselineBend = meanBend;
					ProbeAdvance(ProbePhase.CalYPlus);
					break;
				case ProbePhase.CalYPlus:
					probeMovedYPlus = moved;
					ProbeAdvance(ProbePhase.CalYMinus);
					break;
				case ProbePhase.CalYMinus:
				{
					probeMovedYMinus = moved;
					// DIFFERENTIAL: (plus - minus)/2 removes whatever the stroke contributed to
					// both. Raw dot is logged too so the contamination remains visible.
					probeUy = (probeMovedYPlus - probeMovedYMinus) * 0.5f;
					probeKy = probeUy.magnitude / Mathf.Max(1e-6f, commanded);
					logger.Info(
						"[ALIGNTEST] run={0} CAL-Y rawDot={1:F3} diffDir=({2:F4},{3:F4},{4:F4}) kY={5:F4} "
						+ "(P1 wants a clean single-axis diffDir, P2 wants kY~1)",
						probeRun, Vector3.Dot(probeMovedYPlus.normalized, probeMovedYMinus.normalized),
						probeUy.normalized.x, probeUy.normalized.y, probeUy.normalized.z, probeKy);
					ProbeAdvance(ProbePhase.CalXPlus);
					break;
				}
				case ProbePhase.CalXPlus:
					probeMovedXPlus = moved;
					ProbeAdvance(ProbePhase.CalXMinus);
					break;
				case ProbePhase.CalXMinus:
				{
					probeMovedXMinus = moved;
					probeUx = (probeMovedXPlus - probeMovedXMinus) * 0.5f;
					probeKx = probeUx.magnitude / Mathf.Max(1e-6f, commanded);
					logger.Info(
						"[ALIGNTEST] run={0} CAL-X rawDot={1:F3} diffDir=({2:F4},{3:F4},{4:F4}) kX={5:F4} "
						+ "axisAngle={6:F1}deg (wants ~90 for independent axes)",
						probeRun, Vector3.Dot(probeMovedXPlus.normalized, probeMovedXMinus.normalized),
						probeUx.normalized.x, probeUx.normalized.y, probeUx.normalized.z, probeKx,
						Vector3.Angle(probeUx, probeUy));

					// REACHABILITY, computed before attempting anything: how much command travel
					// does e-perp demand on each measured axis, and how much headroom is left?
					AxisHeadroom(out float xPlus, out float xMinus, out float yPlus, out float yMinus);
					float needY = (probeKy > 1e-6f) ? Vector3.Dot(ePerp, probeUy.normalized) / probeKy : 0f;
					float needX = (probeKx > 1e-6f) ? Vector3.Dot(ePerp, probeUx.normalized) / probeKx : 0f;
					float availY = (needY >= 0f) ? yPlus : yMinus;
					float availX = (needX >= 0f) ? xPlus : xMinus;
					logger.Info(
						"[ALIGNTEST] run={0} REACH ePerpMag={1:F4} needY={2:F4} availY={3:F4} "
						+ "needX={4:F4} availX={5:F4} verdict={6}",
						probeRun, ePerp.magnitude, needY, availY, needX, availX,
						(Mathf.Abs(needY) <= availY && Mathf.Abs(needX) <= availX)
							? "REACHABLE" : "UNREACHABLE-by-hips");
					sweepMinAng = float.MaxValue; sweepMaxAng = float.MinValue;
					ProbeAdvance(ProbePhase.YSweep);
					break;
				}
				case ProbePhase.YSweep:
				{
					float angSpan = (sweepMaxAng > sweepMinAng) ? (sweepMaxAng - sweepMinAng) : 0f;
					float ySpan = Mathf.Abs(sweepYAtMax - sweepYAtMin);
					gainDegPerY = (ySpan > 1e-5f) ? angSpan / ySpan : 0f;
					bestAngY = sweepYAtMin;
					bestAngValue = sweepMinAng;
					// LENGTH NORMALISATION: authority goes roughly as 1/(L-d), so the gain is only
					// meaningful alongside the geometry it was measured at. Logged together so the
					// relationship can be confirmed across characters of very different length.
					logger.Info(
						"[ALIGNTEST] run={0} YSWEEP-RESULT minAng={1:F1}deg@y={2:F4} maxAng={3:F1}deg@y={4:F4} "
						+ "angSpan={5:F1}deg ySpan={6:F4} degPerUnitY={7:F1} L={8:F4} d={9:F4} lever={10:F4} "
						+ "degPerUnitY_x_lever={11:F2} (that product should be ~constant across penes)",
						probeRun, sweepMinAng, sweepYAtMin, sweepMaxAng, sweepYAtMax,
						angSpan, ySpan, gainDegPerY, L, d, L - d, gainDegPerY * (L - d));
					sweepMinAng = float.MaxValue; sweepMaxAng = float.MinValue;
					ProbeAdvance(ProbePhase.XSweep);
					break;
				}
				case ProbePhase.XSweep:
				{
					float angSpanX = (sweepMaxAng > sweepMinAng) ? (sweepMaxAng - sweepMinAng) : 0f;
					float xSpan = Mathf.Abs(sweepYAtMax - sweepYAtMin);
					gainDegPerX = (xSpan > 1e-5f) ? angSpanX / xSpan : 0f;
					bestAngX = sweepYAtMin;
					logger.Info(
						"[ALIGNTEST] run={0} XSWEEP-RESULT minAng={1:F1}deg@x={2:F4} maxAng={3:F1}deg@x={4:F4} "
						+ "angSpan={5:F1}deg xSpan={6:F4} degPerUnitX={7:F1} "
						+ "(compare with degPerUnitY - which axis has the authority?)",
						probeRun, sweepMinAng, sweepYAtMin, sweepMaxAng, sweepYAtMax,
						angSpanX, xSpan, gainDegPerX);
					ProbeAdvance(ProbePhase.GoTo);
					break;
				}
				case ProbePhase.GoTo:
				{
					Vector3 shaftEnd = PeneTip() - PeneBase();
					float endAng = (shaftEnd.sqrMagnitude > 1e-9f) ? Vector3.Angle(shaftEnd, axis) : -1f;
					logger.Info(
						"[ALIGNTEST] run={0} GOTO-RESULT endShaftAng={1:F1}deg sweepMinAng={2:F1}deg "
						+ "targetY={3:F4} meanEPerp={4:F4} meanBend={5:F4} "
						+ "(success = endShaftAng near sweepMinAng)",
						probeRun, endAng, bestAngValue, bestAngY, meanE, meanBend);
					scanN = 0; scanSumD = scanSumA = scanSumDD = scanSumDA = 0f;
					scanDMin = float.MaxValue; scanDMax = float.MinValue;
					ProbeAdvance(ProbePhase.StrokeScan);
					break;
				}
				case ProbePhase.StrokeScan:
				{
					// Least-squares slope of shaftAngle vs depth.
					float slope = 0f;
					if (scanN > 2)
					{
						float denom = scanN * scanSumDD - scanSumD * scanSumD;
						if (Mathf.Abs(denom) > 1e-9f)
							slope = (scanN * scanSumDA - scanSumD * scanSumA) / denom;
					}
					float dRange = (scanDMax > scanDMin) ? (scanDMax - scanDMin) : 0f;
					logger.Info(
						"[ALIGNTEST] run={0} SCAN-RESULT n={1} dMin={2:F4} dMax={3:F4} dRange={4:F4} "
						+ "angAtShallow={5:F1} angAtDeep={6:F1} slope={7:F1}deg/m predictedRise={8:F1}deg "
						+ "(dRange<0.02 = stroke was NOT running, result void; "
						+ "slope~0 = alignment holds at all depths, one y setpoint is enough; "
						+ "slope>0 = stroke axis diverges from hole axis, translation alone cannot fix it)",
						probeRun, scanN, scanDMin, scanDMax, dRange,
						scanAngAtDMin, scanAngAtDMax, slope, slope * dRange);
					ProbeAdvance(ProbePhase.Solve);
					break;
				}
				case ProbePhase.Solve:
					logger.Info(
						"[ALIGNTEST] run={0} SOLVE-RESULT baselineEPerp={1:F4} endEPerp={2:F4} ratio={3:F2} "
						+ "baselineBend={4:F4} endBend={5:F4} baselineD={6:F4} endD={7:F4} dDelta={8:F1}% "
						+ "(P3 wants ratio<0.30, P4 wants bend down, P5 wants |dDelta|<10%)",
						probeRun, probeBaselineEPerp, meanE,
						meanE / Mathf.Max(1e-6f, probeBaselineEPerp),
						probeBaselineBend, meanBend, probeBaselineD, meanD,
						100f * (meanD - probeBaselineD) / Mathf.Max(1e-6f, probeBaselineD));
					ProbeAdvance(ProbePhase.Verify);
					break;
				case ProbePhase.Verify:
					logger.Info("[ALIGNTEST] run={0} VERIFY meanEPerp={1:F4} (P6 wants it to hold) DONE",
						probeRun, meanE);
					probePhase = ProbePhase.Done;
					break;
			}
		}

		private float FloorFraction()
		{
			// The two spaces need DIFFERENT bases. In pene space the floor is tip + range*frac, and
			// the tip term IS the pop-out threshold, so 3 % above it is still safely inside. In hole
			// space zero means FULLY OUT — there is no tip offset — so the same 3 % pulls almost
			// all the way out. Hole space therefore carries the pop-out margin in the fraction
			// itself (15 %, the owner's original figure, which was right for this space).
			float baseFrac;
			if (HoleDepthCapacity() > 0f)
			{
				// TIP RESERVE. Natural withdrawal keeps the TIP inside, so the floor should sit at
				// the tip rather than at an arbitrary fraction — but only when the tip is a modest
				// share of what the hole has. In a very shallow hole the tip IS most of the
				// available depth, and reserving it would leave no stroke at all; there, coming
				// nearly all the way out is both necessary and correct.
				float tipFrac = TipFractionOfCapacity();
				baseFrac = (tipFrac > 0f && tipFrac <= TipReserveMaxFrac)
					? tipFrac
					: ((tipFrac > TipReserveMaxFrac) ? ShallowHoleFloorFrac : HoleFloorFrac);
			}
			else
			{
				// Pene space already anchors on the tip (floor = tip + range * frac).
				baseFrac = PopoutFloorFrac;
			}
			float frac = Mathf.Max(baseFrac, Mathf.Clamp01(UserBackwardTarget));
			float speed = (Sequence != null) ? Sequence.Velocity : MinVelocity;
			float speedMargin = Mathf.Lerp(0f, PopoutSpeedMargin,
				Mathf.InverseLerp(MinVelocity, MaxVelocity, speed));
			return Mathf.Clamp01(frac + speedMargin);
		}

		// ── BEND LIMIT ────────────────────────────────────────────────────────────────────
		// Pressing too hard bows the shaft. The game measures this itself, exactly, with a
		// matched pair on Penetrador:
		//     worldLengthFromUnderSkin          — the UNBENT reference (from the straight pose)
		//     realCurrentWorldLengthFromUnderSkin — the live base→tip chord
		//     deflection = 1 - real / ideal      (0 = perfectly straight)
		// This is better than GetDeformationFactor, which compares physicBone positions against
		// the erection-scaled worldLength and needs an InverseLerp fudge to compensate.
		//
		// STIFFNESS NEEDS NO SPECIAL CASE. Deflection ≈ force / stiffness, and stiffness is
		// Penetrador.rigidez (0.1–10, per character, set by the male alteradores). Governing on
		// MEASURED deflection therefore self-adjusts: a soft pene reaches the limit at a lower
		// force and gets throttled sooner, a stiff one is allowed to push harder. rigidez is read
		// only to report the implied force (deflection × rigidez).
		//
		// MaxBendFraction is the setpoint — the Mission Control "Max bend" slider.
		// Default owner-tuned in game, 2026-08-07: 20 % felt right. Not a guess — do not "improve".
		public float MaxBendFraction { get; set; } = 0.2f;

		// PUNCH — deliberate over-reach PAST Forward 100 %, as a fraction of the usable range
		// (0 = none, 0.3 = 130 % of range). Forward 100 % is "fully seated"; punch is the
		// opted-into push beyond it, and because it is a slider the over-reach is always a user
		// choice rather than a side effect. Bounds the anatomical ceiling too, so the limit
		// scales with the same intent instead of vetoing it.
		// Default owner-tuned in game, 2026-08-07: +20 % felt right (slider allows up to +30 %).
		public float PunchFraction { get; set; } = 0.2f;

		private const float PunchMax = 0.3f;

		// ADAPTIVE PUNCH BACKOFF.
		// Punch is over-reach past "fully seated", so it is exactly the setting that should yield
		// when the body says there is nothing left to give. Two independent signals, both live:
		//   • BENDING NEAR THE CEILING — bending anywhere is handled by the bend throttle, but
		//     bending while already at depth means the punch itself is what is doing the bending.
		//   • RUNNING OUT OF SHAFT — worldLengthFromUnderSkin - penetratingWorldLength is the
		//     length still outside her (the game computes the same difference in PenisPart). As
		//     that headroom approaches zero the pene is as deep as its own length allows, and more
		//     punch can only press the base rather than gain depth.
		// Decays fast, recovers slowly: backing off is the safe direction, and a punch that
		// snapped back would just re-trigger the same collision every stroke.
		private const float PunchBackoffPerSec = 1.5f;

		private const float PunchGrowPerSec = 0.15f;

		// Headroom = the share of the shaft still OUTSIDE her. Below the first figure the pene is
		// as deep as its own length allows; above the second there is plenty left to give.
		private const float PunchHeadroomTight = 0.12f;

		private const float PunchHeadroomAmple = 0.3f;

		// The slider is the NOMINAL punch; this scales it. It may exceed 1 — the whole point is
		// that a long pene in a deep hole with no bending should be allowed to use more of itself
		// than the slider's baseline. The hard ceiling stays PunchMax (+30 %).
		private const float PunchScaleMax = 2f;

		private float punchScale = 1f;

		private void UpdatePunchAdaptive()
		{
			float dt = Time.deltaTime;
			float setpoint = Mathf.Max(0.005f, MaxBendFraction);
			float bend = BendDeflection;
			bool nearCeiling = GetPenetrationFactor() >= Mathf.Clamp01(UserForwardTarget) * 0.9f;
			bool bendingDeep = nearCeiling && bend > setpoint;

			float headroom = 1f;
			try
			{
				Penis pene = base.Session.Player.Character.pene;
				float ideal = pene.worldLengthFromUnderSkin;
				if (ideal > 0.0001f)
				{
					headroom = Mathf.Clamp01((ideal - pene.penetratingWorldLength) / ideal);
				}
			}
			catch
			{
				headroom = 1f;
			}

			if (bendingDeep || headroom < PunchHeadroomTight)
			{
				// Out of shaft, or the punch itself is doing the bending — yield, quickly.
				punchScale = Mathf.Max(0f, punchScale - PunchBackoffPerSec * dt);
			}
			else if (headroom > PunchHeadroomAmple && bend < setpoint * 0.5f)
			{
				// Plenty of pene left and nothing complaining — reach for more, slowly.
				punchScale = Mathf.Min(PunchScaleMax, punchScale + PunchGrowPerSec * dt);
			}
			// Otherwise hold: the middle band is neither limited nor comfortably clear, and
			// drifting there would just hunt.

			logger.InfoRare(120,
				"[AutoThrust/punch] scale={0:F2} eff={1:F3} headroom={2:F2} bend={3:F3} nearCeiling={4}",
				punchScale, EffectivePunch(), headroom, bend, nearCeiling);
		}

		/// <summary>Punch actually in force: slider × adaptive scale, hard-capped at PunchMax.</summary>
		private float EffectivePunch()
		{
			return Mathf.Clamp(Mathf.Clamp(PunchFraction, 0f, PunchMax) * punchScale, 0f, PunchMax);
		}

		private float TotalForwardFraction()
		{
			return Mathf.Clamp01(UserForwardTarget) + EffectivePunch();
		}

		// Recovery (withdraw until straight) is the extreme end of the SAME measure, so the one
		// slider governs both: enter at 2x the allowed bend, leave at half of it.
		private const float BendRecoverEnterMult = 2f;

		private const float BendRecoverExitMult = 0.5f;

		private const float BendSpeedBackoff = 0.6f;

		private const float BendSpeedFloor = 0.25f;

		private const float BendSpeedRecoverPerSec = 0.15f;

		private const float BendThrottleFloor = 0.1f;

		private bool bendRecovering;

		private float bendSpeedScale = 1f;

		/// <summary>Reference length treated as "straight". Rises instantly, decays slowly — so it
		/// FORGETS, which a fixed nominal length cannot.</summary>
		private float bendIdealRef;

		/// <summary>How long a sustained bend takes to stop counting as bend. This constant IS the
		/// ratchet fix.</summary>
		private const float BendRefDecaySeconds = 2.5f;

		/// <summary>Undecayed deflection, kept for diagnostics: it is what the old measure would
		/// have reported, so the two can be logged side by side.</summary>
		public float BendDeflectionRaw
		{
			get
			{
				try
				{
					Penis pene = base.Session.Player.Character.pene;
					float ideal = pene.worldLengthFromUnderSkin;
					if (ideal <= 0.0001f) return 0f;
					return Mathf.Clamp01(1f - pene.realCurrentWorldLengthFromUnderSkin / ideal);
				}
				catch
				{
					return 0f;
				}
			}
		}

		/// <summary>
		/// Live bend, 0 = straight — measured against a DECAYING reference.
		///
		/// THE RATCHET, AND WHY IT WAS A MEASUREMENT BUG. This was `1 - realCurrent / nominal`: a
		/// ratio against a FIXED length. That quantity is total DEFORMATION, not current
		/// deflection, and the two diverge the moment the shaft holds any persistent compression —
		/// the reading stays high while the shaft looks straight, the bend throttle stays clamped,
		/// bendSpeedScale sits on its 0.25 floor, and the stroke is permanently slow. The audit
		/// caught the signature: bendPeak rising monotonically across a whole run INCLUDING inside
		/// the all-features-off baseline arm. Nothing was changing behaviour; the ruler was drifting.
		///
		/// Now the reference rises INSTANTLY to any longer observed length — the shaft
		/// straightening is itself the evidence of what straight means — and otherwise decays
		/// toward the current length over BendRefDecaySeconds. A real bend still reads immediately;
		/// a bend that persists relaxes back toward zero. Capped at the game's nominal length,
		/// because a reference above that would be treating stretch as the new straight and would
		/// inflate every subsequent reading.
		/// </summary>
		public float BendDeflection
		{
			get
			{
				try
				{
					Penis pene = base.Session.Player.Character.pene;
					float cur = pene.realCurrentWorldLengthFromUnderSkin;
					float nominal = pene.worldLengthFromUnderSkin;
					if (nominal <= 0.0001f || cur <= 0.0001f) return 0f;

					// Seed from the nominal so the first reading is sane rather than zero-until-warm.
					if (bendIdealRef <= 0.0001f) bendIdealRef = nominal;

					if (cur > bendIdealRef) bendIdealRef = Mathf.Min(cur, nominal);
					else bendIdealRef = Mathf.Lerp(bendIdealRef, cur,
						Mathf.Clamp01(Time.deltaTime / BendRefDecaySeconds));

					if (bendIdealRef <= 0.0001f) return 0f;
					return Mathf.Clamp01(1f - cur / bendIdealRef);
				}
				catch
				{
					return 0f;
				}
			}
		}

		// ── HIP ALIGNMENT ASSIST ──────────────────────────────────────────────────────────
		// BEND HAS TWO CAUSES and they need OPPOSITE responses:
		//   • force bend       — pressing into the wall. Slow down.
		//   • misalignment bend — the shaft levering on the rim because the pene axis and the
		//                         hole axis disagree. Slowing down fixes nothing; it just makes a
		//                         bad angle slower. The angle has to change.
		// The bend throttle alone conflates them, so a hole at (say) 45° would sit permanently
		// throttled AND permanently bent. This corrects the angle with the same actuator the C/V
		// keys use, and SUPPRESSES the throttle while it is converging — because the bend it would
		// react to is the one alignment is about to remove.
		//
		// Angle maths and the solvability bound are AutoSeeker's (FromToAxisAngle against the
		// root-motion right axis; MAX_SOLVABLE_V_ANGLE = 80). Beyond that the geometry cannot be
		// fixed by hips alone and we fall back to throttling rather than pushing forever.
		public bool AlignHips { get; set; }

		public bool AlignLateral { get; set; }

		public float AlignGain { get; set; } = 0.5f;

		// SEPARATING STROKE MOTION FROM POSITIONING ERROR.
		// The measured angle has two components. The pelvis is MOVING as part of the stroke, so a
		// large part of the swing is the thrust itself — oscillatory, roughly zero-mean over a
		// full stroke, and emphatically not a misalignment. A genuine standing/height error is
		// quasi-DC and survives averaging. Correcting on the instantaneous angle would chase the
		// stroke's own motion and could resonate with it, so both correctors read a slow average
		// spanning several strokes. The instantaneous value is kept for logging only, so the two
		// can be compared when something looks wrong.
		private const float AlignSlowTau = 1.5f;

		private float vangleSlow;

		private float hangleSlow;

		private bool alignSlowPrimed;

		private const float AlignDeadbandDeg = 5f;

		private const float AlignMaxSolvableDeg = 80f;

		// Measured 2026-08-07: at 0.03 with authority scaled by /80°, a real 10° error produced
		// steps of ~1e-4 and vSlow never moved off 10° — far below what the pelvis needs to shift.
		// TWO REGIMES. Getting into place and holding position are different problems:
		//   ACQUIRE — error is large, nothing is established yet, and the pose is visibly wrong.
		//             Move decisively; a slow approach just prolongs a bad angle.
		//   HOLD    — already aligned within tolerance. Corrections here are small and should be
		//             heavily smoothed: at this scale the residual is mostly the stroke's own
		//             motion, and chasing it produces jitter and fights the thrust.
		// Hysteresis between the two so it cannot flap on the boundary.
		private const float AlignAcquireRatePerSec = 0.25f;

		private const float AlignHoldRatePerSec = 0.05f;

		private const float AlignAcquireAboveDeg = 8f;

		private const float AlignHoldBelowDeg = 4f;

		private bool alignAcquiring = true;

		// ══ Z-CENTRE PITCH CONTROL (ALIGNMENT_CAPABILITY_MAP §6) ═════════════════════════════
		// Measured free-space pitch authority: z = 68 deg over its range, y = 22 deg, x ~ 0.
		// DEPTH is the pitch lever, roughly 3x height — and y is ONE-SIDED from the default pose
		// (range [-1.6, 0] with the character starting at 0, so there is no "up" at all, which is
		// why hip-raising never appeared to work). z is two-sided: [-0.5, +0.475].
		//
		// The decomposition is `z_centre + stroke(t)`: bias the pelvis along z and let the stroke
		// ride on top. This does NOT fight the thrust, because the stroke's reversal points are
		// measured in DEPTH, not in command units — bias the pelvis forward and it simply reaches
		// the same depths from a different z, which is exactly the pitch change we want.
		private const float AlignZAcquireRatePerSec = 0.12f;

		private const float AlignZHoldRatePerSec = 0.03f;

		private const float AlignZBiasMax = 0.22f;

		// From the free-space map: pitch RISES with z (-20 deg at z=-0.50 → +48 deg at z=+0.48),
		// i.e. ~69 deg per unit. In-hole the pivot makes it stronger still, so this is a
		// conservative divisor and the residual is handled by the loop, not by a guess.
		private const float AlignDegPerUnitZ = 69f;

		private float alignZBias;

		// ══ COARSE STAGE — avatar placement, hips as the fine-tuner ══════════════════════════
		// Owner's structure: Session.Player.Move does the GROSS work — bring the pene base within
		// tolerance of the hole's axis while the hips sit near NEUTRAL — and the pelvis deltas
		// then fine-tune from a centred, two-sided starting point.
		//
		// This is what makes avatar movement safe here, where the earlier lateral corrector was
		// not. That one had an open-ended objective ("reduce this angle") and so translated
		// forever; this one has a BOUNDED, self-limiting target: it stops the moment |e-perp|
		// is under tolerance, and the residual hip offset it is nulling is itself bounded. It
		// also preserves fine authority — a saturated hip axis has no trim left to give.
		public bool AlignCoarse { get; set; }

		private const float CoarseTolerance = 0.04f;     // metres of e-perp considered "close enough"
		private const float CoarseRatePerSec = 0.05f;    // avatar metres/sec — deliberately slow
		private const float CoarseMaxTotal = 0.40f;      // hard cap on total avatar displacement
		private const float CoarseHipNeutralFrac = 0.25f; // hips within this fraction of range = neutral

		private float coarseTotal;

		/// <summary>Longest coarse may monopolise the alignment DOF before the fine stage runs
		/// anyway. Its own rate (0.05 m/s) closes the 4 cm tolerance in about a second, so three
		/// seconds is already generous — beyond that it is not converging, it is hogging.</summary>
		private const float CoarseMaxHoldSeconds = 3f;

		private float coarseHoldT;
		private bool coarseActive;

		// ══ THE LINE CAST (owner, 2026-08-07) ════════════════════════════════════════════════
		// Cast a straight line OUT of the hole along its axis. The pene base belongs on that
		// line, exactly HALF the pene's length from the entrance — i.e. the stroke centres at
		// 50 % insertion.
		//
		//     outward = +HoleEntrance.forward          (the axis, pointing out of her)
		//     target  = E + outward * (L * 0.5)
		//     error   = target - B
		//
		// This replaces `B_ideal = E - axis*(L - d)`, which depended on the CURRENT depth: as the
		// stroke oscillated, that target oscillated with it and the corrector chased its own
		// stroke. L is constant, so this target is stationary and the error is a real pose error.
		//
		// The error is then split against the same axis:
		//   axial component → the stroke's z centre  (too deep / not deep enough)
		//   perpendicular   → x and y hip trim       (off the line)
		// and both are read from a SLOW average, so the stroke's own excursion averages out
		// rather than being corrected against.
		/// <summary>Superseded by StrokeLength(), which takes the SMALLER of the pene's usable
		/// length and the partner's available depth. Keeping half-a-pene-length here as well meant
		/// the solver aimed at one station while the line cast, the audit and the ANGLE readout
		/// drew another — the instrument disagreeing with the thing it measures. Retained only as
		/// the documented fraction; every call site now goes through StrokeLength().</summary>
		private const float BaseTargetInsertionFrac = 0.5f;

		// ══ STRAIGHT-LINE STROKE ═════════════════════════════════════════════════════════════
		// Placing the base on the line is not enough — the stroke must TRAVEL along that line.
		// A depth command alone moves the pelvis along its own z, which is only the hole's axis
		// by coincidence; any divergence means the shaft is driven sideways into the wall as it
		// advances, which is the "bends harshly at the end" signature.
		//
		// So the stroke command is decomposed onto all three hip axes: express the hole axis in
		// the pelvis's own (root-local) frame and split the commanded magnitude by its
		// components. Straight in, straight out, whatever direction she is presented at.
		public bool StrokeStraight { get; set; }

		/// <summary>
		/// Apply a stroke command of `mag` (positive = into the hole) along the hole's axis,
		/// splitting it across depth/vertical/lateral. Returns false when the hole pose is not
		/// readable, in which case the caller falls back to a pure depth command.
		/// </summary>
		// ── STRAIGHT-LINE STROKE, v2: POSITION-SLAVED ────────────────────────────────────────
		// v1 split the stroke magnitude across the three axes as per-tick DELTAS and diverged
		// 8deg -> 91deg in seven strokes ([AUDIT] run 2 arm 3). Two compounding defects, both
		// inherent to steering perpendicular axes with a rate:
		//
		//   A  ASYMMETRIC CLAMPING RECTIFIES THE STROKE. cx/cy were clipped against remaining
		//      headroom every tick. What is clipped on the in-stroke is not clipped on the
		//      out-stroke, so the cycle stops summing to zero and x/y INTEGRATE. That is the
		//      monotonic walk-off, and no gain reduction fixes it — only the sign of the drift.
		//
		//   B  POSITIVE FEEDBACK. cz = mag*inLocal.z, but the stroke's reversal logic measures
		//      progress in RAW DEPTH. With inLocal.z < 1 each tick delivers less depth than the
		//      caller believes, so the stroke runs longer and injects proportionally more x/y —
		//      which rotates the shaft further off axis, which shrinks inLocal.z again.
		//
		// Both are the same root cause: velocity control on axes with no path back. So x and y
		// are now POSITION-SLAVED to z. To travel along the hole axis, the perpendicular offsets
		// must satisfy a fixed ratio against depth travel:
		//
		//      targetX = anchorX + (inLocal.x / inLocal.z) * (z - anchorZ)
		//      targetY = anchorY + (inLocal.y / inLocal.z) * (z - anchorZ)
		//
		// and we drive TOWARD those targets rather than adding to them. A tick that saturates
		// simply fails to arrive and is retried next tick; nothing accumulates, and when the
		// stroke returns to the anchor depth the targets return to the anchor offsets. Defect A
		// cannot occur because there is no integration. Defect B is bounded by the ratio floor
		// and cap below: past that geometry the hips genuinely cannot deliver a straight line,
		// and reporting that is correct behaviour — we hand the stroke back to plain z rather
		// than diverging while pretending.
		//
		// z itself is left exactly as the baseline arms drove it. Arms 0-2 showed plain z is not
		// the problem, so this adds the perpendicular coupling and changes nothing else.
		private const float StraightMinAxisZ = 0.35f;

		private const float StraightMaxRatio = 1.5f;

		private const float StraightTrackGain = 6f;

		private const float StraightMaxPerpOffset = 0.12f;

		private bool straightAnchored;
		private float sAnchorX, sAnchorY, sAnchorZ;
		private MotionType straightLastMotion;

		/// <summary>Anchor at the SHALLOW reversal (OUT -> IN): the pose nearest neutral, so a
		/// bad pose cannot be baked in as the new reference the way a deep-point anchor would.
		/// </summary>
		private void UpdateStraightAnchor()
		{
			if (Sequence == null) return;
			MotionType m = Sequence.Motion;
			bool shallowReversal = (straightLastMotion == MotionType.OUT && m == MotionType.IN);
			straightLastMotion = m;
			if (!straightAnchored || shallowReversal)
			{
				Vector3 off;
				try { off = controllerOffsets.leftThighOffset; } catch { return; }
				sAnchorX = off.x;
				sAnchorY = off.y;
				sAnchorZ = off.z;
				straightAnchored = true;
			}
		}

		private bool ApplyStrokeAlongAxis(float mag)
		{
			if (!StrokeStraight) return false;
			try
			{
				if (Sequence == null || Sequence.HoleEntrance == null) return false;
				Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
				if (rm == null) return false;
				// Into the hole is the OPPOSITE of the outward axis used by the line cast.
				Vector3 inLocal = rm.InverseTransformDirection(-Sequence.HoleEntrance.forward.normalized);
				if (inLocal.sqrMagnitude < 1E-08f) return false;
				inLocal.Normalize();

				// Ratio floor kills defect B at its source: as inLocal.z falls the perpendicular
				// demand per unit of depth rises without bound, and that divergence IS the runaway.
				if (Mathf.Abs(inLocal.z) < StraightMinAxisZ)
				{
					AuditNoteStraight(0f, true);
					logger.InfoRare(60,
						"[AutoThrust/straight] axis too oblique (inLocal.z={0:F2} < {1:F2}) - "
						+ "handing back to plain z; hips cannot make this line straight",
						inLocal.z, StraightMinAxisZ);
					return false;
				}
				float kx = Mathf.Clamp(inLocal.x / inLocal.z, -StraightMaxRatio, StraightMaxRatio);
				float ky = Mathf.Clamp(inLocal.y / inLocal.z, -StraightMaxRatio, StraightMaxRatio);

				UpdateStraightAnchor();
				if (!straightAnchored) return false;

				// z is driven exactly as the baseline does. Not scaled by inLocal.z — that scaling
				// was defect B, and the depth-space reversal logic expects raw depth units.
				float cz = mag;
				NoteCommand(0f, 0f, cz);
				if (cz != 0f) controller.AddProfundidadDelta(cz);

				Vector3 off;
				try { off = controllerOffsets.leftThighOffset; } catch { return true; }

				// Perpendicular POSITION targets implied by where the depth axis now sits.
				float dz = Mathf.Clamp(off.z - sAnchorZ, -0.5f, 0.5f);
				float tx = sAnchorX + kx * dz;
				float ty = sAnchorY + ky * dz;
				tx = Mathf.Clamp(tx, sAnchorX - StraightMaxPerpOffset, sAnchorX + StraightMaxPerpOffset);
				ty = Mathf.Clamp(ty, sAnchorY - StraightMaxPerpOffset, sAnchorY + StraightMaxPerpOffset);

				// Proportional TRACKING, rate-limited. The clamp below can starve a tick without
				// consequence: the error simply persists into the next one instead of being lost.
				float dt = Time.deltaTime;

				// THE CAP WAS BELOW THE DEMAND — arithmetically, not marginally.
				//
				// This was |mag| + 0.002. But the geometry requires |k| * dz of perpendicular
				// travel per dz of depth travel, and k reaches StraightMaxRatio (1.5). So whenever
				// the axis was oblique the tracker was rate-limited BELOW the rate it needed and
				// the error could never close — which is exactly what the audit reported:
				// "straight commanded but did NOT track, meanErr 0.041-0.048 m" with a bail rate of
				// zero. It was trying every tick and mathematically forbidden from succeeding.
				//
				// Sizing the cap off the perpendicular DEMAND rather than off the stroke magnitude
				// makes the limit a safety bound again instead of the binding constraint.
				float demand = Mathf.Abs(mag) * (1f + Mathf.Max(Mathf.Abs(kx), Mathf.Abs(ky)));
				float maxStep = demand + 0.002f;
				float cx = Mathf.Clamp((tx - off.x) * StraightTrackGain * dt, -maxStep, maxStep);
				float cy = Mathf.Clamp((ty - off.y) * StraightTrackGain * dt, -maxStep, maxStep);

				AxisHeadroom(out float sxP, out float sxM, out float syP, out float syM);
				cy = (cy >= 0f) ? Mathf.Min(cy, syP) : Mathf.Max(cy, -syM);
				cx = (cx >= 0f) ? Mathf.Min(cx, sxP) : Mathf.Max(cx, -sxM);

				// Tracking error is the manipulation check for this arm: commanding without
				// arriving means the axis is blocked, and that is a different fact from the
				// feature not helping.
				AuditNoteStraight(Mathf.Sqrt((tx - off.x) * (tx - off.x) + (ty - off.y) * (ty - off.y)), false);
				// Contention: coarse steers the same perpendicular error via the avatar. If it is
				// active in the same tick and pulling the opposite way, the two are fighting.
				if (coarseActive && (cx != 0f || cy != 0f)) AuditNoteContention();

				NoteCommand(cx, cy, 0f);
				if (cy != 0f) controller.AddVerticalDelta(cy);
				if (cx != 0f) controller.AddHorizontalDelta(cx);

				logger.InfoRare(120,
					"[AutoThrust/straight] inLocal=({0:F2},{1:F2},{2:F2}) k=({3:F2},{4:F2}) "
					+ "dz={5:F4} tgt=({6:F4},{7:F4}) off=({8:F4},{9:F4}) step=({10:F5},{11:F5}) "
					+ "mag={12:F5}",
					inLocal.x, inLocal.y, inLocal.z, kx, ky, dz, tx, ty, off.x, off.y, cx, cy, mag);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private Vector3 lineErrSlow;
		private bool lineErrPrimed;

		private bool TryLineCastError(out Vector3 errSlow, out Vector3 outward, out float axial)
		{
			errSlow = Vector3.zero; outward = Vector3.forward; axial = 0f;
			try
			{
				if (Sequence == null || Sequence.HoleEntrance == null) return false;
				Penis pene = base.Session.Player.Character.pene;
				if (pene == null) return false;
				float L = pene.worldLengthFromUnderSkin;
				if (L <= 0.0001f) return false;

				outward = Sequence.HoleEntrance.forward.normalized;
				Vector3 target = Sequence.HoleEntrance.position + outward * StrokeLength();
				Vector3 e = target - PeneBase();

				if (!lineErrPrimed) { lineErrSlow = e; lineErrPrimed = true; }
				else lineErrSlow = Vector3.Lerp(lineErrSlow, e, Mathf.Clamp01(Time.deltaTime / AlignSlowTau));

				errSlow = lineErrSlow;
				axial = Vector3.Dot(errSlow, outward);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>How far the hips sit from neutral, as a fraction of each axis' range.</summary>
		private float HipOffsetFromNeutralFrac()
		{
			try
			{
				Vector3 cur = controllerOffsets.leftThighOffset;
				PelvisMovementController.Range rx = controller.xRange;
				PelvisMovementController.Range ry = controller.yRange;
				float xc = (rx.MinLimited() + rx.MaxLimited()) * 0.5f;
				float yc = (ry.MinLimited() + ry.MaxLimited()) * 0.5f;
				float xr = Mathf.Max(1e-4f, rx.MaxLimited() - rx.MinLimited());
				float yr = Mathf.Max(1e-4f, ry.MaxLimited() - ry.MinLimited());
				return Mathf.Max(Mathf.Abs(cur.x - xc) / xr, Mathf.Abs(cur.y - yc) / yr) * 2f;
			}
			catch
			{
				return 0f;
			}
		}

		/// <summary>
		/// Coarse placement. Runs only while the fine stage cannot reach — either the base is far
		/// off the hole's axis, or the hips have been driven near their limits and need the avatar
		/// to take over so they can recentre.
		/// </summary>
		private void UpdateCoarsePlacement(float dt)
		{
			coarseActive = false;
			if (!AlignCoarse || Sequence == null) return;
			// Coarse uses the SAME line-cast target: bring the base onto the line, half a pene
			// length out from the hole. Perpendicular part only — the axial part is the stroke
			// centre and belongs to z, not to walking the character.
			if (!TryLineCastError(out Vector3 lineE, out Vector3 lineO, out float lineA)) return;
			Vector3 ePerp = lineE - lineO * lineA;

			float err = ePerp.magnitude;
			float hipFrac = HipOffsetFromNeutralFrac();
			bool needed = err > CoarseTolerance || hipFrac > (1f - CoarseHipNeutralFrac);
			if (!needed || coarseTotal >= CoarseMaxTotal) return;

			// Move in the avatar's own frame, horizontal only — vertical placement is the hips'
			// job and the pelvis y range is far finer than walking the character up and down.
			try
			{
				Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
				if (rm == null) return;
				Vector3 local = rm.InverseTransformDirection(ePerp);
				local.y = 0f;
				if (local.sqrMagnitude < 1e-8f) return;

				float stepLen = Mathf.Min(CoarseRatePerSec * dt, CoarseMaxTotal - coarseTotal);
				Vector3 step = local.normalized * stepLen;
				base.Session.Player.Move(step);
				coarseTotal += stepLen;
				AuditNoteCoarseStep(stepLen);
				coarseActive = true;

				logger.InfoRare(45,
					"[AutoThrust/coarse] ePerp={0:F4} hipFrac={1:F2} step={2:F4} total={3:F3}/{4:F2} "
					+ "(stops at ePerp<{5:F3}; bounded and self-limiting)",
					err, hipFrac, stepLen, coarseTotal, CoarseMaxTotal, CoarseTolerance);
			}
			catch
			{
			}
		}

		private float AlignRatePerSec
		{
			get
			{
				return alignAcquiring ? AlignAcquireRatePerSec : AlignHoldRatePerSec;
			}
		}

		// Angle at which the corrector has FULL authority. Scaling by the 80° solvability bound
		// meant a typical 10° error got 12.5 % of the rate; a moderate error deserves most of it.
		private const float AlignFullAuthorityDeg = 20f;

		// The sign learner may only judge once the angle has actually moved more than noise.
		// Below this the verdict is meaningless and flipping on it produces the observed
		// -1/+1/-1 flapping, which nets out to no correction at all.
		private const float AlignProgressEpsilonDeg = 0.5f;

		// HARD AUTHORITY LIMITS. A self-correcting sign is only safe if being wrong is bounded.
		// With the epsilon gate the learner stops flapping — but when the angle does not respond
		// at all it then never flips either, so it commits to one direction and pushes forever.
		// Observed: the hips descended until the pene left the hole. Three independent brakes:
		//   • total travel from where correction began is capped,
		//   • it gives up after a while without progress and latches off,
		//   • it only runs while comfortably inside — never near the pop-out floor.
		private const float AlignMaxTravel = 0.035f;

		private const float AlignGiveUpSeconds = 6f;

		// A shallow, not-yet-warmed hole never satisfies a 25 % margin, so alignment switched
		// itself off exactly when it was most needed — at the start, at minimum depth. The gate
		// exists to stop a wrong correction becoming a pop-out, and the z-centre lever does not
		// withdraw, so a much smaller margin is sufficient.
		private const float AlignSafeDepthMargin = 0.08f;

		// A stroke may not take longer than this to traverse its window, however short the window.
		private const float MaxStrokeSeconds = 1.2f;

		/// <summary>Stroke speed below which the velocity chain explains itself in the log. Just
		/// under the absolute floor, so a healthy stroke never trips it.</summary>
		private const float SlowStrokeDiagThreshold = 0.08f;

		private float alignAccum;

		private float alignRunSeconds;

		private float alignBestAbs = float.MaxValue;

		private bool alignGaveUp;

		/// <summary>
		/// Only correct while the stroke is comfortably inside. Pushing the hips around near the
		/// entrance is how a wrong sign turns into a pop-out.
		/// </summary>
		private bool SafelyInsideForAlignment()
		{
			bool inside = SafelyInsideForAlignmentCore();
			// A gate that silently suppresses a corrector is indistinguishable from a corrector
			// that does nothing. Counted, so an arm can report WHY it never actuated.
			if (!inside) AuditNoteGateBlocked();
			return inside;
		}

		private bool SafelyInsideForAlignmentCore()
		{
			float cap = HoleDepthCapacity();
			if (cap > 0f)
			{
				return InternalsDepth() > cap * (FloorFraction() + AlignSafeDepthMargin);
			}
			PeneLens L = ReadPeneLens();
			if (!L.valid) return false;
			float span = Mathf.Max(0.0001f, L.full - L.tip);
			return L.pen > GetMinPenetrationExpectation() + span * AlignSafeDepthMargin;
		}

		private const float AlignThrottleRelief = 0.75f;

		private const float AlignEvalSeconds = 0.5f;

		private bool aligning;

		private float alignSign = 1f;

		private float alignProgress;

		private float alignEvalTimer;

		private float alignLastAbs;

		// Lateral correction moves the AVATAR, so its rate is deliberately lower than the hips'.
		private const float AlignLateralRatePerSec = 0.1f;

		private float lateralSign = 1f;

		private float lateralProgress;

		private float lateralEvalTimer;

		private float lateralLastAbs;

		private float lateralAccum;

		/// <summary>Vertical misalignment between the approach axis and the hole axis, degrees.</summary>
		private float VerticalMisalignDeg()
		{
			try
			{
				if (Sequence == null || Sequence.HoleEntrance == null) return 0f;
				Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
				if (rm == null) return 0f;
				return UnityUtils.FromToAxisAngle(rm.forward, -Sequence.HoleEntrance.forward, rm.right);
			}
			catch
			{
				return 0f;
			}
		}

		/// <summary>Horizontal (yaw) misalignment — AutoSeeker measures this about the up axis.</summary>
		private float LateralMisalignDeg()
		{
			try
			{
				if (Sequence == null || Sequence.HoleEntrance == null) return 0f;
				Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
				if (rm == null) return 0f;
				return UnityUtils.FromToAxisAngle(rm.forward, -Sequence.HoleEntrance.forward, rm.up);
			}
			catch
			{
				return 0f;
			}
		}

		/// <summary>Advance the slow averages that strip the stroke's own oscillation.</summary>
		private void SampleAlignSlow(float vInstant, float hInstant, float dt)
		{
			if (!alignSlowPrimed)
			{
				vangleSlow = vInstant;
				hangleSlow = hInstant;
				alignSlowPrimed = true;
				return;
			}
			float a = Mathf.Clamp01(dt / AlignSlowTau);
			vangleSlow = Mathf.Lerp(vangleSlow, vInstant, a);
			hangleSlow = Mathf.Lerp(hangleSlow, hInstant, a);
		}

		/// <summary>
		/// True while the player is driving the hips by hand. Assumes the default C/V binds — the
		/// assist must never fight a manual correction.
		/// </summary>
		private static bool ManualHipInput()
		{
			try
			{
				return Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.V);
			}
			catch
			{
				return false;
			}
		}

		// ══ POSE SOLVE ═══════════════════════════════════════════════════════════════════════
		// Replaces three incremental correctors (fine z/y trim, coarse avatar walk, straight-line
		// stroke) that each chased an overlapping error with no owner per axis. [AUDIT] run 1
		// measured the consequence directly: 18 % of ticks had coarse and straight commanding
		// OPPOSITE SIGNS on the same axis, and none of the three converged.
		//
		// Instead, compute the pose the character should be in and drive each degree of freedom
		// from exactly one actuator:
		//
		//   yaw    Session.Player.Rotate    root.forward -> -axis, about world up
		//   pitch  pelvis z CENTRE BIAS     shaft parallel to axis (69 deg/unit, measured)
		//   perp   Session.Player.Move      base -> station E + axis*(L/2), bounded
		//   height pelvis y                 residual vertical
		//   stroke pelvis z OSCILLATION     unchanged, oscillating about the bias
		//
		// Pelvis z carrying both the pitch bias and the stroke is not a conflict: the stroke
		// oscillates ABOUT the bias, so the same depths are reached from a different pose.
		//
		// ORDER MATTERS: orientation before position. Translating does not change orientation,
		// but rotating moves the base — so solving position first would immediately invalidate
		// it. Solve yaw and pitch, then close the residual with translation.
		//
		// CONSEQUENCE, and the reason this supersedes rather than joins the old code: once the
		// shaft is parallel to the axis and the base sits on the line, a PURE-Z STROKE IS ALONG
		// THE LINE. StrokeStraight existed only to compensate for an unsolved orientation
		// mid-stroke, which is why it commanded 4-5 cm of perpendicular travel it could never
		// deliver. With the pose correct there is nothing for it to correct.
		public bool AlignSolver { get; set; } = true;

		private const float SolveYawRateDegPerSec = 40f;
		private const float SolveYawDeadbandDeg = 1.5f;
		private const float SolvePitchDeadbandDeg = 2f;
		private const float SolveMoveRatePerSec = 0.12f;
		private const float SolveMoveTolerance = 0.02f;
		private const float SolveMoveMaxTotal = 0.5f;
		private const float SolveMaxSolvableDeg = 80f;

		private float solveMoveTotal;

		/// <summary>Above this the pitch correction uses the ACQUIRE rate — same two-regime split
		/// as the legacy corrector (capability map §8), so a large error is closed decisively and
		/// a small one is not chased into jitter against the stroke's own motion.</summary>
		private const float AlignAcquireDeg = 8f;

		/// <summary>Time constant for the translation glide. Deliberately far slower than the
		/// orientation rate: a fast loop and a slow loop on different quantities do not fight, and
		/// that bandwidth gap is what lets positioning and thrusting run at the same time.</summary>
		private const float SolveMoveTau = 1.2f;

		/// <summary>Below this the hips do all of it; above CoarseBlendHi the avatar does; between
		/// the two they share continuously. 3 cm is comfortably inside the pelvis x range
		/// (+/-0.333), so the fine end is never asking for travel the hips do not have.</summary>
		private const float CoarseBlendLo = 0.03f;

		private const float CoarseBlendHi = 0.12f;

		/// <summary>Lag on the blend weight — this is the hysteresis. A latch with two thresholds
		/// still snaps at each edge; a smoothed weight cannot.</summary>
		private const float CoarseBlendTau = 0.8f;

		private float coarseWeight;

		/// <summary>Angle map from FREE CALIBRATE: peneAngle = solveSlopeZ * hipZ + solveInterceptZ.
		/// Measured on THIS character, so it beats any constant.</summary>
		private float solveSlopeZ = AlignDegPerUnitZ;

		/// <summary>
		/// Degrees of shaft pitch per unit of pelvis Z — the FREECAL-measured map when calibration
		/// has run, otherwise the documented default. Exposed so AutoSeek can drive collinearity
		/// with the SAME number the thrust solve uses: two features disagreeing about the
		/// character's own kinematics is how they end up fighting.
		/// </summary>
		/// <summary>Pene length and character scale the map was measured at — recorded to detect
		/// when it has expired, NOT to rescale it. See PitchDegPerUnitZ.</summary>
		private float solveMapAtLength;

		private float solveMapAtScale;

		/// <summary>
		/// Degrees of pitch per unit of pelvis Z, from FREECAL when available.
		///
		/// NO SIZE CORRECTION IS APPLIED, and that is deliberate. An earlier version divided the
		/// slope by pene length on the theory that pitch is a displacement over a lever, so a
		/// longer shaft would swing less. That model is WRONG: pelvis Z does not pivot the shaft
		/// about its base, it moves the pelvis, and the IK ROTATES the pelvis as it translates —
		/// the pene turns because it is rigidly attached to something that turned. Length
		/// therefore governs how far the TIP travels for a given angle (tip rise = length x
		/// sin(theta)), not the angle. A longer shaft on the same rotating pelvis pitches by the
		/// same number of degrees.
		///
		/// What the slope actually depends on is an open question — character scale is the
		/// plausible candidate, since a fixed metre command is a smaller relative motion on a
		/// larger body. But that is a MEASUREMENT, not something to derive, and it needs FREECAL
		/// run at two different scales to establish. Until then the map is marked stale when the
		/// character resizes rather than being "corrected" by an unverified law — a map that
		/// admits it has expired is far safer than one confidently rescaled by the wrong rule.
		/// </summary>
		public float PitchDegPerUnitZ => solveMapCalibrated && !PitchMapStale
			? solveSlopeZ
			: AlignDegPerUnitZ;

		/// <summary>
		/// True when the character has resized materially since calibration, so the stored slope no
		/// longer describes this body. Falls back to the documented default and says so, instead of
		/// silently applying a number measured on a different-sized character.
		/// </summary>
		public bool PitchMapStale
		{
			get
			{
				if (!solveMapCalibrated) return false;
				float lenNow = PeneLengthNow();
				float scaleNow = PlayerScaleNow();
				bool lenMoved = solveMapAtLength > 0.0001f && lenNow > 0.0001f
					&& Mathf.Abs(lenNow - solveMapAtLength) / solveMapAtLength > 0.08f;
				bool scaleMoved = solveMapAtScale > 0.0001f && scaleNow > 0.0001f
					&& Mathf.Abs(scaleNow - solveMapAtScale) / solveMapAtScale > 0.05f;
				return lenMoved || scaleMoved;
			}
		}

		/// <summary>True once FREE CALIBRATE has supplied a measured map for this character.</summary>
		public bool PitchMapCalibrated => solveMapCalibrated;

		private float solveInterceptZ;

		private bool solveMapCalibrated;

		/// <summary>
		/// Half the travel that is actually usable, in world metres — the station's distance from
		/// the entrance and the stroke's half-amplitude.
		///
		/// Two independent limits, and either can bind: the pene's own usable length, and the
		/// depth this partner admits. A long pene in a shallow partner is limited by her; a short
		/// one in a deep partner by itself. Taking the smaller and halving it puts the station at
		/// the stroke's midpoint with symmetric travel in both directions.
		/// </summary>
		private float StrokeLength()
		{
			PeneLens lens = ReadPeneLens();
			float usablePene = lens.valid ? Mathf.Max(0.01f, lens.full - lens.tip) : PeneLengthNow();
			float usableDepth = usablePene;
			float cap = HoleDepthCapacity();
			if (cap > 0f && internalsPerWorld > 0.0001f)
			{
				// Hole capacity is in internals units; convert with the measured scale so the two
				// limits are comparable at all. Mixing the spaces is what made an earlier version
				// of this collapse every cap to near zero.
				usableDepth = cap / internalsPerWorld;
			}
			return Mathf.Max(0.01f, Mathf.Min(usablePene, usableDepth) * 0.5f);
		}

		private void ZHeadroom(out float zPlus, out float zMinus)
		{
			zPlus = 0f;
			zMinus = 0f;
			try
			{
				PelvisMovementController.Range zr = controller.zRange;
				Vector3 cur = controllerOffsets.leftThighOffset;
				zPlus = Mathf.Max(0f, zr.MaxLimited() - cur.z);
				zMinus = Mathf.Max(0f, cur.z - zr.MinLimited());
			}
			catch
			{
			}
		}

		/// <summary>True when the solver handled this tick, so the legacy correctors stand down
		/// rather than adding a second opinion on the same axes.</summary>
		private bool UpdatePoseSolve(float dt)
		{
			// GATED ON THE TOGGLE THAT ADVERTISES IT.
			//
			// AlignSolver defaulted to true and was checked INSTEAD of AlignHips, so the pose solve
			// — yaw rotation, pitch bias, avatar translation — ran whenever a sequence was active
			// regardless of whether "Align hips" was switched on. Features acting while their
			// control reads off is worse than a feature that does not work: it makes every A/B
			// comparison meaningless, and the audit's OFF arms were never actually off.
			//
			// AlignSolver is the IMPLEMENTATION of Align hips, not a second independent switch, so
			// it now requires the toggle the user actually sees.
			if (!AlignHips || !AlignSolver || Sequence == null || dt <= 0f) return false;
			if (ManualHipInput()) return false;
			Transform he = Sequence.HoleEntrance;
			if (he == null) return false;
			Transform root;
			try { root = base.Session.Player.RootMotion; } catch { return false; }
			if (root == null) return false;

			Vector3 E = he.position;
			Vector3 outward = he.forward.normalized;
			Vector3 axisIn = -outward;
			Vector3 B = PeneBase();
			Vector3 T = PeneTip();
			Vector3 shaft = T - B;
			if (shaft.sqrMagnitude < 1E-09f) return false;
			shaft.Normalize();
			float L = PeneLengthNow();
			if (L <= 0.0001f) return false;

			// Unsolvable geometry: say so and stand down rather than grinding at it. AutoSeek uses
			// the same 80 deg limit for the same reason.
			float steep = UnityUtils.FromToAxisAngle(root.forward, axisIn, root.right);
			if (Mathf.Abs(steep) > SolveMaxSolvableDeg)
			{
				logger.InfoRare(120,
					"[AutoThrust/solve] axis {0:F0}deg off the body plane - beyond what hips and "
					+ "yaw can solve; standing down", steep);
				return false;
			}

			// ── 1. YAW ── heading, about world up. Owns lateral ANGLE outright.
			float yawErr = UnityUtils.FromToAxisAngle(root.forward, axisIn, root.up);
			bool yawing = Mathf.Abs(yawErr) > SolveYawDeadbandDeg;
			if (yawing)
			{
				base.Session.Player.Rotate(
					Mathf.MoveTowards(0f, yawErr, SolveYawRateDegPerSec * dt));
			}

			// ── 2. PITCH ── shaft vs axis in the body's vertical plane, driven by the z CENTRE.
			// 69 deg/unit is measured (ALIGNMENT_CAPABILITY_MAP §3), so there is no sign learner:
			// the direction is in the measurement.
			// ABSOLUTE, not differential. "Regardless of the current pitch, the required pitch is
			// known" only holds with an absolute map from hip position to pene angle, and 69
			// deg/unit is a SLOPE — it has no intercept, so on its own it can only ever say
			// "move a bit that way" and must integrate. The intercept is measurable live: this
			// tick we know both the current hip z and the current pene angle, so
			//
			//     peneAngle(z) = slope * z + intercept   =>   intercept = peneAngle - slope * z
			//
			// and the required hip z follows in closed form. Re-derived every tick, so it
			// self-calibrates to the character's scale and pose instead of trusting a constant
			// measured once on one body.
			float peneAngleNow = UnityUtils.FromToAxisAngle(root.forward, shaft, root.right);
			float peneAngleWant = UnityUtils.FromToAxisAngle(root.forward, axisIn, root.right);
			float hipZNow;
			try { hipZNow = controllerOffsets.leftThighOffset.z; } catch { hipZNow = alignZBias; }

			// Prefer the map FREE CALIBRATE measured on this character: it sweeps the depth axis
			// through its whole range while tracking the pene's angle, which is this exact
			// relationship, measured rather than assumed. The live intercept below still runs on
			// top, so an uncalibrated session degrades to the 69 deg/unit default instead of
			// failing, and a calibrated one is anchored to the real body.
			float slopeZ = solveMapCalibrated ? solveSlopeZ : AlignDegPerUnitZ;
			float intercept = solveMapCalibrated
				? Mathf.Lerp(solveInterceptZ, peneAngleNow - slopeZ * hipZNow, 0.5f)
				: (peneAngleNow - slopeZ * hipZNow);
			float hipsStrokeZ = Mathf.Clamp((peneAngleWant - intercept) / slopeZ,
				-AlignZBiasMax, AlignZBiasMax);

			float pitchErr = peneAngleWant - peneAngleNow;
			bool pitching = Mathf.Abs(pitchErr) > SolvePitchDeadbandDeg;
			if (pitching)
			{
				float wantZ = hipsStrokeZ;
				float zRate = (Mathf.Abs(pitchErr) > AlignAcquireDeg
					? AlignAcquireRatePerSec : AlignHoldRatePerSec) * Mathf.Clamp01(AlignGain);
				float zStep = Mathf.Clamp(wantZ - alignZBias, -zRate * dt, zRate * dt);
				ZHeadroom(out float zPlus, out float zMinus);
				zStep = (zStep >= 0f) ? Mathf.Min(zStep, zPlus) : Mathf.Max(zStep, -zMinus);
				if (zStep != 0f)
				{
					alignZBias += zStep;
					AuditNoteAlignStep(zStep);
					NoteCommand(0f, 0f, zStep);
					controller.AddProfundidadDelta(zStep);
				}
			}

			// ── 3. POSITION ── only after orientation, and only the part that is NOT depth.
			// The along-axis component is the stroke's business; correcting it here would fight
			// the thrust, which is the mistake the depth-dependent target made originally.
			// POSITION RUNS CONCURRENTLY with yaw and pitch — it is NOT gated behind them. Gating
			// would stall positioning for as long as orientation takes and make the whole thing
			// sequential, when the character can obviously turn, tilt, walk and thrust at once.
			//
			// What separates them is BANDWIDTH, not turn-taking. Orientation is rate-limited and
			// converges in a second or two; translation is exponentially smoothed with a much
			// longer time constant. A fast loop and a slow loop on different quantities do not
			// fight. Likewise the pelvis receives centre + stroke SUMMED: positioning sets the
			// mean, thrusting sets the deviation about it, so two writers share one actuator
			// without competing. Thrust is never gated on alignment — it continues throughout.
			//
			// The error is measured against the CURRENT base but the orientation commands issued
			// above are already in flight, so the smoothing constant is deliberately slower than
			// the orientation rate: by the time translation has moved appreciably, the pose it is
			// correcting for is the settled one, not the one being left behind.
			// strokeLength: half of what is ACTUALLY usable, taking the smaller of the two limits.
			// The pene's usable length and the character's available depth are different numbers
			// and either can be the binding one — a long pene in a shallow partner is limited by
			// her, a short one in a deep partner by itself. Half, so the station sits at the
			// stroke's midpoint with symmetric travel either way.
			Vector3 station = E + outward * StrokeLength();
			Vector3 err = station - B;
			// The along-axis component is the STROKE's business. Removing it is what keeps
			// positioning and thrusting independent — correcting depth here would be positioning
			// reaching into the thrust loop, which is the coupling that has to stay broken.
			Vector3 perp = err - outward * Vector3.Dot(err, outward);
			float perpMag = perp.magnitude;
			bool moving = false;
			if (perpMag > SolveMoveTolerance && solveMoveTotal < SolveMoveMaxTotal)
			{
				// Exponential approach: step a fixed FRACTION of the remaining error per second,
				// then clamp to a maximum speed. Large errors close briskly, small ones glide in
				// and settle instead of buzzing — and it self-terminates, since the step shrinks
				// with the error rather than needing a stop condition bolted on.
				float k = Mathf.Clamp01(dt / SolveMoveTau);

				// BLEND, NOT A SWITCH. Hips handle fine work — bounded, precise, reversible — and
				// the avatar handles gross displacement the hips cannot reach. Choosing one with
				// an if() puts a discontinuity right where the error spends most of its time,
				// and an error hovering on the boundary would chatter between two actuators with
				// different dynamics. So the share is a continuous weight over a band, and the
				// weight itself is exponentially smoothed — that lag IS the hysteresis, and
				// unlike a two-threshold latch it cannot snap even at the edges of the band.
				float wTarget = Mathf.SmoothStep(0f, 1f,
					Mathf.InverseLerp(CoarseBlendLo, CoarseBlendHi, perpMag));
				coarseWeight = Mathf.Lerp(coarseWeight, wTarget, Mathf.Clamp01(dt / CoarseBlendTau));

				Vector3 flat = new Vector3(perp.x, 0f, perp.z);
				float flatMag = flat.magnitude;
				if (flatMag > SolveMoveTolerance)
				{
					float want = Mathf.Min(flatMag * k, SolveMoveRatePerSec * dt);

					// Coarse share: walk the avatar.
					float stepLen = Mathf.Min(want * coarseWeight,
						Mathf.Max(0f, SolveMoveMaxTotal - solveMoveTotal));
					if (stepLen > 0f)
					{
						base.Session.Player.Move(flat.normalized * stepLen);
						solveMoveTotal += stepLen;
						AuditNoteCoarseStep(stepLen);
						moving = true;
					}

					// Fine share: the pelvis's own lateral axis. Only the component along the
					// body's right actually maps to AddHorizontalDelta — the rest of the flat
					// error is depth-ward and belongs to the stroke, not to positioning.
					float fine = want * (1f - coarseWeight);
					if (fine > 0f)
					{
						float lateral = Vector3.Dot(flat.normalized, root.right) * fine;
						AxisHeadroom(out float xP, out float xM, out float _yP2, out float _yM2);
						lateral = (lateral >= 0f) ? Mathf.Min(lateral, xP) : Mathf.Max(lateral, -xM);
						if (lateral != 0f)
						{
							NoteCommand(lateral, 0f, 0f);
							controller.AddHorizontalDelta(lateral);
							moving = true;
						}
					}
				}
				if (Mathf.Abs(perp.y) > SolveMoveTolerance)
				{
					AxisHeadroom(out float _xP, out float _xM, out float yP, out float yM);
					float yStep = Mathf.Clamp(perp.y * k,
						-SolveMoveRatePerSec * dt, SolveMoveRatePerSec * dt);
					yStep = (yStep >= 0f) ? Mathf.Min(yStep, yP) : Mathf.Max(yStep, -yM);
					if (yStep != 0f)
					{
						NoteCommand(0f, yStep, 0f);
						controller.AddVerticalDelta(yStep);
						moving = true;
					}
				}
			}

			aligning = yawing || pitching || moving;
			logger.InfoRare(45,
				"[AutoThrust/solve] yawErr={0:F1} pitchErr={1:F1} perp={2:F4} (flat={3:F4} y={4:F4}) "
				+ "coarseW={5:F2} strokeLen={6:F3} zBias={7:F4} moved={8:F3}/{9:F2} stage={10}",
				yawErr, pitchErr, perpMag, new Vector3(perp.x, 0f, perp.z).magnitude, perp.y,
								coarseWeight, StrokeLength(),
alignZBias, solveMoveTotal, SolveMoveMaxTotal,
				yawing ? "YAW" : (pitching ? "PITCH" : (moving ? "MOVE" : "HOLD")));
			return true;
		}

		private void UpdateAlignment()
		{
			aligning = false;
			if (Sequence == null) return;

			// The solver owns every alignment DOF when it is on. The legacy correctors are left
			// in place only so the audit can still contrast them; they must never run alongside
			// it, or the contention it was built to remove comes straight back.
			if (UpdatePoseSolve(Time.deltaTime)) return;

			float dt = Time.deltaTime;
			float vInstant = VerticalMisalignDeg();
			float hInstant = LateralMisalignDeg();
			SampleAlignSlow(vInstant, hInstant, dt);

			// COARSE placement first: it exists to hand the fine stage a reachable problem from a
			// near-neutral hip pose. While it is working, the fine trims stand down so the two
			// stages cannot fight over the same error.
			UpdateCoarsePlacement(dt);
			// COARSE MUST YIELD. This early return was meant to stop the two stages fighting over
			// one error, but [AUDIT] run 1 arm 4 recorded alignTicks=0 with gateBlocked=0 %: the
			// fine stage was never REACHED, because coarse stayed active for the whole arm and
			// returned first every tick. Arm 2 shows why — coarse ran 0.29 m and was still
			// stepping at the arm's end, i.e. it does not converge, so "wait for coarse" means
			// "never". A stage that has not converged within its own settling time has failed and
			// must hand over rather than hold the DOF forever.
			if (coarseActive)
			{
				coarseHoldT += dt;
				if (coarseHoldT < CoarseMaxHoldSeconds)
				{
					aligning = true;
					return;
				}
				logger.InfoRare(1,
					"[AutoThrust/align] coarse held the DOF for {0:F1}s without converging - "
					+ "yielding to the fine stage", coarseHoldT);
			}
			else
			{
				coarseHoldT = 0f;
			}

			// LATERAL — pelvis x, NOT avatar translation.
			//
			// This drove Session.Player.Move, which relocates the whole AVATAR: an unbounded,
			// cumulative world translation with no way back, and it had none of the guards the
			// vertical path has. Observed: the player walked steadily to one side while the tip
			// stayed captured, levering the shaft into a severe bend.
			//
			// AddHorizontalDelta is the right actuator — the capability map measures x as bounded
			// (+/-0.333) with ~0 pitch authority, so it trims lateral offset without disturbing
			// pitch and physically cannot run away. Now guarded like the vertical: safety gate,
			// give-up latch, bounded accumulation, epsilon rule on the sign learner.
			if (AlignLateral && !ManualHipInput() && !alignGaveUp && SafelyInsideForAlignment())
			{
				float hs = hangleSlow;
				float absH = Mathf.Abs(hs);
				if (absH > AlignDeadbandDeg && absH <= AlignMaxSolvableDeg)
				{
					AxisHeadroom(out float lxP, out float lxM, out float _lyP, out float _lyM);
					float lrate = (alignAcquiring ? AlignAcquireRatePerSec : AlignHoldRatePerSec)
						* Mathf.Clamp01(AlignGain);
					float lstep = Mathf.Clamp(hs / AlignFullAuthorityDeg, -1f, 1f)
						* lrate * lateralSign * dt;
					// Bounded accumulation: never further than AlignMaxTravel from where it began.
					if (Mathf.Abs(lateralAccum + lstep) > AlignMaxTravel
						&& Mathf.Sign(lstep) == Mathf.Sign(lateralAccum))
					{
						lstep = 0f;
					}
					lstep = (lstep >= 0f) ? Mathf.Min(lstep, lxP) : Mathf.Max(lstep, -lxM);
					if (lstep != 0f)
					{
						lateralAccum += lstep;
						NoteCommand(lstep, 0f, 0f);
						controller.AddHorizontalDelta(lstep);
					}

					lateralProgress += lateralLastAbs - absH;
					lateralLastAbs = absH;
					lateralEvalTimer += dt;
					if (lateralEvalTimer >= AlignEvalSeconds)
					{
						if (Mathf.Abs(lateralProgress) >= AlignProgressEpsilonDeg)
						{
							if (lateralProgress < 0f) lateralSign = 0f - lateralSign;
							lateralProgress = 0f;
						}
						lateralEvalTimer = 0f;
					}
					aligning = true;
				}
				else
				{
					lateralProgress = 0f;
					lateralEvalTimer = 0f;
					lateralLastAbs = absH;
				}
			}

			if (!AlignHips) return;

			// SAFETY GATES, checked before any vertical authority is used at all.
			if (alignGaveUp || !SafelyInsideForAlignment())
			{
				alignProgress = 0f;
				alignEvalTimer = 0f;
				return;
			}

			// VERTICAL. Driven from the SLOW angle, so the stroke's own pelvis motion averages out
			// and only a persistent positioning error is corrected.
			float v = vangleSlow;
			float absV = Mathf.Abs(v);
			if (absV <= AlignDeadbandDeg || absV > AlignMaxSolvableDeg || ManualHipInput())
			{
				alignProgress = 0f;
				alignEvalTimer = 0f;
				alignLastAbs = absV;
				return;
			}

			aligning = true;

			// Regime selection, with hysteresis so it cannot flap on the boundary.
			if (alignAcquiring && absV < AlignHoldBelowDeg) alignAcquiring = false;
			else if (!alignAcquiring && absV > AlignAcquireAboveDeg) alignAcquiring = true;

			// SELF-CORRECTING SIGN. AutoSeeker drives vertical from a POSITIONAL error and maps
			// vangle to DEPTH instead, so the correct angle→vertical polarity is not something to
			// assume. Drive it, watch whether |vangle| actually falls, and flip if it does not.
			alignProgress += alignLastAbs - absV;
			alignLastAbs = absV;
			alignEvalTimer += dt;
			if (alignEvalTimer >= AlignEvalSeconds)
			{
				// Only judge when the angle actually moved more than noise. If it did not, KEEP
				// the accumulator running rather than resetting it — resetting on an inconclusive
				// window is what let noise decide the sign and made it flap.
				if (Mathf.Abs(alignProgress) >= AlignProgressEpsilonDeg)
				{
					if (alignProgress < 0f) alignSign = 0f - alignSign;
					alignProgress = 0f;
				}
				alignEvalTimer = 0f;
			}

			// GIVE UP if the angle simply is not responding. Some misalignment is the hole's own
			// orientation and no amount of hip height will change it; pushing regardless is what
			// walked the hips out of the hole.
			alignRunSeconds += dt;
			if (absV < alignBestAbs - 0.25f) { alignBestAbs = absV; alignRunSeconds = 0f; }
			if (alignRunSeconds >= AlignGiveUpSeconds)
			{
				alignGaveUp = true;
				aligning = false;
				logger.InfoRare(1, "[AutoThrust/align] no progress in {0:F0}s (vSlow={1:F1}) - standing down",
					AlignGiveUpSeconds, v);
				return;
			}

			AxisHeadroom(out float _hxA, out float _hxB, out float yPlus, out float yMinus);

			// ── PRIMARY: z-centre from the LINE CAST ──────────────────────────────────────
			// The axial component of the line-cast error says directly how far the base is from
			// its half-length station along the hole's axis: positive = the base should move
			// OUTWARD (reduce z), negative = it sits too far out. That is a position error in
			// metres, stationary across the stroke, and it supersedes deriving the bias from an
			// angle. The angular form is kept only as the fallback when no hole pose is readable.
			float wantZ;
			if (TryLineCastError(out Vector3 lineErr, out Vector3 lineOut, out float lineAxial))
			{
				// Axial error is in world metres; the z command is in pelvis units. Convert with
				// the same MEASURED metres-per-unit the calibration produced, defaulting to the
				// free-space figure when no better estimate exists.
				float mPerUnit = (posPerStep > 1e-4f) ? posPerStep : 0.35f;
				wantZ = Mathf.Clamp(-lineAxial / mPerUnit, -AlignZBiasMax, AlignZBiasMax);
				logger.InfoRare(60,
					"[AutoThrust/line] err=({0:F3},{1:F3},{2:F3}) |err|={3:F3} axial={4:F3} "
					+ "perp={5:F3} wantZ={6:F4} mPerUnit={7:F3}",
					lineErr.x, lineErr.y, lineErr.z, lineErr.magnitude, lineAxial,
					(lineErr - lineOut * lineAxial).magnitude, wantZ, mPerUnit);
			}
			else
			{
				wantZ = Mathf.Clamp(-v / AlignDegPerUnitZ, -AlignZBiasMax, AlignZBiasMax);
			}
			float zRate = (alignAcquiring ? AlignZAcquireRatePerSec : AlignZHoldRatePerSec)
				* Mathf.Clamp01(AlignGain);
			float zStep = Mathf.Clamp(wantZ - alignZBias, -zRate * dt, zRate * dt);
			float zHeadPlus = 0f, zHeadMinus = 0f;
			try
			{
				PelvisMovementController.Range zrA = controller.zRange;
				Vector3 curA = controllerOffsets.leftThighOffset;
				zHeadPlus = Mathf.Max(0f, zrA.MaxLimited() - curA.z);
				zHeadMinus = Mathf.Max(0f, curA.z - zrA.MinLimited());
			}
			catch
			{
			}
			zStep = (zStep >= 0f) ? Mathf.Min(zStep, zHeadPlus) : Mathf.Max(zStep, -zHeadMinus);
			if (zStep != 0f)
			{
				alignZBias += zStep;
				AuditNoteAlignStep(zStep);
				NoteCommand(0f, 0f, zStep);
				controller.AddProfundidadDelta(zStep);
			}

			// ── SECONDARY: y trim. Kept because it is a real (if weaker) lever, but it is
			// ONE-SIDED from the default pose — yPlus headroom is 0 at y = 0 — so it can only
			// ever assist downward. Logged so that asymmetry is visible rather than mysterious.
			float step = Mathf.Clamp(v / AlignFullAuthorityDeg, -1f, 1f)
				* AlignRatePerSec * Mathf.Clamp01(AlignGain) * alignSign * dt;

			// TRAVEL CAP: never drift further than AlignMaxTravel from where correction started,
			// in either direction. Trim the step at the boundary rather than stopping dead, so it
			// can still correct back the other way once the sign learner flips.
			float projected = alignAccum + step;
			if (Mathf.Abs(projected) > AlignMaxTravel)
			{
				float allowedStep = Mathf.Sign(step) * Mathf.Max(0f, AlignMaxTravel - Mathf.Abs(alignAccum));
				step = (Mathf.Sign(step) == Mathf.Sign(alignAccum) || Mathf.Abs(alignAccum) >= AlignMaxTravel)
					? allowedStep
					: step;
			}
			alignAccum += step;
			NoteCommand(0f, step, 0f);
			controller.AddVerticalDelta(step);

			// Instantaneous vs slow tells stroke motion apart from a real positioning error: if
			// vInst swings widely while vSlow sits near zero, that is the thrust, not misalignment.
			logger.InfoRare(90,
				"[AutoThrust/align] mode={0} vSlow={1:F1} vInst={2:F1} hSlow={3:F1} hInst={4:F1} "
				+ "zBias={5:F4} zStep={6:F5} yStep={7:F5} yHeadUp={8:F3} yHeadDn={9:F3} "
				+ "sign={10} bend={11:F3} gain={12:F2}",
				alignAcquiring ? "ACQUIRE" : "HOLD", vangleSlow, vInstant, hangleSlow, hInstant,
				alignZBias, zStep, step, yPlus, yMinus, alignSign, BendDeflection, AlignGain);
		}

		/// <summary>
		/// Proportional throttle: once measured bend passes the setpoint, scale velocity by
		/// setpoint/bend so the stroke eases off BEFORE it bows, instead of reacting after.
		/// While alignment is converging the throttle is RELIEVED — that bend is geometric and
		/// slowing down would only prevent the stroke that proves the correction worked.
		/// </summary>
		private float BendThrottle()
		{
			float setpoint = Mathf.Max(0.005f, MaxBendFraction);
			float d = BendDeflection;
			if (d <= setpoint) return 1f;
			float throttle = Mathf.Clamp(setpoint / d, BendThrottleFloor, 1f);
			if (aligning) throttle = Mathf.Lerp(throttle, 1f, AlignThrottleRelief);
			return throttle;
		}

		// Returns true while the shaft is bent badly enough that the stroke must back out.
		private bool UpdateBendRecovery()
		{
			float setpoint = Mathf.Max(0.005f, MaxBendFraction);
			float d = BendDeflection;
			if (!bendRecovering)
			{
				if (d > setpoint * BendRecoverEnterMult)
				{
					bendRecovering = true;
					// Back off ONCE per bend event, not per tick, and never below the floor.
					bendSpeedScale = Mathf.Max(BendSpeedFloor, bendSpeedScale * BendSpeedBackoff);
					overlay.InfoMessage("Auto-thrust: shaft bending — backing off");
				}
			}
			else if (d <= setpoint * BendRecoverExitMult)
			{
				bendRecovering = false;
			}

			// Speed is only allowed to climb back while the shaft is healthy.
			if (!bendRecovering && bendSpeedScale < 1f)
			{
				bendSpeedScale = Mathf.Min(1f, bendSpeedScale + BendSpeedRecoverPerSec * Time.deltaTime);
			}
			return bendRecovering;
		}

		public float ThrustBalance { get; set; } = 0.5f;

		public float UserThrustBalance { get; set; } = 0.5f;

		public float UserForwardTarget { get; set; } = 1f;

		public float UserBackwardTarget { get; set; }

		public bool ViolentMode
		{
			get
			{
				return controller.maxSpeed == defaultControllerMaxSpeed;
			}
			set
			{
				if (value)
				{
					controller.maxSpeed = 100f;
				}
				else
				{
					controller.maxSpeed = defaultControllerMaxSpeed;
				}
			}
		}

		private float ImmediateDepth => controllerOffsets.leftThighOffset.z;

		/// <summary>
		/// Depth as the STROKE sees it: the pelvis z offset with the alignment bias removed.
		///
		/// THE COUPLING THIS FIXES. ImmediateDepth is the raw pelvis z offset, and the pose solve
		/// biases that very axis to set pitch (AddProfundidadDelta, 69-100 deg/unit measured). So
		/// every pitch correction registered as stroke progress: the stroke believed it had
		/// already moved, and stood still while the solve worked. In game that reads as
		/// positioning and thrusting being mutually exclusive; in the audit it showed up as
		/// axisErr = -1, the sentinel for a whole stroke in which depth changed but the base
		/// never moved.
		///
		/// Subtracting the bias makes the two ADDITIVE rather than competing: positioning sets
		/// the mean, the stroke oscillates about wherever that mean currently is, and the same
		/// depths are reached from a different pelvis position — which was the entire point of
		/// biasing z for pitch in the first place.
		/// </summary>
		private float StrokeDepth => controllerOffsets.leftThighOffset.z - alignZBias;

		// The 100 % reference, in whichever space GetPenetrationDepth is reporting. These two must
		// never disagree — see the note there.
		private float MaxWorldPenetration
		{
			get
			{
				float cap = HoleDepthCapacity();
				return (cap > 0f) ? cap : base.Session.Player.Character.pene.worldLength;
			}
		}

		public SequenceState Sequence { get; private set; }

		public bool VelocityRampUp { get; set; } = true;

		public bool IgnorePRatio { get; set; }

		public AutoThrustService(ConfigEntry<KeyboardShortcut> hotkey, ConfigEntry<bool> useConstantVelocity, ConfigEntry<bool> reduceSmoothTime, ConfigEntry<bool> targetVelocityScale)
		{
			this.hotkey = hotkey;
			this.useConstantVelocity = useConstantVelocity;
			this.reduceSmoothTime = reduceSmoothTime;
			this.targetVelocityScale = targetVelocityScale;
		}

		public override void OnStart()
		{
			base.OnStart();
			overlay = Lookup<OverlayService>();
			hotkeyHandle = Lookup<DispatcherService>().Input.KeyboardEvent(hotkey, base.Scope);
			// A HALF-SWAPPED GUEST IS NOT A PROGRAMMING ERROR — DO NOT THROW ON IT.
			//
			// Bringing in a second model destroys the previous character's components while the
			// session still references them. A destroyed Unity Component is not a null REFERENCE,
			// so these lines look safe and then throw inside native code on get_gameObject. The
			// exception escapes OnStart, ScopeSupport shuts the service down permanently, and BE's
			// crash handler tries to raise a modal that ALSO throws — so one stale reference takes
			// out the feature and buries the cause under a second stack trace.
			//
			// Unity's == operator reports destroyed objects as null, so the state is detectable.
			// Refuse to start against a dead guest and let the caller retry when the new one is
			// ready, which is a normal condition rather than a failure.
			if (base.Session.Player == null || base.Session.Player.GameObject == null
				|| base.Session.Guest == null || base.Session.Guest.Impl == null)
			{
				throw new InvalidOperationException(
					"guest/player not ready (mid model change) - will attach when it is");
			}
			controller = base.Session.Player.GameObject.GetComponentInChildren<PelvisMovementController>();
			controllerOffsets = Traverse.Create((object)controller).Field<LocalEffectorOffset>("m_effector").Value;
			controllerSmoothTime = Traverse.Create((object)controller).Field<float>("smoothTime");
			defaultControllerMaxSpeed = controller.maxSpeed;
			defaultControllerSmoothTime = controllerSmoothTime.Value;
			Lookup<DispatcherService>().DoUpdate.Add(OnUpdate, base.Scope);
			controller.updatingPelvisPosition += Controller_updatingPelvisPosition;
			EmocionesFemeninas e = base.Session.Guest.Impl.GetComponentInChildren<EmocionesFemeninas>();
			// Non-fatal: pleasure is a signal the stroke reads, not something it cannot run without,
			// and a character still assembling may not have it yet.
			pleasure = (e != null) ? e.placer : null;
		}

		public override void OnStop()
		{
			base.OnStop();
			if (controller != null)
			{
				controller.updatingPelvisPosition -= Controller_updatingPelvisPosition;
			}
		}

		private void Controller_updatingPelvisPosition(ref Vector3 currentLocalTarget, Transform effectorTransform, PelvisMovementController sender)
		{
			lastDepth = currentLocalTarget.z;
		}

		private void ResetHipTarget()
		{
			float currentDepth = controllerOffsets.leftThighOffset.z;
			controller.AddProfundidadDelta(currentDepth - lastDepth);
		}

		private void OnUpdate()
		{
			ReactInput();
			// Free-space characterisation runs OUTSIDE the sequence on purpose: it needs no hole,
			// no penetration and no stroke, which is exactly what makes it clean.
			if (controller != null)
			{
				UpdateFreeCal();
			}
			// Angle readout runs outside the sequence too — reading the approach angle BEFORE
			// entry is half its value.
			UpdateAngleDebug();

			// REMOTE REQUESTS. Polled on the service's own update so the actual state change
			// happens on the main thread, whoever asked for it.
			if (BeTestControl.RequestFreeCal)
			{
				BeTestControl.RequestFreeCal = false;
				BeTestControl.FreeCalComplete = false;
				BeTestControl.FreeCalRunning = true;
				FreeCal = true;
				logger.Info("[FREECAL] started by remote request (BeTestControl)");
			}
			if (BeTestControl.RequestAudit)
			{
				BeTestControl.RequestAudit = false;
				StrokeAudit = true;
				logger.Info("[AUDIT] started by remote request (BeTestControl)");
			}
			if (Sequence != null)
			{
				Process();
			}
		}

		// DEPTH BASIS — what "100 %" means.
		//
		// SMA 23.1 removed penetracionLocalActual and this returned 0f, silently disabling
		// GetPenetrationFactor, GetPenetrationRatio, GetDepenetrationThreshold, the whole
		// OUT-stroke bound and its velocity shaping.
		//
		// PRIMARY basis is HER, not the member: maxProfundidadPhysicsLocal is this hole's
		// authored depth (holeConfig.fixedVirtualProfundidad, else castProfundidad —
		// BoneStretchedChain.cs:519), and penetratedDepthLocalInternals is the current
		// penetration in the SAME internals space. So 100 % = her natural wall for THIS hole:
		// a per-hole constant that does not drift, identical at the start of a session and
		// after she has relaxed, and independent of how large the pene is. Over-reach reads
		// above 100 % because it genuinely is.
		// (This mirrors AIChat's ThrustEngine.CurrentDepth, which reaches the same value via
		// reflection on the [Obsolete] maxProfundidadVirtual; maxProfundidadPhysicsLocal is
		// the supported successor and needs no reflection.)
		//
		// FALLBACK, when no hole is registered or its depth reads zero: the pene's own
		// penetratingWorldLength over worldLength — the same quantity the game's
		// ControlladorDeAutoSexV2 uses. Current and max ALWAYS come from the same basis;
		// mixing internals-space depth with world-space length would be meaningless.
		// PENE LENS — the whole depth model, in ONE unit space (penetratingWorldLength).
		// Copied from AIChat's ThrustEngine.ReadPeneLens/ShallowLen, which is the version proven
		// in game. `tip` is clamped to <= 90 % of full so the pop-out floor can never invert.
		private struct PeneLens { public float pen, tip, full; public bool valid; }

		private PeneLens ReadPeneLens()
		{
			try
			{
				Penis p = base.Session.Player.Character.pene;
				if (p == null) return default(PeneLens);
				float full = p.worldLength;
				if (full <= 0.0001f) return default(PeneLens);
				float tip = Mathf.Clamp(p.worldTipPartLength, 0f, full * 0.9f);
				return new PeneLens { pen = p.penetratingWorldLength, tip = tip, full = full, valid = true };
			}
			catch
			{
				return default(PeneLens);
			}
		}

		// DEPTH IS MEASURED AGAINST WHAT THIS HOLE ADMITS, not against the member's length.
		//
		// penetrationFactor feeds NON-LINEAR shaping — notably Lerp(..., pf*pf*pf) on the
		// outstroke and InverseLerp(0.7, 1, pf) in the deformation tolerance. Normalising by the
		// pene's full length means a SHALLOW hole can never push pf near 1, so every curve sits in
		// its bottom range and the stroke crawls; then warm-up admits a little more depth, pf
		// climbs, and the cube explodes — slow, slow, then far too fast. Hole-relative, pf spans
		// the full 0–1 across whatever this hole actually offers, so the shaping behaves the same
		// in a small hole as in a deep one.
		//
		// Both halves come from the same space or neither does: internals depth against the
		// hole's own capacity (the pair the game itself compares in maximaProfundidadPhysicsAlcanzada),
		// falling back to pene world lengths when no hole is registered.
		private float GetPenetrationDepth()
		{
			if (HoleDepthCapacity() > 0f)
			{
				return InternalsDepth();
			}
			return base.Session.Player.Character.pene.penetratingWorldLength;
		}

		// ANATOMICAL CEILING (AIChat: pastAnatomy). AIChat uses BOTH bases and this is where the
		// second one belongs: pene lengths drive the stroke TARGETS and the pop-out floor, while
		// HER internals depth provides an independent POSITION limit. That is what stops the
		// inward stroke punching past 100 % at speed — a threshold on the target alone cannot,
		// because the command overshoots it before the check reacts (same unit-space problem as
		// the outstroke). Reflected because maxProfundidadVirtual is [Obsolete(..., true)];
		// cached per hole so the reflection never runs per tick.
		private static BoneStretchedChain anatomicalChain;

		private static float anatomicalDepth;

		private float AnatomicalDepth()
		{
			BoneStretchedChain h = (Sequence != null) ? Sequence.hole : null;
			if (h == null) return 0f;
			if (ReferenceEquals(h, anatomicalChain)) return anatomicalDepth;

			float v = 0f;
			try
			{
				object cfg = h.holeConfig;
				if (cfg != null)
				{
					System.Reflection.FieldInfo fi = cfg.GetType().GetField("maxProfundidadVirtual",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
						| System.Reflection.BindingFlags.NonPublic);
					if (fi != null) v = (float)fi.GetValue(cfg);
				}
			}
			catch
			{
				v = 0f;
			}

			anatomicalChain = h;
			anatomicalDepth = (v > 0.0001f) ? v : 0f;
			return anatomicalDepth;
		}

		private float InternalsDepth()
		{
			BoneStretchedChain h = (Sequence != null) ? Sequence.hole : null;
			if (h == null) return 0f;
			return h.estadoDePuntos.actualLocal.penetratedDepthLocalInternals;
		}

		// Her authored depth scaled by the Forward (Deep) setting: at 100 % this is exactly her
		// anatomy, so over-reach can only ever be a deliberate slider choice.
		private bool PastAnatomy()
		{
			float anat = AnatomicalDepth();
			if (anat <= 0f) return false;
			float allowed = anat * TotalForwardFraction();
			return allowed > 0f && InternalsDepth() >= allowed;
		}

		private float GetPenetrationFactor()
		{
			return GetPenetrationDepth() / MaxWorldPenetration;
		}

		// OUTSTROKE FLOOR — AIChat's ShallowLen, verbatim in shape:
		//     floor = tip + (full - tip) * max(POPOUT_FLOOR_FRAC, UserBackwardTarget)
		// i.e. the floor is measured UP FROM THE TIP across the usable range, not as a fraction of
		// the whole length. Backward Target 0 % therefore sits at 15 % of the range above the
		// pop-out threshold (owner: "0 % should map to 15 %"), and higher settings stay deeper.
		// AIChat's POPOUT_SAFETY_FRAC. This is the floor AT REST; the speed margin below adds up to
		// PopoutSpeedMargin on top, so the floor tops out around 15 % at full speed — which is the
		// "0 % should map to 15 %" figure. Setting 0.15 here instead made 15 % the resting floor and
		// then added the margin on top of it, reaching ~27 % of the range and swallowing most of
		// the withdrawal.
		private const float PopoutFloorFrac = 0.03f;

		// Hole space has no tip offset, so the pop-out margin lives here instead. Used only when
		// the tip cannot be measured yet.
		private const float HoleFloorFrac = 0.15f;

		// If the tip is more than this share of the hole's depth, reserving it would consume most
		// of the stroke, so the floor drops back and the pene comes nearly all the way out — which
		// is the correct, necessary behaviour in a very shallow hole. NOTE worldTipPartLength
		// scales with the pene's overall length, so this branch selects itself: a large pene in a
		// small hole produces a large tip fraction and lands here without any size special-casing.
		private const float TipReserveMaxFrac = 0.4f;

		private const float ShallowHoleFloorFrac = 0.05f;

		// How far BELOW the reversal floor the hard pop-out guard sits, as a fraction of the span.
		// Small: just enough headroom that the turnaround can actually be reached.
		private const float GuardSlackFrac = 0.05f;

		// The entrance brake never scales below this, so the withdrawal always arrives at the
		// reversal point instead of decelerating asymptotically toward it.
		private const float BrakeMinScale = 0.35f;

		// Below this commanded step the position change is dominated by physics jitter, so the
		// sample would corrupt the ratio rather than refine it.
		private const float RatioSampleMinStep = 0.0005f;

		private static readonly Logger logger = Logger.Create<AutoThrustService>();

		// SPEED-SCALED FLOOR — also AIChat's, and it exists because of this exact symptom
		// (owner, 2026-08-04: "0 % might be a little too shallow as speed increases"). A
		// withdrawal that is safe when slow is not safe when fast: the reversal needs runway, and
		// the faster the stroke the more of it overshoots past the floor before the direction
		// flips. So the floor LIFTS with the current stroke speed — Backward 0 % stays genuinely
		// shallow when slow and gains margin as things get quick.
		private const float PopoutSpeedMargin = 0.12f;

		private float GetMinPenetrationExpectation()
		{
			// Hole space: the floor is a fraction of what this hole admits.
			float cap = HoleDepthCapacity();
			if (cap > 0f)
			{
				return cap * FloorFraction();
			}
			PeneLens L = ReadPeneLens();
			if (!L.valid)
			{
				Penis pene = base.Session.Player.Character.pene;
				return Mathf.Lerp(pene.worldTipPartLength * GetDepenetractionScaleFactor(),
					MaxWorldPenetration, UserBackwardTarget);
			}
			float range = Mathf.Max(0.0001f, L.full - L.tip);
			return L.tip + range * FloorFraction();
		}

		// CEILING — anchored at the tip across the SAME range as the floor, so Forward and Backward
		// percentages share one scale (AIChat: deepLen = tip + range * Deep%). Forward 100 % is
		// therefore fully seated, not 0.75 * length: the old form measured from ZERO while the
		// floor measures from the TIP, so the two ends of the pair meant different things.
		private float GetMaxPenetrationExpectation()
		{
			// Hole space: Forward 100 % = this hole's full depth, punch pushes past it.
			float cap = HoleDepthCapacity();
			if (cap > 0f)
			{
				return Mathf.Max(cap * TotalForwardFraction(), GetMinPenetrationExpectation());
			}
			PeneLens L = ReadPeneLens();
			if (!L.valid)
			{
				return Mathf.Lerp(GetMinPenetrationExpectation(), MaxWorldPenetration * 0.75f, UserForwardTarget);
			}
			float range = Mathf.Max(0.0001f, L.full - L.tip);
			float deepLen = L.tip + range * TotalForwardFraction();
			return Mathf.Max(deepLen, GetMinPenetrationExpectation());
		}

		private float GetDeformationFactor(float penetrationFactor)
		{
			Penis pene = base.Session.Player.Character.pene;
			float actualWorldLength = (pene.punta.physicBone.position - pene.@base.physicBone.position).magnitude;
			float deformationFactor = actualWorldLength / pene.worldLength;
			deformationFactor = Mathf.InverseLerp(0f, 0.9f - Mathf.InverseLerp(0.7f, 1f, penetrationFactor) * 0.3f, deformationFactor);
			if (Sequence.NonDeformedExitPRatio == 1f)
			{
				deformationFactor = 1f;
			}
			return deformationFactor;
		}

		private float GetPenetrationRatio()
		{
			return Mathf.InverseLerp(GetMinPenetrationExpectation(), GetMaxPenetrationExpectation(), GetPenetrationDepth());
		}

		private float GetDepenetrationThreshold()
		{
			return GetMinPenetrationExpectation() / MaxWorldPenetration;
		}

		// Velocity caps must be UNIT-STABLE across the two depth spaces.
		//
		// This used the measured span (max - min expectation) as the distance. That span is in
		// hole-internals units when a hole is registered and pene-world units otherwise — very
		// different magnitudes — so moving the basis to hole space silently collapsed every cap
		// and the outstroke crawled. The factors themselves are dimensionless (0..1 of whichever
		// space is active), so convert them with the CONTROLLER's own reference instead: the
		// stroke window as a fraction, times MaxDepth in effector-Z units. A narrow Forward/
		// Backward window still yields proportionally lower velocity, which is the behaviour the
		// original expression was after, but the number no longer depends on which space is live.
		private float GetVelocityForPenetrationFactor(float currentFactor, float targetFactor)
		{
			float windowFrac = Mathf.Max(0.05f, TotalForwardFraction() - FloorFraction());
			float commandSpan = windowFrac * MaxDepth;
			float v = Mathf.Abs((currentFactor - targetFactor) * commandSpan / Time.deltaTime) / GetThrustScaleFactor();
			// FLOOR THE GEOMETRY-DERIVED CAP. A tiny hole yields a tiny window and a tiny factor
			// delta, which multiplies out to a cap near zero and stalls the stroke. Only the
			// geometric cap is floored — the bend throttle and the bend backoff are deliberate
			// safety reductions and must still be able to slow it below this.
			//
			// TIME-BASED FLOOR as well: a fixed MinVelocity is a fixed SPEED, and in a shallow or
			// not-yet-warmed hole the window is short, so the stroke still crawls — it is slow
			// relative to the distance it has to cover. This floors the RATE instead: whatever the
			// window, a stroke may not take longer than MaxStrokeSeconds to traverse it.
			// NOT applied here any more. This function returns a CAP, and the caller uses it as
			// Min(commanded, cap) — raising a ceiling cannot speed up a stroke that is already
			// slower than it. That is why [AUDIT] run 2 logged 17.8 s, 13.3 s and 9.2 s strokes
			// against a nominal 1.2 s limit: the floor was written and deployed and had no path
			// to the output. It now lives in StrokeRateFloor(), applied on the FLOOR side.
			return Mathf.Max(MinVelocity, v);
		}

		/// <summary>
		/// Minimum stroke SPEED needed to traverse the current window within MaxStrokeSeconds.
		/// MinVelocity is a fixed speed, so in a shallow or not-yet-warmed hole the window is
		/// short and the stroke still crawls relative to the distance it must cover; this floors
		/// the RATE instead. Applied BEFORE bendSpeedScale on purpose — the bend backoff is a
		/// safety reduction and must still be able to slow the stroke below this comfort floor.
		/// </summary>
		/// <summary>
		/// HARD floor applied AFTER every reduction, the bend backoff included.
		///
		/// The reductions MULTIPLY: BendThrottle floors at 0.10 and bendSpeedScale at 0.25, so
		/// their product bottoms out near 0.025 of commanded — a dead stop that still reports a
		/// velocity. Every previous floor sat upstream of one or both of them, which is how the
		/// stroke could read "moving" while visibly stationary. Safety reductions may slow the
		/// stroke; they may not stop it. A stalled stroke at a bad angle is not a safe state — it
		/// is precisely the state that had to be broken out of by hand.
		/// </summary>
		public float AbsoluteMinVelocity { get; set; } = 0.09f;

		private float AbsoluteFloor(float v)
		{
			float f = Mathf.Max(0f, AbsoluteMinVelocity);
			if (v >= f) return v;
			logger.InfoRare(120,
				"[AutoThrust/floor] velocity {0:F4} raised to absolute floor {1:F4} "
				+ "(bendScale={2:F2} bend={3:F3})", v, f, bendSpeedScale, BendDeflection);
			return f;
		}

		private float StrokeRateFloor()
		{
			float windowFrac = Mathf.Max(0.05f, TotalForwardFraction() - FloorFraction());
			float commandSpan = windowFrac * MaxDepth;
			return commandSpan / MaxStrokeSeconds / Mathf.Max(0.0001f, GetThrustScaleFactor());
		}

		private float GetDepenetractionScaleFactor()
		{
			float activeVelocity = GetVelocity(MotionType.OUT);
			return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(MinVelocity, 2f, activeVelocity * GetThrustScaleFactor()));
		}

		// ══ ANGLE READOUT ════════════════════════════════════════════════════════════════════
		// A plain side-by-side of the two directions the whole alignment effort is trying to make
		// collinear, so they can be eyeballed against what is on screen. Deliberately independent
		// of the audit and of penetration: the hole's axis and the shaft's axis both exist as soon
		// as a hole is registered, and being able to read them BEFORE entry is what makes an
		// approach-angle problem distinguishable from an in-hole one.
		//
		// Sign convention, stated because it is the thing most likely to be misread: HoleEntrance
		// .forward points OUT of the hole, so a perfectly aligned shaft (which points IN) is
		// ANTI-parallel to it. `align` below is already folded for this — 0 deg means perfect.
		// Drawn with Tracer, the same utility AutoSeek's own debug lines use, so the two can be on
		// at once and read together. Colours:
		//
		//   CYAN    the hole's axis, drawn one pene-length out of the entrance — where the shaft
		//           would lie if the insertion were perfectly straight. This is the reference.
		//   YELLOW  the shaft itself, base to tip. Overlaying cyan = aligned; the visible fan
		//           between them IS the alignment error, and its size is what the numbers call
		//           `align`.
		//   GREEN   the line cast's target station at L/2 out along the axis, drawn as a short
		//           cross, plus a line from it to the actual base. That connector's length is the
		//           error the corrector is driving to zero — if it never shrinks, the corrector
		//           is not working, whatever the shaft looks like.
		//   MAGENTA the hole entrance normal stub, so the entrance's own orientation is legible
		//           even when the shaft is nowhere near it.
		//
		// Independent of penetration on purpose: reading the approach angle BEFORE entry is what
		// separates an approach problem from an in-hole one.
		public bool AngleDebug { get; set; }

		private static readonly Color AngleAxisColor = new Color(0f, 0.9f, 1f);
		private static readonly Color AngleShaftColor = new Color(1f, 0.92f, 0.2f);
		private static readonly Color AngleTargetColor = new Color(0.2f, 1f, 0.3f);
		private static readonly Color AngleNormalColor = new Color(1f, 0.2f, 1f);

		// Lifetime MUST be Tracer's own 0.2 s, not Time.deltaTime. DebugLine expires through a
		// coroutine that deactivates the object and returns it to the pool; at a one-frame
		// lifetime the line is recycled before it reliably renders, which is exactly why the
		// first version of this drew nothing. AutoSeek redraws every frame at 0.2 s and is
		// visible — copy the proven call, do not invent a shorter one.
		private const float AngleLineLife = 0.2f;

		/// <summary>Closest of the three hole entrances to the tip, mirroring AutoSeek's own
		/// choice, so the readout works during the approach when no Sequence exists yet.</summary>
		private Transform NearestHoleEntrance()
		{
			try
			{
				FemaleChar ch = base.Session.Guest.Impl;
				Vector3 tip = PeneTip();
				Transform best = null;
				float bestD = float.MaxValue;
				Transform[] cands = new Transform[]
				{
					base.Session.Guest.Puppet.GetIKBoneTransform(ch.vagHole.entrada),
					base.Session.Guest.Puppet.GetIKBoneTransform(ch.anusHole.entrada),
					base.Session.Guest.Puppet.GetIKBoneTransform(ch.bocaHole.entrada)
				};
				for (int i = 0; i < cands.Length; i++)
				{
					if (cands[i] == null) continue;
					float d = Vector3.Distance(tip, cands[i].position);
					if (d < bestD) { bestD = d; best = cands[i]; }
				}
				return best;
			}
			catch
			{
				return null;
			}
		}

		private void UpdateAngleDebug()
		{
			if (!AngleDebug) return;
			Vector3 B, T;
			try { B = PeneBase(); T = PeneTip(); }
			catch
			{
				logger.InfoRare(60, "[ANGLE] pene geometry unreadable - nothing to draw");
				return;
			}
			Vector3 shaft = T - B;
			if (shaft.sqrMagnitude < 1E-09f)
			{
				logger.InfoRare(60, "[ANGLE] zero-length shaft (base==tip) - nothing to draw");
				return;
			}
			Vector3 s = shaft.normalized;
			float L = PeneLengthNow();
			if (L <= 0.0001f) L = shaft.magnitude;

			// The shaft, always — this one needs no hole and no penetration.
			Tracer.DrawLineOnTop(B, T, AngleShaftColor, AngleLineLife);

			Transform he = null;
			try { he = (Sequence != null) ? Sequence.HoleEntrance : null; } catch { }
			// NO SEQUENCE = the approach, which is the case this readout is most useful in.
			// Sequence.HoleEntrance only exists once thrusting is under way, so before entry fall
			// back to the nearest hole — the same target AutoSeek would choose.
			if (he == null) he = NearestHoleEntrance();
			if (he == null)
			{
				// Distinguishes "not drawing at all" from "drawing, but no hole anywhere" — the
				// shaft line is still on screen in this state.
				logger.InfoRare(60,
					"[ANGLE] no hole found - shaft only. dir=({0:F3},{1:F3},{2:F3}) len={3:F3}",
					s.x, s.y, s.z, L);
				return;
			}

			Vector3 E = he.position;
			Vector3 outward = he.forward.normalized;

			// The reference axis: where a straight shaft would sit. Drawn a full length and a half
			// so it reads as a line rather than a dash at this scale.
			Tracer.DrawLineOnTop(E, E + outward * (L * 1.5f), AngleAxisColor, AngleLineLife);
			Tracer.DrawLineOnTop(E, E + outward * (L * 0.2f), AngleNormalColor, AngleLineLife);

			// The line-cast station at L/2 and the connector to the real base. The connector is
			// the error being corrected; watching it shrink is the whole point.
			Vector3 station = E + outward * StrokeLength();
			Vector3 up = Vector3.Cross(outward, Vector3.right);
			if (up.sqrMagnitude < 1E-06f) up = Vector3.Cross(outward, Vector3.forward);
			up = up.normalized;
			Vector3 right = Vector3.Cross(outward, up).normalized;
			float tick = L * 0.25f;
			Tracer.DrawLineOnTop(station - up * tick, station + up * tick, AngleTargetColor, AngleLineLife);
			Tracer.DrawLineOnTop(station - right * tick, station + right * tick, AngleTargetColor, AngleLineLife);
			Tracer.DrawLineOnTop(station, B, AngleTargetColor, AngleLineLife);

			// Numeric companion to the lines, so a wrong-looking picture can be checked against
			// the values the aligner is actually using.
			Vector3 hIn = -outward;
			float pen = 0f;
			try { pen = base.Session.Player.Character.pene.penetratingWorldLength; } catch { }
			logger.InfoRare(30,
				"[ANGLE] align={0:F1}deg holeIn=({1:F3},{2:F3},{3:F3}) pene=({4:F3},{5:F3},{6:F3}) "
				+ "L={7:F3} baseToStation={8:F4} bend={9:F3} pen={10:F3} {11}",
				Vector3.Angle(s, hIn), hIn.x, hIn.y, hIn.z, s.x, s.y, s.z, L,
				(station - B).magnitude, BendDeflection, pen,
				(pen > 0.0005f) ? "INSIDE" : "outside");
		}

		// ══ STROKE AUDIT ═════════════════════════════════════════════════════════════════════
		// A CONTROLLED A/B, not a log dump. The eye cannot see any of the quantities that decide
		// whether the alignment work is helping: the perpendicular deviation of the base path
		// from the hole's axis (millimetres), the angle between the achieved motion and the axis
		// we THINK we are commanding (a sign error here looks like "a bit off" and is actually
		// inverted), or whether a feature quietly costs 20 % of stroke rate to buy 5 % of
		// straightness. So the audit cycles the feature set itself, holds each combination for a
		// fixed number of strokes, and reports medians per arm.
		//
		// Pre-registered verdicts (theory-discipline T2 — written before ANY data was taken, so
		// the thresholds cannot be fitted to the result afterwards):
		//
		//  V1 FRAME SANITY   with StrokeStraight on, the angle between achieved base displacement
		//                    and the hole axis must be < 25 deg. Above 90 deg the local-frame
		//                    mapping is INVERTED — the runaway failure this toggle exists to guard.
		//  V2 STRAIGHTNESS   StrokeStraight (arm 3) must cut median pathPerp >= 20 % vs arm 1.
		//  V3 ALIGNMENT      any align arm must cut median shaftAngle >= 20 % vs the OFF baseline.
		//  V4 NO BEND COST   no arm may raise median bendPeak more than 10 % over baseline.
		//  V5 NO RATE COST   no arm may raise median strokeSeconds more than 25 % over baseline.
		//  V6 CONVERGENCE    median |axial| over the arm's last third < its first third.
		//  V7 NO POPOUTS     no arm may raise popouts per stroke over baseline.
		//  V8 INSTRUMENT     if the hole entrance itself moves faster than 0.05 m/s (median), the
		//                    target is not stationary, the line cast is chasing a moving station,
		//                    and V2/V3/V6 are reported INCONCLUSIVE rather than pass/fail.
		//
		// V8 is the validation gate: an instrument that cannot see a stationary target cannot
		// judge a stationary-target theory, and reporting a number anyway is the worse failure.
		public bool StrokeAudit
		{
			get { return auditOn; }
			set
			{
				if (value == auditOn) return;
				auditOn = value;
				if (value) AuditStart(); else AuditStop(true);
			}
		}

		// SIX arms, not five: arm 5 repeats arm 0's OFF baseline at the END. Run 2/3 showed
		// bendPeak climbing monotonically THROUGH THE WHOLE RUN, including inside the opening
		// baseline arm itself (0.047 -> 0.187 over its own 8 strokes). Bend was ratcheting with
		// TIME, not with the arm under test, which silently penalised every later arm and made
		// all four V4 verdicts unsafe to act on. A closing baseline measures that drift directly:
		// arm5 - arm0 is the time confound, and V4 is judged against the LARGER of the two so a
		// feature is only blamed for what exceeds the drift.
		/// <summary>Live progress, surfaced in Mission Control. The audit takes minutes and its
		/// only previous completion signal was a log line, which is not visible while playing —
		/// so there was no way to tell a finished run from a stalled one.</summary>
		public string AuditStatus
		{
			get
			{
				if (!auditArmed) return auditRun > 0 ? "AUDIT: COMPLETE (run " + auditRun + ")" : "AUDIT: off";
				return "AUDIT arm " + (auditArm + 1) + "/" + AuditArms + " (" + auditArmNames[auditArm]
					+ ") stroke " + (auditStrokeInArm + 1) + "/" + AuditStrokesPerArm;
			}
		}

		private const int AuditArms = 6;

		private const int AuditStrokesPerArm = 8;

		private const int AuditMaxSamples = 512;

		private static readonly string[] auditArmNames =
		{
			"OFF(baseline)", "align", "align+coarse", "align+straight", "align+coarse+straight",
			"OFF(closing baseline)"
		};

		private bool auditOn, auditArmed;

		private int auditArm, auditStrokeInArm, auditRun;

		private bool savedAlignHips, savedAlignLateral, savedAlignCoarse, savedStrokeStraight;

		private float aStrokeT, aBendPeak, aBendSum, aAxialSum, aPerpSum, aShaftSum, aEvelSum;

		private float aPathPerpSq, aDepthMax, aDepthMin;

		private int aTicks, aPopouts;

		private Vector3 aLastBase, aLastE, aDispAccum;

		private bool aPrimed, aLastPenetrating;

		private MotionType aLastMotion;

		private float auditEvelSum;

		private int auditEvelN;

		private readonly float[,] auditBank = new float[AuditArms * 9, AuditMaxSamples];

		private readonly int[] auditCount = new int[AuditArms];

		private const int MBendPeak = 0;
		private const int MBendMean = 1;
		private const int MPathPerp = 2;
		private const int MAxisErr = 3;
		private const int MShaftAng = 4;
		private const int MAxial = 5;
		private const int MPerp = 6;
		private const int MSeconds = 7;
		private const int MPopout = 8;

		// ── MANIPULATION CHECKS (per-arm positive controls) ──────────────────────────────────
		// An outcome metric cannot tell "this feature ran and did not help" apart from "this
		// feature never ran". Both read as identical-to-baseline. Run 2 could not distinguish
		// them for any arm, which made every "no effect" reading in it unfalsifiable.
		//
		// So each arm now carries a positive control fed from the REAL code path: did the
		// mechanism actuate, by how much, and within the time its own gains imply? An arm that
		// fails its check has its outcome verdicts reported INVALID rather than as a result,
		// because a feature that never fired has not been tested.
		//
		//  arm 0/5  OFF       expect EXACTLY ZERO actuation. Proves the arms are genuinely
		//                     different configurations and the toggles took effect at all.
		//  arm 1    align     expect |alignZBias| to move. At AlignAcquireRatePerSec = 0.25/s a
		//                     real correction shows >= 0.02 inside a second; near-zero means the
		//                     safety gate held it off, and gateBlocked reports exactly that.
		//  arm 2    coarse    expect avatar displacement > 0 AND self-termination. At
		//                     CoarseRatePerSec = 0.05 m/s the 4 cm tolerance is ~1 s of travel,
		//                     so in an 8-stroke arm it must both move and stop. Zero
		//                     displacement means arm 2 was silently a repeat of arm 1.
		//  arm 3    straight  expect perpendicular commands AND low tracking error. High error =
		//                     commanding without achieving (blocked / no headroom). A high bail
		//                     count = the obliquity guard fired and the arm was again arm 1.
		//  arm 4    all       every check above PLUS contention: coarse and straight both drive
		//                     x/y, so opposite-sign commands on one axis in the same tick means
		//                     they are fighting. That is the "clashes with other features" case,
		//                     and no outcome metric can see it.
		//
		// TIMELINE. Each mechanism has a rate, so each needs a minimum exposure. An arm shorter
		// than its own settling time reports TOO-SHORT, not FAIL: a slow corrector denied its
		// seconds has not been refuted, and calling that a failure would be the same error as
		// believing a feature that never ran.
		private const float MinArmSecondsAlign = 2f;

		private const float MinArmSecondsCoarse = 4f;

		private float armSeconds, armAlignAbs, armCoarseDisp, armStraightTrackErr;

		private int armTicks, armGateBlocked, armAlignTicks, armStraightTicks, armStraightBail,
			armContention;

		private bool armCoarseStopped;

		private float lastCoarseStepAt;

		private readonly bool[] auditArmOk = new bool[AuditArms];

		private readonly string[] auditArmDetail = new string[AuditArms];

		private void AuditResetArmChecks()
		{
			armSeconds = 0f;
			armAlignAbs = 0f;
			armCoarseDisp = 0f;
			armStraightTrackErr = 0f;
			armTicks = 0;
			armGateBlocked = 0;
			armAlignTicks = 0;
			armStraightTicks = 0;
			armStraightBail = 0;
			armContention = 0;
			armCoarseStopped = false;
			lastCoarseStepAt = 0f;
		}

		private void AuditNoteAlignStep(float zStep)
		{
			if (!auditArmed) return;
			armAlignAbs += Mathf.Abs(zStep);
			armAlignTicks++;
		}

		private void AuditNoteCoarseStep(float stepLen)
		{
			if (!auditArmed) return;
			armCoarseDisp += stepLen;
			lastCoarseStepAt = armSeconds;
		}

		private void AuditNoteGateBlocked()
		{
			if (auditArmed) armGateBlocked++;
		}

		private void AuditNoteStraight(float trackErr, bool bailed)
		{
			if (!auditArmed) return;
			if (bailed) { armStraightBail++; return; }
			armStraightTicks++;
			armStraightTrackErr += trackErr;
		}

		private void AuditNoteContention()
		{
			if (auditArmed) armContention++;
		}

		/// <summary>Did this arm's mechanism actually actuate? False = its outcome numbers must
		/// not be believed.</summary>
		private bool AuditArmValid(int arm, out string detail)
		{
			float gateFrac = (armTicks > 0) ? ((float)armGateBlocked / (float)armTicks) : 0f;
			float trackErr = (armStraightTicks > 0) ? (armStraightTrackErr / (float)armStraightTicks) : 0f;
			int stTotal = armStraightTicks + armStraightBail;
			float bailFrac = (stTotal > 0) ? ((float)armStraightBail / (float)stTotal) : 0f;
			bool wantAlign = (arm >= 1 && arm <= 4);
			bool wantCoarse = (arm == 2 || arm == 4);
			bool wantStraight = (arm == 3 || arm == 4);
			bool ok = true;
			string why = "";

			if (!wantAlign)
			{
				if (armAlignTicks > 0 || armCoarseDisp > 0f || armStraightTicks > 0)
				{
					ok = false;
					why += "OFF-arm ACTUATED (align=" + armAlignTicks + " coarse="
						+ armCoarseDisp.ToString("F3") + " straight=" + armStraightTicks
						+ ") - the toggles did not take; ";
				}
			}
			else if (armSeconds < MinArmSecondsAlign)
			{
				ok = false;
				why += "TOO-SHORT for align (" + armSeconds.ToString("F1") + "s < "
					+ MinArmSecondsAlign.ToString("F0") + "s); ";
			}
			else if (armAlignAbs < 0.02f)
			{
				ok = false;
				why += "align NEVER ACTUATED (sum|zStep|=" + armAlignAbs.ToString("F4")
					+ ", gateBlocked=" + gateFrac.ToString("P0") + "); ";
			}

			if (wantCoarse)
			{
				if (armSeconds < MinArmSecondsCoarse)
				{
					ok = false;
					why += "TOO-SHORT for coarse (" + armSeconds.ToString("F1") + "s < "
						+ MinArmSecondsCoarse.ToString("F0") + "s); ";
				}
				else if (armCoarseDisp <= 0f)
				{
					ok = false;
					why += "coarse NEVER MOVED the avatar - this arm was really arm 1; ";
				}
				else if (!armCoarseStopped)
				{
					ok = false;
					why += "coarse never SELF-TERMINATED (disp=" + armCoarseDisp.ToString("F3")
						+ "m, still stepping at arm end); ";
				}
			}

			if (wantStraight)
			{
				if (bailFrac > 0.5f)
				{
					ok = false;
					why += "straight BAILED on " + bailFrac.ToString("P0")
						+ " of ticks (axis too oblique) - this arm was really arm 1; ";
				}
				else if (armStraightTicks == 0)
				{
					ok = false;
					why += "straight NEVER RAN; ";
				}
				else if (trackErr > 0.01f)
				{
					ok = false;
					why += "straight commanded but did NOT TRACK (meanErr=" + trackErr.ToString("F4")
						+ "m > 0.01) - blocked or out of headroom; ";
				}
			}

			if (arm == 4 && armContention > 0)
			{
				float cf = (armTicks > 0) ? ((float)armContention / (float)armTicks) : 0f;
				why += "CONTENTION coarse-vs-straight opposite-sign on " + cf.ToString("P0")
					+ " of ticks; ";
				if (cf > 0.1f) ok = false;
			}

			detail = "secs=" + armSeconds.ToString("F1")
				+ " alignSum=" + armAlignAbs.ToString("F4")
				+ " alignTicks=" + armAlignTicks
				+ " gateBlocked=" + gateFrac.ToString("P0")
				+ " coarseDisp=" + armCoarseDisp.ToString("F3")
				+ " coarseStopped=" + armCoarseStopped
				+ " straightTicks=" + armStraightTicks
				+ " bail=" + bailFrac.ToString("P0")
				+ " trackErr=" + trackErr.ToString("F4")
				+ " contention=" + armContention
				+ (ok ? "" : "  << " + why);
			return ok;
		}

		private void AuditStart()
		{
			auditRun++;
			savedAlignHips = AlignHips;
			savedAlignLateral = AlignLateral;
			savedAlignCoarse = AlignCoarse;
			savedStrokeStraight = StrokeStraight;
			for (int i = 0; i < AuditArms; i++) auditCount[i] = 0;
			auditArm = 0;
			auditStrokeInArm = 0;
			for (int i = 0; i < AuditArms; i++) { auditArmOk[i] = false; auditArmDetail[i] = "not reached"; }
			AuditResetArmChecks();
			auditEvelSum = 0f;
			auditEvelN = 0;
			auditArmed = true;
			aPrimed = false;
			AuditResetStroke();
			AuditApplyArm();
			logger.Info(
				"[AUDIT] run={0} START arms={1} strokesPerArm={2} - cycling feature sets and "
				+ "measuring; do NOT touch the align toggles until it reports",
				auditRun, AuditArms, AuditStrokesPerArm);
		}

		private void AuditStop(bool restore)
		{
			auditArmed = false;
			if (restore)
			{
				AlignHips = savedAlignHips;
				AlignLateral = savedAlignLateral;
				AlignCoarse = savedAlignCoarse;
				StrokeStraight = savedStrokeStraight;
			}
		}

		private void AuditApplyArm()
		{
			AlignLateral = false;
			AlignHips = (auditArm >= 1 && auditArm <= 4);
			AlignCoarse = (auditArm == 2 || auditArm == 4);
			StrokeStraight = (auditArm == 3 || auditArm == 4);
			// Arm 4 is the combination that matters most: the end state is a SINGLE alignment
			// toggle, so the sub-features have to work together, not merely each in isolation.
			logger.Info("[AUDIT] run={0} ARM {1} = {2} (alignHips={3} coarse={4} straight={5})",
				auditRun, auditArm, auditArmNames[auditArm], AlignHips, AlignCoarse, StrokeStraight);
		}

		private void AuditResetStroke()
		{
			aStrokeT = 0f;
			aBendPeak = 0f;
			aBendSum = 0f;
			aAxialSum = 0f;
			aPerpSum = 0f;
			aShaftSum = 0f;
			aEvelSum = 0f;
			aPathPerpSq = 0f;
			aTicks = 0;
			aPopouts = 0;
			aDispAccum = Vector3.zero;
			aDepthMax = -1E+09f;
			aDepthMin = 1E+09f;
		}

		private void AuditTick(float dt)
		{
			if (!auditArmed || dt <= 0f) return;
			Transform he;
			try { he = Sequence.HoleEntrance; } catch { return; }
			if (he == null) return;

			Vector3 E = he.position;
			Vector3 outward = he.forward.normalized;
			Vector3 B = PeneBase();
			Vector3 T = PeneTip();
			float L = PeneLengthNow();
			if (L <= 0.0001f) return;

			bool pen = false;
			try { pen = base.Session.Player.Character.pene.penetratingWorldLength > 0.0005f; }
			catch { }

			if (!aPrimed)
			{
				aPrimed = true;
				aLastBase = B;
				aLastE = E;
				aLastPenetrating = pen;
				aLastMotion = Sequence.Motion;
				return;
			}

			// pathPerp: how far the BASE strays off the hole's own axis line during the stroke.
			// The direct measurement of "is the stroke a straight line into the hole", and
			// invisible to the eye at the scale that matters — a centimetre already bows the shaft.
			Vector3 rel = B - E;
			float alongLine = Vector3.Dot(rel, outward);
			float perpDist = (rel - outward * alongLine).magnitude;
			aPathPerpSq += perpDist * perpDist;

			// Line-cast error, recomputed here independently of the corrector, so the audit is
			// not merely reading back the corrector's own opinion of itself.
			Vector3 target = E + outward * StrokeLength();
			Vector3 e = target - B;
			float axial = Vector3.Dot(e, outward);
			aAxialSum += Mathf.Abs(axial);
			aPerpSum += (e - outward * axial).magnitude;

			// Shaft vs axis: the actual straightness of the insertion, in degrees.
			Vector3 shaft = T - B;
			if (shaft.sqrMagnitude > 1E-09f)
			{
				aShaftSum += Vector3.Angle(shaft.normalized, -outward);
			}

			// Achieved base displacement accumulated as a VECTOR, so its direction survives.
			aDispAccum += B - aLastBase;
			aEvelSum += (E - aLastE).magnitude / dt;
			aLastBase = B;
			aLastE = E;

			float bend = BendDeflection;
			if (bend > aBendPeak) aBendPeak = bend;
			aBendSum += bend;

			float pf = GetPenetrationFactor();
			if (pf > aDepthMax) aDepthMax = pf;
			if (pf < aDepthMin) aDepthMin = pf;

			if (aLastPenetrating && !pen) aPopouts++;
			aLastPenetrating = pen;

			aStrokeT += dt;
			aTicks++;
			armSeconds += dt;
			armTicks++;
			// Coarse "self-terminated" means it has gone quiet for a full second while still
			// enabled — the observable form of reaching tolerance.
			if (armCoarseDisp > 0f && (armSeconds - lastCoarseStepAt) > 1f) armCoarseStopped = true;

			// A stroke boundary is the OUT -> IN reversal: one complete out-and-back per sample.
			MotionType m = Sequence.Motion;
			bool boundary = (aLastMotion == MotionType.OUT && m == MotionType.IN);
			aLastMotion = m;
			if (!boundary || aTicks < 4) return;

			AuditCommitStroke(outward);
		}

		private void AuditCommitStroke(Vector3 outward)
		{
			int arm = auditArm;
			int n = auditCount[arm];
			if (n < AuditMaxSamples)
			{
				float inv = 1f / (float)Mathf.Max(1, aTicks);
				// Angle between the motion we ACHIEVED and the axis we intended to move along.
				// Folded to the nearer of +/-outward because a stroke travels both ways.
				float axisErr = (aDispAccum.sqrMagnitude > 1E-08f)
					? Mathf.Min(Vector3.Angle(aDispAccum.normalized, outward),
						Vector3.Angle(aDispAccum.normalized, -outward))
					: -1f;

				auditBank[arm * 9 + MBendPeak, n] = aBendPeak;
				auditBank[arm * 9 + MBendMean, n] = aBendSum * inv;
				auditBank[arm * 9 + MPathPerp, n] = Mathf.Sqrt(aPathPerpSq * inv);
				auditBank[arm * 9 + MAxisErr, n] = axisErr;
				auditBank[arm * 9 + MShaftAng, n] = aShaftSum * inv;
				auditBank[arm * 9 + MAxial, n] = aAxialSum * inv;
				auditBank[arm * 9 + MPerp, n] = aPerpSum * inv;
				auditBank[arm * 9 + MSeconds, n] = aStrokeT;
				auditBank[arm * 9 + MPopout, n] = aPopouts;
				auditCount[arm] = n + 1;

				logger.Info(
					"[AUDIT] run={0} arm={1} stroke={2} secs={3:F2} bendPeak={4:F3} bendMean={5:F3} "
					+ "pathPerp={6:F4} axisErr={7:F1} shaftAng={8:F1} axial={9:F4} perp={10:F4} "
					+ "depth=[{11:F2},{12:F2}] popouts={13} Evel={14:F3}",
					auditRun, arm, auditStrokeInArm, aStrokeT, aBendPeak, aBendSum * inv,
					Mathf.Sqrt(aPathPerpSq * inv), axisErr, aShaftSum * inv, aAxialSum * inv,
					aPerpSum * inv, aDepthMin, aDepthMax, aPopouts, aEvelSum * inv);

				auditEvelSum += aEvelSum * inv;
				auditEvelN++;
			}

			AuditResetStroke();
			auditStrokeInArm++;
			if (auditStrokeInArm < AuditStrokesPerArm) return;

			// Close the arm out: judge whether its mechanism actually actuated BEFORE moving on,
			// while the counters still belong to it.
			string detail;
			auditArmOk[auditArm] = AuditArmValid(auditArm, out detail);
			auditArmDetail[auditArm] = detail;
			logger.Info("[AUDIT/CHECK] run={0} arm={1} ({2}) -> {3} | {4}",
				auditRun, auditArm, auditArmNames[auditArm],
				auditArmOk[auditArm] ? "ACTUATED" : "NOT VALIDLY TESTED", detail);

			auditStrokeInArm = 0;
			auditArm++;
			if (auditArm < AuditArms)
			{
				AuditResetArmChecks();
				AuditApplyArm();
				return;
			}
			AuditReport();
			AuditStop(true);
			auditOn = false;
		}

		private float AuditMedian(int arm, int metric, int fromThird, int toThird)
		{
			int n = auditCount[arm];
			if (n <= 0) return float.NaN;
			int lo = n * fromThird / 3;
			int hi = n * toThird / 3;
			if (hi <= lo) { lo = 0; hi = n; }
			int c = hi - lo;
			float[] tmp = new float[c];
			for (int i = 0; i < c; i++) tmp[i] = auditBank[arm * 9 + metric, lo + i];
			Array.Sort(tmp);
			return tmp[c / 2];
		}

		private float AuditMedian(int arm, int metric)
		{
			return AuditMedian(arm, metric, 0, 3);
		}

		private static string Verdict(bool ok, bool inconclusive)
		{
			return inconclusive ? "INCONCLUSIVE" : (ok ? "PASS" : "FAIL");
		}

		private void AuditReport()
		{
			float evel = (auditEvelN > 0) ? (auditEvelSum / (float)auditEvelN) : 0f;
			bool moving = evel > 0.05f;

			logger.Info("[AUDIT/REPORT] run={0} ===================================", auditRun);
			logger.Info("[AUDIT/REPORT] arm | n | bendPeak bendMean pathPerp axisErr shaftAng "
				+ "axial perp secs popouts");
			for (int a = 0; a < AuditArms; a++)
			{
				logger.Info("[AUDIT/REPORT] {0} ({1}) n={2} | {3:F3} {4:F3} {5:F4} {6:F1} {7:F1} "
					+ "{8:F4} {9:F4} {10:F2} {11:F2}",
					a, auditArmNames[a], auditCount[a],
					AuditMedian(a, MBendPeak), AuditMedian(a, MBendMean), AuditMedian(a, MPathPerp),
					AuditMedian(a, MAxisErr), AuditMedian(a, MShaftAng), AuditMedian(a, MAxial),
					AuditMedian(a, MPerp), AuditMedian(a, MSeconds), AuditMedian(a, MPopout));
			}

			// MANIPULATION CHECKS FIRST. An outcome verdict on an arm whose mechanism never fired
			// is not a weak result, it is a meaningless one, so these are printed before any V-line
			// and they veto the arms they belong to.
			for (int a = 0; a < AuditArms; a++)
			{
				logger.Info("[AUDIT/REPORT] M{0} {1} -> {2} | {3}",
					a, auditArmNames[a], auditArmOk[a] ? "ACTUATED" : "NOT VALIDLY TESTED",
					auditArmDetail[a]);
			}

			logger.Info("[AUDIT/REPORT] V8 INSTRUMENT holeEntranceSpeed={0:F4} m/s -> {1}",
				evel, moving ? "TARGET IS MOVING - V2/V3/V6 downgraded" : "stationary, verdicts valid");

			// V1 frame sanity — the check that catches an inverted local-frame mapping.
			float ae3 = AuditMedian(3, MAxisErr);
			bool ok3 = auditArmOk[3];
			bool v1inv = !float.IsNaN(ae3) && ae3 > 90f;
			logger.Info("[AUDIT/REPORT] V1 FRAME axisErr(straight)={0:F1}deg -> {1}{2}",
				ae3, Verdict(!float.IsNaN(ae3) && ae3 < 25f, float.IsNaN(ae3) || !ok3),
				v1inv ? "  *** INVERTED LOCAL FRAME - disable Straight-line stroke ***" : "");

			// V2 straightness — straight vs align-only, alignment otherwise identical.
			float p1 = AuditMedian(1, MPathPerp);
			float p3 = AuditMedian(3, MPathPerp);
			float d2 = (p1 > 0f) ? ((p1 - p3) / p1) : float.NaN;
			logger.Info("[AUDIT/REPORT] V2 STRAIGHTNESS pathPerp {0:F4} -> {1:F4} ({2:P0}) -> {3}",
				p1, p3, d2, Verdict(d2 >= 0.2f, moving || float.IsNaN(d2) || !auditArmOk[3] || !auditArmOk[1]));

			// V3 alignment — best align arm vs the OFF baseline.
			float s0 = AuditMedian(0, MShaftAng);
			float best = float.MaxValue;
			int bestArm = -1;
			for (int a = 1; a < AuditArms - 1; a++)
			{
				float s = AuditMedian(a, MShaftAng);
				if (!float.IsNaN(s) && auditArmOk[a] && s < best) { best = s; bestArm = a; }
			}
			float d3 = (s0 > 0f && bestArm >= 0) ? ((s0 - best) / s0) : float.NaN;
			logger.Info("[AUDIT/REPORT] V3 ALIGNMENT shaftAng {0:F1} -> {1:F1} (arm {2}, {3:P0}) -> {4}",
				s0, best, bestArm, d3, Verdict(d3 >= 0.2f, moving || float.IsNaN(d3)));

			// V4/V5/V7 regressions — a feature that buys straightness with bend, stroke rate or
			// pop-outs has not helped, and that trade is exactly what is invisible while playing.
			float b0 = AuditMedian(0, MBendPeak);
			float t0 = AuditMedian(0, MSeconds);
			float o0 = AuditMedian(0, MPopout);

			// TIME CONFOUND. Both baselines ran with every feature off, so any difference between
			// them is drift, not treatment. V4 is judged against whichever baseline is worse, so a
			// feature is blamed only for what it adds ON TOP of the drift.
			float bEnd = AuditMedian(AuditArms - 1, MBendPeak);
			float drift = (!float.IsNaN(bEnd) && b0 > 0f) ? ((bEnd - b0) / b0) : float.NaN;
			logger.Info("[AUDIT/REPORT] DRIFT bendPeak baseline-open={0:F3} baseline-close={1:F3} "
				+ "({2:P0}) - {3}",
				b0, bEnd, drift,
				(!float.IsNaN(drift) && drift > 0.1f)
					? "BEND RATCHETS WITH TIME - a real defect, and it confounds V4"
					: "no material drift; V4 comparisons stand");
			float b0eff = (!float.IsNaN(bEnd) && bEnd > b0) ? bEnd : b0;

			for (int a = 1; a < AuditArms - 1; a++)
			{
				float b = AuditMedian(a, MBendPeak);
				bool va = auditArmOk[a];
				float t = AuditMedian(a, MSeconds);
				float o = AuditMedian(a, MPopout);
				logger.Info("[AUDIT/REPORT] arm {0}: V4 bend {1:F3}->{2:F3} {3} | V5 secs "
					+ "{4:F2}->{5:F2} {6} | V7 popout {7:F2}->{8:F2} {9}",
					a, b0eff, b, Verdict(!(b > b0eff * 1.1f), float.IsNaN(b) || !va),
					t0, t, Verdict(!(t > t0 * 1.25f), float.IsNaN(t) || !va),
					o0, o, Verdict(!(o > o0), float.IsNaN(o) || !va));
			}

			// V6 convergence — does the line cast settle, or is it still hunting at the end?
			for (int a = 1; a < AuditArms - 1; a++)
			{
				float f = AuditMedian(a, MAxial, 0, 1);
				float l = AuditMedian(a, MAxial, 2, 3);
				logger.Info("[AUDIT/REPORT] V6 arm {0} |axial| first={1:F4} last={2:F4} -> {3}",
					a, f, l, Verdict(l < f, moving || float.IsNaN(f) || float.IsNaN(l) || !auditArmOk[a]));
			}
			logger.Info("[AUDIT/REPORT] run={0} END - toggles restored to their pre-audit state",
				auditRun);
		}

		// ── STALL BREAKOUT ───────────────────────────────────────────────────────────────────
		// Observed in run 1: the shaft jammed at a bad angle and produced one enormously long,
		// heavily morphed stroke (arm 4 median 4.6 s, arm 3 peak 11 s of arm time) that only
		// ended by hand. The bend logic cannot resolve it — bend recovery pulls OUT, but at a bad
		// angle withdrawing does not reduce the bend enough to re-enter cleanly, so it settles
		// into a slow grind with the geometry unchanged.
		//
		// The absolute velocity floor stops it reading "moving" while stopped, but a stroke that
		// is commanded and still not travelling is a distinct fault and needs a distinct
		// response: abandon the current stroke, withdraw to the shallow end, and let the next
		// stroke re-approach from a pose that is not the one that jammed. Detected on ACHIEVED
		// depth, never on the command, since the command is exactly what is lying in this state.
		private const float StallSeconds = 2.5f;

		private const float StallDepthEpsilon = 0.004f;

		private float stallT, stallRefDepth;
		private bool stallBreaking;
		private float stallBreakT;

		public bool StallBreakoutActive => stallBreaking;

		private void UpdateStallBreakout(float dt)
		{
			if (Sequence == null || dt <= 0f) { stallT = 0f; stallBreaking = false; return; }
			float d = StrokeDepth;

			if (stallBreaking)
			{
				stallBreakT += dt;
				// Give the breakout a bounded window, then hand control back regardless: an
				// unbounded escape hatch is just a second way to get stuck.
				if (stallBreakT > 1.5f || d <= GetMinPenetrationExpectation() * 1.05f)
				{
					stallBreaking = false;
					stallT = 0f;
					stallRefDepth = d;
					logger.Info("[AutoThrust/stall] breakout complete after {0:F1}s, depth={1:F4}",
						stallBreakT, d);
				}
				return;
			}

			if (Mathf.Abs(d - stallRefDepth) > StallDepthEpsilon)
			{
				stallRefDepth = d;
				stallT = 0f;
				return;
			}
			stallT += dt;
			if (stallT < StallSeconds) return;

			stallBreaking = true;
			stallBreakT = 0f;
			logger.Info(
				"[AutoThrust/stall] depth stuck at {0:F4} for {1:F1}s while thrusting "
				+ "(bend={2:F3} bendScale={3:F2}) - withdrawing to re-approach",
				d, stallT, BendDeflection, bendSpeedScale);
		}

		private void Process()
		{
			// A jammed stroke is withdrawn unconditionally: nothing else in the loop can resolve
			// a bad angle, and every other branch here assumes the stroke is progressing.
			if (stallBreaking)
			{
				Thrust(0f - Mathf.Max(AbsoluteMinVelocity, GetVelocity(MotionType.OUT)));
				return;
			}
			float deltaDepth = StrokeDepth - lastTickDepth;
			lastTickDepth = StrokeDepth;
			if (Sequence.Motion == MotionType.NONE)
			{
				Thrust(0.01f);
			}
			Sequence.Ticks++;
			// Envelope monitoring runs during REAL use — that is the whole point of it.
			UpdateEnvelopeMonitor(Time.deltaTime);
			UpdateStallBreakout(Time.deltaTime);
			// The audit measures BEFORE alignment runs this tick, so each sample reflects the
			// state the correctors were responding to rather than the state they just produced.
			AuditTick(Time.deltaTime);
			float penetrationFactor = GetPenetrationFactor();
			float deformationFactor = GetDeformationFactor(penetrationFactor);

			// Keep the world→internals scale current: the tip reserve in FloorFraction depends on
			// it, and it can only be measured while genuinely inside.
			SampleInternalsPerWorld();

			// EXPERIMENT: when armed it owns the alignment DOF exclusively, so the production
			// correctors stand down and cannot contaminate the measurement.
			if (AlignTest)
			{
				UpdateAlignProbe();
				aligning = false;
			}
			else
			{
			// Alignment runs BEFORE the bend logic: it sets `aligning`, which relieves the throttle
			// so the two loops cooperate instead of fighting. Its gain is far below the throttle's
			// so they operate on clearly different timescales and cannot oscillate against
			// each other.
			UpdateAlignment();
			}

			// Adaptive punch: evaluated every tick, before anything reads TotalForwardFraction.
			UpdatePunchAdaptive();

			// BEND RECOVERY takes priority over the normal stroke: while the shaft is bowed, the
			// only correct motion is OUT, until it is straight again. Thrust(OUT) still runs
			// through the adaptive floor clamp, so recovering cannot pop it out.
			if (UpdateBendRecovery())
			{
				Thrust(0f - GetVelocity(MotionType.OUT) * bendSpeedScale);
				return;
			}

			if (Sequence.Motion == MotionType.OUT)
			{
				float activeVelocity = GetVelocity(MotionType.OUT);
				float depenetrationThreshold = GetDepenetrationThreshold();
				float maxVelocity = GetVelocityForPenetrationFactor(penetrationFactor, depenetrationThreshold);
				float inVelocity = GetVelocity(MotionType.IN);
				if (activeVelocity > MaxBalancedVelocity)
				{
					activeVelocity = Mathf.Lerp(MaxBalancedVelocity, activeVelocity, penetrationFactor * penetrationFactor * penetrationFactor);
				}
				else if (inVelocity < activeVelocity)
				{
					activeVelocity = Mathf.Lerp(inVelocity, activeVelocity, penetrationFactor);
				}
				// Never let the geometry shape the withdrawal down to a crawl; bendSpeedScale is
				// applied after, so a real bend can still slow it.
				float rawVelocity = activeVelocity;
				float floorRate = StrokeRateFloor();
				activeVelocity = AbsoluteFloor(Mathf.Max(Mathf.Max(MinVelocity, floorRate), Mathf.Min(activeVelocity, maxVelocity)) * bendSpeedScale);
				// NEAR-ZERO STROKE DIAGNOSTIC.
				//
				// "It crawls at minimum depth" has at least five candidate causes here and they
				// need different fixes: the geometry clamp (maxVelocity) collapsing as
				// penetrationFactor approaches the threshold, bendSpeedScale throttling on a
				// deflection, MinVelocity being a fixed SPEED that is meaningless in a short
				// window, StrokeRateFloor mis-sizing that window, or the absolute floor not being
				// reached at all. From the outside every one of them looks identical.
				//
				// So when the result comes out slow, print the whole chain in ONE line — inputs,
				// each candidate limit, and which one won. Throttled, and only when it is actually
				// slow, so it costs nothing during normal strokes.
				if (activeVelocity < SlowStrokeDiagThreshold)
				{
					string winner = (floorRate >= MinVelocity && floorRate >= Mathf.Min(rawVelocity, maxVelocity))
						? "StrokeRateFloor"
						: (MinVelocity >= Mathf.Min(rawVelocity, maxVelocity) ? "MinVelocity"
							: (maxVelocity < rawVelocity ? "maxVelocity(geometry)" : "raw"));
					logger.InfoRare(20, "[AutoThrust/slow] v={0:F4} raw={1:F4} maxV={2:F4} "
						+ "minV={3:F4} rateFloor={4:F4} bendScale={5:F3} absFloor={6:F3} pf={7:F3} "
						+ "thr={8:F3} depth={9:F4} -> limited by {10}",
						activeVelocity, rawVelocity, maxVelocity, MinVelocity, floorRate,
						bendSpeedScale, AbsoluteMinVelocity, penetrationFactor,
						depenetrationThreshold, StrokeDepth, winner);
				}
				// AT-ENTRANCE TERMINATOR (AIChat's `atEntrance`). Reaching the floor ends the
				// withdrawal REGARDLESS of deformation. Without this the `deformationFactor < 1f`
				// clause holds the stroke in OUT indefinitely — there is always a hair of
				// deformation (measured in game: bend ≈ 0.003 while hovering) — so the out-stroke
				// never terminated: position dithered around the floor with pf BELOW thr, and any
				// tick where the clamp mis-estimated let it drift further out. That was the
				// intermittent pop-out.
				if (AtEntrance())
				{
					Thrust(0.01f);
				}
				else if (penetrationFactor > depenetrationThreshold || deformationFactor < 1f)
				{
					Thrust(0f - activeVelocity);
				}
				else
				{
					Thrust(0.01f);
				}
				return;
			}
			float relPosition = GetRelativeHipsToHoleDistance();
			float penetrationThreshold = GetDepenetrationThreshold() * 1.25f;
			float activeVelocity2 = GetVelocity(MotionType.IN);
			bool atLimit = Sequence.HoleDepthLimit && penetrationFactor > penetrationThreshold;
			float pRatio = GetPenetrationRatio();
			if (Sequence.HoleDiameterLimit && (double)deformationFactor < 0.8)
			{
				if (Sequence.NonDeformedExitPRatio == 0f)
				{
					Sequence.NonDeformedExitPRatio = pRatio;
				}
				if (deformationFactor < 1f)
				{
					activeVelocity2 *= Mathf.Pow(0.5f, deformationFactor);
				}
			}
			if (UserForwardTarget < 1f)
			{
				float maxPF = GetVelocityForPenetrationFactor(penetrationFactor, GetMaxPenetrationExpectation() / MaxWorldPenetration);
				activeVelocity2 = AbsoluteFloor(Mathf.Max(Mathf.Max(MinVelocity, StrokeRateFloor()), Mathf.Min(activeVelocity2, maxPF)));
			}
			_ = MaxSafeVelocity;
			float motionThreshold = 0.00015f;
			float depthLimit = GetRequestedDepth();
			// Limits the game itself reports are absolute (AIChat: "never HOLD against a limit the
			// game is reporting — back off"). The anatomical ceiling and the hole's own depth flag
			// are POSITION checks in her space, so they stop the stroke regardless of speed — a
			// threshold on the stroke target alone cannot, because the command overshoots it before
			// the check reacts. This is what stops the inward stroke punching past 100 % at speed.
			bool pastAnatomy = PastAnatomy();
			bool holeLimit = Sequence.HoleDepthLimit;
			if (StrokeDepth < depthLimit && (firstThrust || deltaDepth > motionThreshold) && deformationFactor > 0.6f && !atLimit && !holeLimit && !pastAnatomy && relPosition > 0f && pRatio < 1f)
			{
				// Proportional bend throttle applies to the INWARD stroke — that is the one that
				// generates the bending force. Combined with the per-event backoff.
				Thrust(activeVelocity2 * bendSpeedScale * BendThrottle());
				return;
			}
			Sequence.ExitDueToMotionLimit = !(lastDepth < depthLimit) || (!firstThrust && !(deltaDepth > motionThreshold));
			Thrust(-0.01f);
			Sequence.ExitDeformation = deformationFactor;
		}

		private float GetRequestedDepth()
		{
			return MaxDepth;
		}

		private float GetRelativeHipsToHoleDistance()
		{
			Vector3 holePosition = Sequence.HoleEntrance.position;
			Vector3 hipsPosition = base.Session.Player.Character.pene.@base.physicBone.position;
			Transform rm = base.Session.Player.Character.animatorRootMotionTransform;
			holePosition = rm.InverseTransformPoint(holePosition);
			hipsPosition = rm.InverseTransformPoint(hipsPosition);
			Vector3 v1 = Vector3.Project(holePosition, Vector3.forward);
			Vector3 v2 = Vector3.Project(hipsPosition, Vector3.forward);
			return v1.z - v2.z;
		}

		private void Thrust(float dv)
		{
			MotionType req = ((dv > 0f) ? MotionType.IN : MotionType.OUT);
			if (req == MotionType.IN)
			{
				firstThrust = Sequence.Motion != MotionType.IN;
			}
			if (Sequence.Motion != req)
			{
				Sequence.Motion = req;
				Sequence.Ticks = 0;
				if (req == MotionType.OUT)
				{
					UpdateVelocitySettings();
				}
				ResetHipTarget();
			}
			float value = dv * GetThrustScaleFactor();
			NoteCommand(0f, 0f, Mathf.Abs(value) * Time.deltaTime);
			if (req == MotionType.OUT)
			{
				// WITHDRAWAL SPEED IS CAPPED, NOT FLOORED. This was
				// `-Mathf.Max(Abs(value), MaxSafeVelocity)` — a MINIMUM outward speed with no upper
				// bound at all, so a fast stroke yanked out faster than any floor check could react,
				// and the first outward tick after a reversal (no adaptive-clamp history yet) could
				// clear the entrance on its own. AIChat took this same "MaxSafeVelocity idea" and
				// used it the other way round: `Min(step * outMult, MAX_SAFE_OUT_VEL * dt)`, so a
				// pattern may thrust in hard but always withdraws under control. Same value, 0.60.
				value = 0f - Mathf.Min(Mathf.Abs(value), MaxSafeOutVelocity);

				// ADAPTIVE HARD CLAMP (AIChat ThrustEngine) — the anti-popout that actually holds
				// it in. AddProfundidadDelta and penetratingWorldLength are DIFFERENT unit spaces,
				// so the outward command cannot be clamped against penLen directly: estimate the
				// command→penLen ratio from last frame (penLen fell |penDelta| for a command of
				// lastOutStep) and clamp this frame's command so penLen cannot cross the floor.
				// As penLen → floor the allowed step → 0, so it brakes to a stop AT the floor
				// instead of overshooting out. Without this the Max() above forces a minimum
				// outward speed every frame and the stroke pops straight out.
				// FLOOR SPACE: prefer the HOLE's own depth when it is known. A shallow hole
					// (mouth, or a shallow anus) admits only a fraction of the pene, so a floor
					// derived from the PENE's range is meaningless there — the whole usable travel
					// can be smaller than the margin. Working in the hole's internals space makes
					// the floor proportional to what this hole actually has.
					float pos, span;
					bool holeSpace = false;
					float capacity = HoleDepthCapacity();
					if (capacity > 0.0001f)
					{
						pos = InternalsDepth();
						span = capacity;
						holeSpace = true;
					}
					else
					{
						PeneLens LP = ReadPeneLens();
						if (!LP.valid)
						{
							controller.AddProfundidadDelta(value * Time.deltaTime);
							return;
						}
						pos = LP.pen;
						span = Mathf.Max(0.0001f, LP.full - LP.tip);
					}

					// TWO DIFFERENT FLOORS, and conflating them is what made the stroke creep.
					//   reversalFloor — where the stroke should TURN AROUND (the user's Backward
					//                   Target). Process() flips to IN when depth reaches it.
					//   guardFloor    — the hard pop-out guard the clamp defends, deliberately a
					//                   little BELOW the reversal floor.
					// Clamping against the reversal floor made both the clamp allowance
					// ((pos-floor)/ratio) and the brake (above/band) tend to zero at exactly the
					// point the reversal needed the stroke to REACH — so it decelerated forever
					// and never got there. Letting the clamp permit a small overshoot past the
					// reversal point guarantees the turnaround actually fires.
					float reversalFloor = holeSpace
						? span * FloorFraction()
						: GetMinPenetrationExpectation();
					float guardFloor = Mathf.Max(0f, reversalFloor - span * GuardSlackFrac);
					float floorPos = guardFloor;
					float posDelta = pos - lastPenLen;
					float cmd = Mathf.Abs(value) * Time.deltaTime;

					// The command→position ratio is a property of the RIG, not of this stroke, so
					// it PERSISTS across reversals. Resetting it each stroke left the first outward
					// tick unclamped — and at high speed, or in a shallow hole, one tick is enough
					// to clear the entrance. That was the remaining pop-out.
					// RATIO ESTIMATION IS ASYMMETRIC ON PURPOSE.
					// Measured in game: consecutive samples ranged 0.14–0.36 for the same motion,
					// because near the floor the command (~1e-4) is the same size as physics
					// jitter, so posDelta is mostly noise. An UNDER-estimate is dangerous — it
					// makes allowed=(pos-guard)/ratio too permissive and the next step overshoots
					// the guard, which is the intermittent pop-out. An over-estimate merely makes
					// the clamp gentler. So: only sample when the command is big enough to be
					// signal, then rise fast and fall slowly, biasing conservative.
					if (lastOutStep > RatioSampleMinStep && posDelta < 0f)
					{
						float ratio = (0f - posDelta) / lastOutStep;
						if (ratio > 1e-5f)
						{
							if (posPerStep <= 0f) posPerStep = ratio;
							else if (ratio > posPerStep) posPerStep = Mathf.Lerp(posPerStep, ratio, 0.5f);
							else posPerStep = Mathf.Lerp(posPerStep, ratio, 0.05f);
						}
					}
					if (posPerStep > 1e-5f)
					{
						float allowed = Mathf.Max(0f, (pos - floorPos) / posPerStep);
						cmd = Mathf.Min(cmd, allowed);
					}

					// ENTRANCE BRAKE: taper across the last stretch above the floor so it
					// DECELERATES into the entrance rather than arriving at speed. Scaled to the
					// span in use, so a shallow hole gets a proportionally short brake.
					float band = Mathf.Max(0.0001f, span * 0.08f);
					float above = pos - reversalFloor;
					float brake = 1f;
					if (above < band)
					{
						// Taper toward the reversal point, but NEVER to zero — a brake that
						// reaches 0 stalls the stroke just short of the turnaround. It keeps at
						// least BrakeMinScale so it always arrives and reverses.
						brake = Mathf.Max(BrakeMinScale, Mathf.Clamp01(above / band));
						cmd *= brake;
					}

					cmd = Mathf.Max(0f, cmd);

					// ── TERMINAL DEADLOCK GUARD ──────────────────────────────────────────────
					//
					// If the outward command has clamped to zero we have withdrawn as far as this
					// stroke is allowed to, and continuing to select OUT is a no-op repeated
					// forever. Measured live: pos=0.0333 against revFloor=0.0444 and guard=0.0335,
					// cmd=0, pelvis frozen to within 1e-5 over four seconds, bend 0.03 % — nothing
					// wrong with the shaft, nothing wrong with the throttle, simply a state with
					// no exit.
					//
					// The existing exit is AtEntrance(), which uses a DIFFERENT basis than the
					// clamp does (FloorFraction vs the reversal floor). When those two disagree —
					// and here they do — the stroke can be past the clamp's floor while
					// AtEntrance() still reads false, and then no branch can move it.
					//
					// So terminate on the clamp's OWN condition rather than a parallel one: if the
					// outward command is zero and we are at or below the reversal floor, the
					// out-stroke is finished by definition. Flip to IN.
					//
					// The stall breakout cannot cover this, because it withdraws — which is the
					// direction already pinned.
					if (cmd <= 1E-06f && pos <= reversalFloor)
					{
						logger.InfoRare(1,
							"[AutoThrust/out] outward command clamped to zero at pos={0:F4} "
							+ "(revFloor={1:F4}) - out-stroke is complete; reversing to IN rather "
							+ "than re-selecting a no-op", pos, reversalFloor);
						Sequence.Motion = MotionType.IN;
						lastOutStep = 0f;
						lastPenLen = pos;
						Thrust(GetVelocity(MotionType.IN) * Time.deltaTime);
						return;
					}

					// Straight-line withdrawal: negative magnitude = out along the hole axis.
					if (ApplyStrokeAlongAxis(0f - cmd))
					{
						lastOutStep = cmd;
						lastPenLen = pos;
						return;
					}
					// Diagnostics: throttled, one line per ~30 outward ticks. Everything needed to
					// tell the three failure modes apart — creeping (cmd→0 with above>0), popping
					// out (pos below reversalFloor), or crawling (v capped low).
					logger.InfoRare(30,
						"[AutoThrust/out] space={0} pos={1:F4} revFloor={2:F4} guard={3:F4} span={4:F4} "
						+ "cmd={5:F5} brake={6:F2} ratio={7:F4} v={8:F3} pf={9:F3} thr={10:F3} bend={11:F3} "
						+ "tipFrac={12:F3} i/w={13:F3} floorFrac={14:F3}",
						holeSpace ? "hole" : "pene", pos, reversalFloor, guardFloor, span,
						cmd, brake, posPerStep, Mathf.Abs(value), GetPenetrationFactor(),
						GetDepenetrationThreshold(), BendDeflection,
						TipFractionOfCapacity(), internalsPerWorld, FloorFraction());
					lastOutStep = cmd;
					lastPenLen = pos;
					controller.AddProfundidadDelta(0f - cmd);
					return;
			}
			else
			{
				// Inward: forget the last OUT command (it is not a valid basis for the next
				// ratio sample) but KEEP posPerStep — the rig's conversion does not change, and
				// keeping it is what leaves the next withdrawal's first tick already clamped.
				lastOutStep = 0f;
				float capIn = HoleDepthCapacity();
				if (capIn > 0.0001f)
				{
					lastPenLen = InternalsDepth();
				}
				else
				{
					PeneLens LIn = ReadPeneLens();
					if (LIn.valid) lastPenLen = LIn.pen;
				}
			}
			// Inward (and any fallback) stroke: same straight-line split when available.
			if (!ApplyStrokeAlongAxis(value * Time.deltaTime))
			{
				NoteCommand(0f, 0f, value * Time.deltaTime);
				controller.AddProfundidadDelta(value * Time.deltaTime);
			}
		}

		private float GetThrustScaleFactor()
		{
			return GetPerLengthThrustScaleFactor() * base.Session.Player.Character.pene.worldLength / 0.2f;
		}

		private float GetPerLengthThrustScaleFactor()
		{
			if (targetVelocityScale.Value)
			{
				return UserForwardTarget - UserBackwardTarget;
			}
			return 1f;
		}

		private float GetVelocity(MotionType motion)
		{
			float v = Sequence.Velocity * GetVelocityMultiplier(motion);
			if (Math.Abs(v) < MinVelocity)
			{
				return MinVelocity;
			}
			return v;
		}

		private float GetVelocityMultiplier(MotionType motion)
		{
			float s = ((motion != MotionType.IN) ? (1f - Mathf.Clamp(ThrustBalance, 0.5f, 1f)) : Mathf.Clamp(ThrustBalance, 0f, 0.5f));
			s = s * s / 0.25f;
			return Mathf.Max(s, 0.001f);
		}

		private void UpdateVelocitySettings()
		{
			Sequence.Step++;
			Sequence.RampUpVelocity = VelocityRampUp;
			float initialInVelocity = GetVelocity(MotionType.IN);
			float perPleasureVelocity = Mathf.Lerp(MinVelocity, MaxVelocity, pleasure.value.value / 100f);
			float pRatio = GetPenetrationRatio();
			pRatio = Sequence.UpdatePRatio(pRatio);
			float perDepthVelocity = Mathf.Lerp(MinVelocity, MaxVelocity, pRatio);
			if (MaxVelocity > MaxSafeVelocity && perDepthVelocity < MaxSafeVelocity)
			{
				perDepthVelocity = MaxSafeVelocity;
			}
			if (Sequence.ExitDueToMotionLimit && Sequence.ExitDeformation > 0.8f && (pRatio > 0.2f || initialInVelocity > MaxBalancedVelocity))
			{
				perDepthVelocity = MaxVelocity;
			}
			if (IgnorePRatio)
			{
				perDepthVelocity = MaxVelocity;
			}
			float targetVelocity = Mathf.Min(perPleasureVelocity, perDepthVelocity);
			if (useConstantVelocity.Value)
			{
				targetVelocity = perDepthVelocity;
			}
			Sequence.UpdateVelocity(targetVelocity);
			ThrustBalance = 0.5f;
			if (Sequence.NonDeformedExitPRatio > 0f)
			{
				float requiredVelocity = Mathf.Lerp(MinVelocity, Mathf.Min(MaxBalancedVelocity, MaxVelocity), Sequence.NonDeformedExitPRatio);
				requiredVelocity = Mathf.Min(requiredVelocity, Sequence.Velocity);
				float x = Mathf.Lerp(initialInVelocity, requiredVelocity, 0.3f);
				requiredVelocity = x;
				float s = requiredVelocity / Sequence.Velocity * 0.25f;
				ThrustBalance = Mathf.Sqrt(s);
				Sequence.NonDeformedExitPRatio = 0f;
			}
			if (ThrustBalance > 0.49f && ThrustBalance < 0.51f)
			{
				ThrustBalance = UserThrustBalance;
			}
			else if (ThrustBalance < 0.5f)
			{
				ThrustBalance = Math.Min(ThrustBalance, UserThrustBalance);
			}
			else if (ThrustBalance > 0.5f)
			{
				ThrustBalance = Math.Max(ThrustBalance, UserThrustBalance);
			}
		}

		/// <summary>
		/// True when the USER stopped the assist, as opposed to it ending because penetration
		/// ended.
		///
		/// StopSequence() runs every frame while not penetrating, so "the sequence is null" cannot
		/// distinguish "the player switched this off" from "we simply came out". AutoSeek was
		/// reading the latter and re-acquiring after a deliberate stop — pressing Space to stop
		/// then pulling out would immediately start a fresh seek. Intent has to be recorded when it
		/// is expressed; it cannot be recovered from state afterwards.
		/// </summary>
		public bool UserStopped { get; private set; }

		private void ReactInput()
		{
			if (base.Session.Player.Character.pene.isPenetrating)
			{
				if (hotkeyHandle.Up && hotkeyHandle.Duration < 2f)
				{
					if (Sequence != null)
					{
						UserStopped = true;
						StopSequence();
					}
					else
					{
						UserStopped = false;
						StartSequence();
					}
				}
			}
			else
			{
				StopSequence();
			}
		}

		/// <summary>
		/// Cancel a previous "user stopped" WITHOUT starting anything. Every other path that clears
		/// the flag also requires penetration, so it survives a depenetration — and a stuck stop
		/// intent disarms the seek loop on sight, every frame, with no way back in.
		/// </summary>
		public void ClearUserStop()
		{
			UserStopped = false;
		}

		public void TryStartSequence()
		{
			// An explicit start clears the stop intent — otherwise one deliberate Space press would
			// suppress the handoff forever.
			UserStopped = false;
			if (base.Session.Player.Character.pene.isPenetrating && Sequence == null)
			{
				StartSequence();
			}
		}

		private void StartSequence()
		{
			if (Sequence == null)
			{
				Sequence = new SequenceState
				{
					Velocity = MinVelocity,
					hole = base.Session.Player.Character.pene.TryGetPenetratingHole()
				};
				if (Sequence.hole != null)
				{
					Sequence.HoleEntrance = base.Session.Guest.Puppet.GetIKBoneTransform(Sequence.hole.entrada);
				}
				UpdateVelocitySettings();
				if (reduceSmoothTime.Value)
				{
					controllerSmoothTime.Value = 0.005f;
				}
				overlay.InfoMessage("Auto-thrust sequence started");
			}
		}

		private void StopSequence()
		{
			if (Sequence != null)
			{
				Sequence = null;
				controllerSmoothTime.Value = defaultControllerSmoothTime;
				// Per-sequence state must not leak into the next one: a bend backoff earned in the
				// last session would otherwise start the next one slow, and the clamp's history
				// refers to a stroke that no longer exists.
				bendRecovering = false;
				bendSpeedScale = 1f;
				punchScale = 1f;
				aligning = false;
				alignProgress = 0f;
				alignEvalTimer = 0f;
				alignLastAbs = 0f;
				lateralProgress = 0f;
				lateralEvalTimer = 0f;
				lateralLastAbs = 0f;
				lateralAccum = 0f;
				alignZBias = 0f;
				coarseTotal = 0f;
				coarseActive = false;
				alignSlowPrimed = false;
				vangleSlow = 0f;
				hangleSlow = 0f;
				alignAccum = 0f;
				alignRunSeconds = 0f;
				alignBestAbs = float.MaxValue;
				alignGaveUp = false;
				lastOutStep = 0f;
				lastPenLen = 0f;
				// posPerStep is deliberately NOT cleared: the command→position conversion belongs
				// to the rig, and keeping it means the very first withdrawal of the next sequence
				// is clamped too. It is re-estimated continuously anyway.
				anatomicalChain = null;
				anatomicalDepth = 0f;
				overlay.InfoMessage("Auto-thrust sequence stopped");
			}
		}
	}

	public enum MotionType
	{
		NONE,
		IN,
		OUT
	}

	private ConfigEntry<bool> enableFeature;

	private ConfigEntry<KeyboardShortcut> hotkey;

	private ConfigEntry<bool> constantVelocity;

	private ConfigEntry<bool> reduceSmoothTime;

	private ConfigEntry<bool> targetVelocityScale;

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
		enableFeature = config.Bind<bool>("Features", "EnableAutoThrust", true, "Enable Auto Thrust: Automatic pelvis motion");
		hotkey = config.Bind<KeyboardShortcut>("AutoThrust", "MainHotkey", new KeyboardShortcut(KeyCode.Space, Array.Empty<KeyCode>()), "Auto Thrust: Start/stop hotkey");
		constantVelocity = config.Bind<bool>("AutoThrust", "ConstantVelocity", false, "Auto Thrust: Constant velocity");
		reduceSmoothTime = config.Bind<bool>("AutoThrust", "ReduceSmoothTime", true, "Auto Thrust: Speed patch");
		targetVelocityScale = config.Bind<bool>("AutoThrust", "TargetVelocityScale", false, "Auto Thrust: Scale velocity with user target");
	}

	public override void OnInit()
	{
		base.OnInit();
		Lookup<PluginOptionsService>().Expose(enableFeature, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(hotkey, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(constantVelocity, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(reduceSmoothTime, base.Scope, PluginOptionsService.SettingsType.player);
		Lookup<PluginOptionsService>().Expose(targetVelocityScale, base.Scope, PluginOptionsService.SettingsType.player);
	}

	public override void OnStart()
	{
		base.OnStart();
		Lookup<SessionTracker>().InterviewServices.Add(() => new AutoThrustService(hotkey, constantVelocity, reduceSmoothTime, targetVelocityScale));
	}
}
