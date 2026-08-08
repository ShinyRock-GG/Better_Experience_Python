# Mission Control — applying the AutoThrust lessons (plan)

Author: Fable 5 session, 2026-08-06.
Status: **SCOPE CUT BY OWNER (2026-08-06):** *"We just make the current stuff work with the
updated values. No additions."* → Lessons 1 and 2 are satisfied by the AutoThrust signal
repair (already implemented + built); **Lessons 3–7 are NOT to be built** — no Depth readout,
no Resistance bar, no slider-bound rework, no detents, no logging change. They stay below as
rejected options, not as pending work. Companion to
`AUTOTHRUST_BACKPORT_PLAN.md`; that one repairs the physics signal, this one applies the same
lessons to the window that DISPLAYS and CONTROLS it.

**The feature's purpose, stated first so nothing below violates it:** Mission Control is BE's
all-in-one *motion* window (`EnableMissionControl`, F6) — one place to steer the Player
(AutoThrust) and Guest (VelocityControl) motion systems and watch what they are doing live.
Two columns: **Player** (max velocity, thrust balance, forward/backward target + read-only
active velocity / balance / P-ratio) and **Guest** (speed, depth). It is a control-and-
telemetry surface, not a physics system. Every item below either makes a control DO what its
label claims, or makes a readout TELL THE TRUTH. Nothing here adds a new motion behaviour.

---

## Lesson 1 — a readout wired to a dead signal is worse than no readout

*Where it was learned:* G:'s `/diag frameMs` sat pinned at exactly 40.00 ms while the real
frame time was 51–75 ms. Every conclusion drawn from it was wrong, and the defect was only
found because a second, independent meter disagreed (`PENE_INVESTIGATION.md` §10, T4).

*Applied here:* **"Active P-ratio" currently displays a constant.**
`activePRatioSlider.Value = atService.Sequence.MaxPRatio`, and `MaxPRatio` is an EMA of
`pRatio = GetPenetrationRatio()` — which is `InverseLerp(min, max, GetPenetrationDepth())`
with `GetPenetrationDepth()` stubbed to `0f` in 23.1. So the bar reads 0 forever and looks
like a live instrument. Same root cause for the *"Active balance"* readout's usefulness:
`ThrustBalance` is auto-tuned from `Sequence.NonDeformedExitPRatio` / `ExitDeformation`, both
derived from the same dead ratio.

Fix, in order of preference:
1. Land `AUTOTHRUST_BACKPORT_PLAN.md` step 1 (`GetPenetrationDepth() =>
   pene.penetratingWorldLength`) — the readout becomes true with no UI change at all.
2. **Until then, show `N/A`, not `0`.** The window already has this exact idiom for a missing
   sequence (`"Active velocity: N/A"`, slider forced to 0) — reuse it rather than inventing a
   convention (BE-authoritative: extend its idiom, do not restyle).

## Lesson 2 — a control that silently does nothing is the worst failure mode

*Where it was learned:* BE's own handoff notes it (`retired/HANDOFF.md` §5, "Silent no-ops that
produced false conclusions … log a COUNT for any bulk operation"), and G: paid for it twice —
damping applied to 0 rigidbodies, and a `PeneCollisionMaintainer` reused while disabled.

*Applied here:* **"Backward Target %" is currently a no-op slider.** It sets
`atService.UserBackwardTarget`, which feeds `GetMinPenetrationExpectation()` →
`GetDepenetrationThreshold()` → the OUT-stroke comparison
`penetrationFactor > depenetrationThreshold` — and that comparison can never be true while
`penetrationFactor` is 0. The user drags a labelled control and nothing happens.
Same backport step 1 fixes it. **The plan's ordering matters: do NOT redesign this slider
before the signal is repaired** — its behaviour today tells you nothing about its design.

## Lesson 3 — display depth from the game's own quantities, with a defined zero

*Where it was learned:* G: replaced a hardcoded `DEFAULT_MAX_DEPTH_Z = 0.20` and a drifting
running-max with the pene's own world lengths, after finding the game's native
`ControlladorDeAutoSexV2` grading depth as
`InverseLerp(minProfundidad, maxProfundidad, pene.penetratingWorldLength)`. The payoff was a
**defined zero**: 0 % = the pop-out threshold (`worldTipPartLength`), 100 % = full length.

