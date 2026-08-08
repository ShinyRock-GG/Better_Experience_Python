# Stroke alignment — theory, predictions, and the in-game experiment

Author: Fable 5 session, 2026-08-07. Predictions written BEFORE any data was collected.
Companion: `AUTOTHRUST_BACKPORT_PLAN.md`. Protocol: theory-discipline T2 (a theory is a
numeric prediction about a measurement not yet taken; state the TRUE and the FALSE outcome).

## 0. Why the previous attempts failed

Three iterations of angle-chasing all failed the same way: they steered a **scalar angle** with
a **rate**, having no model of which direction reduced it. Consequences, all observed in game:

- a self-correcting sign that flapped (`-1,+1,-1,…`, net zero motion), then — once gated
  against noise — committed to a wrong direction and walked the hips down until pop-out;
- lateral correction that translated the avatar until depenetration;
- no termination condition, because a rate has no target.

The fix is not a better gain. It is to stop steering an angle and solve for a **position**.

## 1. The theory (H1)

With the tip captured by the hole, straight insertion means the shaft is collinear with the
hole axis. Let

| symbol | meaning | source (all metres) |
|---|---|---|
| **E** | hole entrance position | `Sequence.HoleEntrance.position` |
| **â** | hole axis, unit | `-Sequence.HoleEntrance.forward` |
| **B** | pene base | `pene.@base.physicBone.position` |
| **T** | pene tip | `pene.punta.physicBone.position` |
| **L** | shaft length, unbent | `pene.worldLengthFromUnderSkin` |
| **d** | length currently inside | `pene.penetratingWorldLength` |

If the shaft were straight along â with the tip at depth d, the base would lie at

> **B_ideal = E − â·(L − d)**

so the alignment error is **e = B_ideal − B**, and the part that is NOT the stroke is the
component perpendicular to the axis:

> **e⊥ = e − â(e·â)**

**H1: driving the pelvis so that e⊥ → 0 straightens the insertion.** Corollaries:
the correction is self-terminating (e⊥ = 0 at the solution), needs no sign learning (the
direction is in the vector), and cannot withdraw (perpendicular motion does not change d to
first order).

**Authority.** The shaft pivots about E with the base a distance r = L − d outside, so a
perpendicular base movement δ rotates it by **Δθ ≈ δ / r**. Deeper insertion → smaller r →
*more* angular authority per metre. As d → L, r → 0 and sensitivity diverges, so r needs a floor.

## 2. The unknown that must be measured first (H0)

`AddVerticalDelta` / `AddHorizontalDelta` take a scalar. **Nothing establishes which world
direction each moves the base in, or how many metres per unit.** Assuming it is the reason
every previous attempt needed a sign learner.

**H0: each command axis maps to a fixed world direction with a constant scale**, i.e.
ΔB = k_y·û_y per unit of vertical command, ΔB = k_x·û_x per unit of horizontal command.

If H0 holds, û_x, û_y and k_x, k_y are measurable in a few seconds, and the correction becomes
a 2-D least-squares solve of `cx·û_x + cy·û_y ≈ e⊥` — with no frame assumptions whatsoever.

## 3. The experiment (`AlignProbe`, self-driving)

Armed by a Mission Control toggle, runs automatically once thrusting is under way. Phases:

| phase | duration | what it does |
|---|---|---|
| Baseline | 2.0 s | no commands; logs the state vector |
| CalY+ / CalY− | 0.6 s each | commands vertical only, ± ; measures ΔB |
| CalX+ / CalX− | 0.6 s each | commands horizontal only, ± ; measures ΔB |
| Solve | 6.0 s | drives e⊥ using the measured basis |
| Verify | 3.0 s | no commands; does the alignment hold? |

Aborts immediately on: not penetrating, depth below the safety floor, base displacement beyond
a cap, or range saturation. Every phase transition is logged with its measurements.

## 4. Predictions — TRUE and FALSE outcomes, written in advance

| # | Prediction if the theory is TRUE | If FALSE |
|---|---|---|
| P1 | Calibration yields consistent û_x, û_y with \|k\| > 0: the ± phases produce base movements that are **opposite in direction and similar in magnitude** | H0 is wrong — the command is not a fixed-direction translation (e.g. it is a target, or is being overridden). Everything downstream is void. |
| P2 | `k_y` is close to **1 m per commanded unit** (the game's units are metres) | The command unit is not metres; the measured k is then the required conversion and must be used everywhere |
| P3 | During Solve, **\|e⊥\| decreases monotonically** (within noise) and ends < 30 % of its baseline | The basis mapping or the ideal-base formula is wrong |
| P4 | **bend falls** as \|e⊥\| falls (positive correlation across the Solve phase) | Misalignment is not the dominant cause of bend — the bend throttle should stay primary and alignment is cosmetic |
| P5 | **d changes < 10 %** of its baseline mean during Solve | Perpendicular correction IS disturbing depth; the projection is wrong or the pivot model does not hold |
| P6 | During Verify, \|e⊥\| stays within 1.5× of its end-of-Solve value | The pose drifts back — something else drives the same DOF (the pelvis limiter is the suspect) |

## 5. Logging

One `[ALIGNTEST]` line per tick, key=value, plus a `SUMMARY` line per phase. Fields:
`run phase t B T E a L d ePerpX ePerpY ePerpZ ePerpMag bend cmdX cmdY dBase xRem yRem pf motion`.
Parseable directly out of `LogOutput.log`; the per-phase summaries carry the numbers P1–P6 are
judged on, so the verdict does not depend on eyeballing a stream.

## 6. What this experiment CANNOT tell us

- Whether the pelvis limiter fights the correction — only P6 hints at it.
- Whether lateral (`AddHorizontalDelta`) has the same range/authority as vertical; the
  calibration measures scale, not headroom.
- Anything about her hole moving under her own animation during the run (the target is not
  static; the probe measures E every tick but does not model its motion).