*Applied here:* the Player column has **no depth readout at all** — the only depth slider is
the Guest one. Adding "Depth: NN %" (`InverseLerp(worldTipPartLength, worldLength,
penetratingWorldLength) * 100`) is squarely in-purpose for a motion telemetry window, needs no
new physics, and gives the Forward/Backward target sliders a scale the user can actually read
against. Show `N/A` when the pene lengths are invalid (Lesson 1's rule).

## Lesson 4 — hardcoded UI bounds go stale; ask the game for the range

*Where it was learned:* `VersionDelta.md` §1 — 23.1 added `PelvisMovementController.xRange/
yRange/zRange` and `IDepthPositionContainer.maxDepth/minDepth`; G:'s note was "query
`controller.zRange` at runtime instead of hardcoding".

*Applied here:* `depthSlider = asLayout.HSlider(0f, 0f, 0.1f)` — the Guest depth slider's
range is a magic `0.1`. `maxVelocitySlider` similarly toggles between hardcoded `2f` and `4f`.
Where the game exposes a real range, drive the slider bounds from it; where it does not (BE's
own `MaxVelocity` config), keep the config value as the bound — which the window already does
correctly for `speedSlider.MaxValue = asService.MaxVelocity`. **Precedent exists inside the
file; make the other sliders match it.**

## Lesson 5 — surface the resistance signal that already exists

*Where it was learned:* G: added a resistance bar driven by the deformation factor, and it was
what finally made "it forces through clothing" diagnosable — the shaft compressing against a
surface is a real, continuous physical signal.

*Applied here:* BE computes `GetDeformationFactor()` **every tick** and shows it nowhere. A
read-only "Resistance" bar in the Player column costs one field and one `Refresh()` line,
adds no computation, and is exactly what a motion-telemetry window is for. It is also the one
readout that stays meaningful even while the depth signal is dead — useful immediately.

## Lesson 6 — analog sliders need detents at the values people actually want

*Where it was learned:* G: "Hitting exactly 100 % on the analog Deep slider was fiddly" →
magnetic detents at 25/50/75/100/125 % with a ±3 % snap radius, plus a one-click preset row;
fine control preserved everywhere else.

*Applied here:* Forward Target and Backward Target are 0–1 analog sliders whose interesting
values are 0 / 25 / 50 / 75 / 100 %. Same detent treatment, same snap radius. This is pure UI
polish in BE's own idiom and touches no motion code.

## Lesson 7 — do not swallow the exception that explains a missing panel

*Applied here:* `InitAutoThrust` / `InitVelocityControl` are each wrapped in
`try { … } catch (Exception) { }`. If either service is absent the column silently never
appears and the user has no way to know why. Log the exception once (BE has its logging
convention) — same "log a COUNT / a reason" rule as Lesson 2. The empty catch is otherwise
correct: the window must still open with one column.

---

## What NOT to do (staying true to the feature)

- **No new motion behaviour in Mission Control.** Patterns, automodes, per-hole memory,
  auto-caress and seeker logic are AIChat features built on top of BE; they do not belong in
  BE's control window. Mission Control steers and reports — that is its whole job.
- **No restyle of the layout system.** Use `DrawableLabel`/`DrawableSlider`/`DrawableToggle`,
  the existing `atLayout`/`asLayout` columns, and `Refresh()`. BE is authoritative for its own
  structure; every past deviation cost an in-game failure.
- **No behaviour change hidden in a readout.** Instrumentation measures; it never steers
  (R10). The new Depth/Resistance items are read-only.
- Keep the window's size/hotkey/config conventions (`EnableMissionControl`, F6, MonkeyMode
  docking corner).

## Ordered work list

| # | Item | Depends on | Risk |
|---|---|---|---|
| 1 | Repair `GetPenetrationDepth()` (`AUTOTHRUST_BACKPORT_PLAN.md` §2 step 1) | — | low, one line |
| 2 | `N/A` instead of `0` for P-ratio / balance while the signal is invalid | — (useful even before 1) | very low, UI only |
| 3 | Log the swallowed init exceptions (Lesson 7) | — | very low |
| 4 | Add read-only **Resistance** (deformation) bar | — | very low, value already computed |
| 5 | Add read-only **Depth %** with the defined zero | 1 | low |
| 6 | Slider bounds from live ranges where they exist | — | low |
| 7 | Detents on Forward/Backward Target | 1 (so the sliders mean something first) | very low |

## Verification

Same discipline as the backport plan: **prove each displayed value moves before trusting it**
(T4). For each new/repaired readout, watch one full stroke cycle and confirm the number
traverses its expected range — Depth % rises to ~100 at the wall and falls to ~0 at the
turnaround; Resistance dips when the shaft compresses. A readout that never moves is a
defect, not a quiet system. BEProbe `:8903` `/errors` must stay clean across the window.

**Ownership:** F: is BE's domain; owner decides whether this is implemented here or in a
BE-scoped session. Nothing in this plan has been written to code.
