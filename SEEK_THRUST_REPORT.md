# AutoSeek / AutoThrust — implementation report, pitfalls, and lessons

Session of 2026-08-07. Written for whoever picks this up next, including me.

Everything below is either measured live through the dev probe (`:8910`) or read out of the
IL reference graph. Where something is *assumed*, it says so — several of the worst hours of this
session came from assumptions dressed as facts.

---

## 1. Where the implementation stands

### 1.1 AutoSeek — the placement pipeline

The seeker runs an **explicit phase machine** (`SeekPhase`), evaluated once per solve so every branch
agrees on where it is. Phase boundaries are conditions, not timers.

| phase | condition | behaviour |
|---|---|---|
| **TRANSIT** | tip > 2 cm from the entrance | Full speed (~0.9 m/s × SpeedScale, τ shortened by SpeedScale), **no alignment gating**, target = a point 1.5 cm out **on the hole's axis** |
| **HOLD** | ≤ 2 cm, not aligned | **STOP.** Nothing moves forward. Collinearity is corrected in place |
| **TOUCH** | ≤ 2 cm, aligned | Deliberate creep forward (0.45 authority) until the **correct** hole reports contact |
| **NEGOTIATE** | contact detected | Hold; let `Penetraciones.AceptaPenetracion` run. Follows the entrance if it moves (0.25 forward authority) |

Any violation returns to **HOLD**, never to TRANSIT: position was fine and only the angle failed, so
re-running the whole approach throws away work that was already correct.

The phase enum replaced four interacting booleans — see §2.13, which is *why* it exists.

Recovery: any of *lost line* (angle > `CollinearAbortDeg`), *bowing* (`BendNow() > 0.12`), *tip
slipped off target* (> 35 mm), or *contact lost after having it* (> 0.25 s) triggers an explicit
**retreat** to the on-axis standoff, then re-approach. Capped at 4 attempts, then a full
re-acquire. There is a 1.5 s retreat watchdog — a stuck recovery is just a different deadlock.

Continuous handoff: one hotkey arms a loop of seek → thrust → depenetration → seek, until the
hotkey is pressed again. Gated on the existing `Autothrust` config toggle. Hard geometry failures
(`UnreachableTarget`, `VerticalAngleTooWide`) disarm the loop and say so; transient ones (`Retry`)
do not.

### 1.2 Collinearity

Collinearity is **two** terms, and conflating them cost most of a day:

- **angle** — `Vector3.Angle(shaftDir, -axis)`
- **lateral** — perpendicular distance from the tip to the axis *line* (`LateralOffsetFromAxis`)

A shaft parallel to the axis but 5 cm to the side scores **0°** on the first and fails completely.
Both gate the dock and both throttle the approach. The lateral term is now a **vector**, fed into
translation so it is *driven*, not merely checked.

Tolerance is **progressive**: ~50 mm at a pene length out, ~8 mm at the entrance — a funnel, not a
wall. Boca runs at half of everything (6° / 4 mm) because its entrance is the lips (a small
feature) and it rides on the head, which we measured moving **~40× more** than the pelvis holes
(16.5 mm s.d. vs 0.4 mm over 6 s).

### 1.3 Contact

Contact is **measured, not inferred**: `chain.penetraciones.currentHits.cantidadRealDeHitsContraPartes`,
where `chain` is the specific `BoneStretchedChain` resolved by matching entrance position. It is
hole-specific — touching a thigh does not register. Verified through the graph:
`BoneStretchedChain.IsPenetratedBy` and `PenetradoPor` read the same `currentHits`, fed by
`PenisPart.OnEnter/OnStay/TryingEnter`.

**Not yet solved:** the count does not identify *which* part. The owner's rule is "touching the
lips is correct, anything else is not", and the current check would accept teeth contact. Options:
subscribe to the `PenisPart` callbacks (which carry the colliding part) or use proximity to the
lips transform as a proxy. The first is correct; the second is available now.

### 1.4 Motion quality

- Every axis drives a **velocity with bounded acceleration**, integrated per frame. Position deltas
  with no state between frames is what "a thousand little teleports" looked like — the
  discontinuity was in the *derivative*, not the value.
- **Anti-windup** on vertical and pitch: `effectiveError = error − pending`, where pending is paid
  down by the error actually shrinking. Removes lag-driven oscillation without phases.
- Feedback rates scale with **√SpeedScale**, not linearly. Linear scaling caused oscillation twice.
- **Lever compensation**: angular gains scale as `0.2 / peneLength`. Tip travel ≈ length × angle, so
  the same correction that is gentle on a short shaft flings a long one off.
- **Target velocity feed-forward** (0.12 s lead, EMA τ 0.18 s, clamped 1.2 m/s): chasing a moving
  target by position alone guarantees permanent lag.

### 1.5 AutoThrust

- **Stroke/bias decoupling.** `ImmediateDepth` is literally `leftThighOffset.z`, and the pose solve
  biases that same axis for pitch — so the stroke read the solve's corrections as its own progress
  and stalled. `StrokeDepth = leftThighOffset.z − alignZBias` now feeds every reversal comparison.
- **Bend ratchet fixed.** `BendDeflection` was `1 − current/nominal`, a ratio against a *fixed*
  length — total deformation, not current deflection. The reference now rises instantly and decays
  over 2.5 s, so a sustained bend relaxes to zero instead of clamping the throttle forever.
- **Absolute velocity floor** (0.09) applied *after* every reduction. `BendThrottle` (floor 0.10)
  and `bendSpeedScale` (floor 0.25) multiply to ~0.025 of commanded — a dead stop that still
  reported a velocity.
- **`MaxStrokeSeconds` moved to the floor side.** It had been applied inside a function returning a
  **cap**, where `Min(commanded, cap)` made it inert. The audit logged 17.8 s strokes against a
  1.2 s limit for weeks.
- **Straight-stroke rate cap** sized off perpendicular demand (`|k| × dz`) instead of stroke
  magnitude, which was arithmetically below what the geometry required.

### 1.6 Instrumentation

- **DevProbe** (`:8910`) — a generic reflective endpoint replacing four bespoke servers. `T:` / `O:`
  / `C:` / `F:` / `S:` path roots, `/get`, `/set`, `/call`, `/watch`, `/script`, `/type`, `/find`.
  `/watch` samples **all paths in one tick**, which is the only trustworthy way to compare live
  values.
- **Stroke audit** — 6 arms × 8 strokes with pre-registered verdicts, per-arm **manipulation
  checks**, and an instrument-validity gate (V8) that downgrades results to INCONCLUSIVE when the
  target is moving.
- **FREECAL** — now captures the z→pitch **curve** per height level, drops turnaround bins
  structurally (by sample count, not by value), and builds a 2-D `PitchSurface` with an inverse
  solver.
- **`BeTestControl`** — statics for remote test control, since the probe reaches statics without a
  service lookup.

---

## 2. Pitfalls, and what each one taught

These are ordered by how much time they cost.

### 2.1 A feature that ran while its toggle read "off"

`AlignSolver` defaulted to `true` and was checked *instead of* `AlignHips`. The entire pose solve
ran whenever a sequence was active, regardless of the toggle.

**Cost:** every stroke observation and both audit runs. Arms 0 and 5 were labelled "all features
off" while the solver steered yaw, pitch and avatar position throughout. As the owner put it: *"we
could have hit a working configuration a hundred times and wouldn't have known it."*

**The sharper lesson:** I had *already built* manipulation checks specifically to catch "an OFF arm
that isn't off". They passed every time, because they only verified the **legacy** correctors were
quiet. I never added a probe for the component I had written most recently and understood least
well. **A verification blind spot sits where you are most confident.**

### 2.2 Comparing measurements from different frames

I read tip position and hole position in separate `/get` calls minutes apart, subtracted them, and
reported a 1.22 m separation as geometry. The scene had changed between reads — she had knelt. The
real separation was 4 cm.

**Lesson:** on a live scene, sequential reads are not a measurement. `/watch` samples every path in
the same tick and exists for exactly this. This is the same error class as the audit's V8 gate,
committed with a different tool an hour after building the gate.

### 2.3 Seating offset baked into the sensor

`PeneClosestPointTo` returned the collider point **plus 12 mm** of `SeekOvershoot`. Every derived
number — `dp`, collinearity, the dwell trigger — inherited a tip that was 12 mm further forward
than reality, so the seeker "arrived" while genuinely short. That *was* the standoff, and tightening
the dwell distance did nothing because the dwell was not the problem.

**Lesson:** a sensor reports where things *are*. Intent belongs on the target, where it is visible
as intent instead of contaminating everything downstream.

### 2.4 Guessing the tip definition three times

`partePunta.position + 0.1 × tipLength` (undershoot) → full offset (overshoot) → `punta.physicBone`
(overshoot) → collider furthest-from-base probed along the shaft (correct).

**Lesson:** when two derivations fail in *opposite* directions, stop deriving. The owner's
suggestion — measure the mesh — was right, and the geometric answer was available all along via
`Collider.ClosestPoint`.

### 2.5 A gate that was closed 80 % of the time

The pitch driver required `|pelvisTarget.z| < MaxDepth`, which resolves to `0.1` against a z range
of −0.5…+0.48. It ran only in the middle fifth of its own travel. Collinearity was never driven,
and no amount of gain tuning would have changed that, because the code was not executing.

**Lesson:** a correction that declines to act is indistinguishable from one that is badly tuned.
Every gate now logs *why* it is idle. `pitch drive idle: shaft bowed; no z headroom; (err=14.2deg
room+=0.000 room-=0.412)` would have saved hours.

### 2.6 Collinear conflated with parallel

`Vector3.Angle(shaft, axis)` measures **direction only**. A shaft 5 cm to the side scores 0° and
sails through the gate, then drives forward parallel to the hole and misses.

**Lesson (the owner's):** *"it doesn't go where it's supposed to go"* — the two terms want
different actuators (angle → pitch/yaw, offset → translation), so conflating them left the lateral
error with **no owner at all**.

### 2.7 Gating that made its own precondition unreachable

Requiring collinearity *before* permitting approach means a misaligned seeker throttles its own
forward motion toward zero and never reaches the contact that would let it align. My 0.15 floor
made the stall slower, not absent.

**Lesson:** contact is the *easy* half and barely needs alignment. Touch first, then align about the
contact point — which is how a person would do it. Ordering was the bug; no tolerance value fixes
an ordering bug.

### 2.8 Arriving is not touching

The approach drives `dp` to zero and stops, because reaching the computed target *is* its job. If
the target is short by any amount, it parks somewhere it believes is correct and waits forever.

**Lesson:** the target is a hypothesis; contact is the evidence. A component that reports success by
its own internal measure needs an *external* check that the success is real.

### 2.9 The wrong axis entirely

`entrada.forward` is the entrance **bone's** forward, not the direction the hole opens. Measured
live on the vag they differ by **13°** — enormous against a 6–12° gate.
`chain.worldOutHoleDirection` is the game's own.

**Lesson:** I had "verified" this from a *comment* in AIChat's `AutoSeekEngine` and treated it as
settled. A comment is not a measurement. The owner said "the vagina is not pointed straight down"
and was right.

### 2.10 Diagnosing a limit from a constant instead of a measurement

I claimed the vertical axis had no upward travel because `yRange.max = 0`. The owner asked whether
backing off could not simply uncrouch. One query: `currentLocalTarget.y = −0.98` — **0.98 m of
upward travel available**. The claim was wrong, and it was wrong in the same way twice in
consecutive messages.

**Lesson:** a range constant tells you the bounds, not where you are. Measure the live value.

### 2.11 A build system that laundered failures

My deploy script piped `dotnet build` straight into `grep`, discarding the exit code — so a failed
compile looked identical to a success, and it then "verified" a stale DLL against itself and
reported OK. Twice the owner tested a build that did not contain the fix under test.

Separately: I ran `build.ps1` directly for the whole session despite a memory saying *"deploy via
build.bat -b → build_master.ps1; never run build.ps1 directly"*, and `build_master` builds from
**G:'s** source tree while I was editing **F:'s**.

**Lesson:** a stale deploy is worse than a failed build. A failed build announces itself; a stale one
silently invalidates the next round of observations *and* everything reasoned from them.

### 2.12 Self-inflicted build spiral

`timeout` killing a slow `dotnet build` orphans MSBuild worker nodes that hold locks, so the *next*
build blocks, gets killed, and orphans more. Builds got progressively slower until they always timed
out. The actual build takes **1.7 s**.

**Lesson:** when a tool gets progressively worse over a session, suspect the harness before the tool.

---

### 2.13 Behaviour emerging from interacting booleans

`transiting`, `inContact`, `FinalStage`, `Retreating` — each set in one place, read in several. The
combinations were never enumerated, so **states existed that nobody designed**: advancing while
retreating, pinned while transiting, dwelling with nothing touching. The owner's description was
"buggy as fuck", which is exactly what an unenumerated state space feels like from outside.

**Lesson:** replaced with an explicit `SeekPhase` enum evaluated once per solve. The point is not
tidiness — it is that illegal combinations become **unrepresentable** rather than merely unlikely.
Every phase transition now logs itself with the numbers behind it, so the sequence is observable
instead of inferred.

### 2.14 Two conditions for the same event, computed from different bases

The AutoThrust out-stroke deadlocked with `pos=0.0333`, `revFloor=0.0444`, `cmd=0`, pelvis frozen to
within 1e-5 over four seconds and bend at 0.03 %. Nothing was wrong with the shaft, the throttle or
the velocity.

The outward clamp had correctly produced `cmd = 0` — we were past the reversal floor. But the branch
that flips to IN used `AtEntrance()`, which tests against `FloorFraction()`, a **different basis**.
When the two disagree, the stroke can be past the clamp's floor while `AtEntrance()` still reads
false, and then **no branch can move it**. Every tick: select OUT, compute zero, move nothing.

The stall breakout could not rescue it either — its response is to *withdraw*, which is the exact
direction already pinned at zero. A recovery that acts in the only blocked direction is not a
recovery.

**Lesson:** two conditions describing the same event from different bases will eventually disagree,
and the gap between them is where the system hangs. Terminate on the clamp's **own** condition. This
is the same shape as the `MaxStrokeSeconds` floor-applied-to-a-ceiling bug: both were "the check and
the thing it guards do not speak the same units".

### 2.15 Tightening a tolerance without damping the gain

Boca got half the angular and lateral tolerance (6° / 4 mm) because its target is small and moves
~40× more than a pelvis hole. I did not touch the gains. The result was the controller *whipping*
around — demanding more precision while retaining full authority to hunt for it, against a target
moving faster than the loop can settle.

**Lesson:** tolerance and gain are not independent. Tightening the first without reducing the second
converts a stable loop into an oscillator. Boca now runs gains ×0.35 and a 0.55 s shaft-direction
EMA; precision comes from *tracking the mean*, not from chasing every twitch.

### 2.16 Scaling feedback rates linearly with a speed multiplier

`SpeedScale` was applied uniformly to every rate when the speed control was added. It caused
oscillation **twice** — the vertical axis squatting, then pitch hunting the moment its gate opened.

**Lesson:** open-loop *traversal* rates may scale linearly; **feedback** rates must not. A feedback
loop has a stability margin that speed eats directly. Feedback rates now scale with **√SpeedScale**,
and the transit *time constant* scales too — a higher ceiling with an unchanged τ means the cap is
never reached, so "faster" silently does nothing over short distances.

### 2.17 Accuracy is distance-per-correction, not corrections-per-second

At 4× speed a single frame covers four times the ground, so the same per-frame solve is four times
coarser and can shoot straight through a 2 cm gate inside one tick. Raising speed silently lowered
precision.

**Lesson:** the placement solve now sub-steps (up to 4×) with speed, holding the distance moved
between evaluations roughly constant. Bounded, because it re-solves *geometry* and the plant only
updates once per frame — beyond a few sub-steps it is re-reading identical sensor values.

## 3. The pattern behind almost all of it

Nearly every failure this session was **a gate that never opened, or a measurement that lied**:

- a corrector gated closed over 80 % of its range
- a tip sensor inflated by 12 mm
- a bend reference that never decayed, so the throttle never released
- a rate floor applied to a ceiling
- a rate cap below its own geometric demand
- collinearity that only measured half of itself
- an axis taken from the wrong transform
- a feature running with its toggle off
- a travel limit inferred from a constant

Every one *looked* like a tuning problem from the outside. None of them were. The generalisable
defence is to make components state what they are doing and why they are not acting — which is why
`idle:` logging, the audit's manipulation checks, contact-based state, and the deploy verifier all
exist now.

---

## 4. Open items

| item | state |
|---|---|
| Vertical deadlock at y = −0.98 | **Unresolved.** Not a travel limit. Measure `dp.y` and `vertPending` side by side while stuck — distinguishes "not seeing the error" from "seeing it and cancelling it" (pending clamp is ±0.12, error was 0.14 — suspiciously close) |
| `z = −0.506` pinned at `zRange.min` | Depth axis genuinely out of travel; `ZRoom()` should be reporting this and evidently is not |
| Lips-vs-teeth contact identity | Not exposed by `PenetradorHits`. Try the `PenisPart` callbacks |
| Yellow line vs seeker measurement | `ANGLE` draws `punta.physicBone → base.physicBone`; the seeker uses collider tip − `parteBase`. Different endpoints, so "converged" and "looks aligned" are not the same claim |
| Stroke audit | Must be re-run — all prior results contaminated by §2.1 |
| Pitch surface | Built and inverted; never exercised in a seek |
| `MaxDepth = 0.2f` | Still hardcoded where live `zRange` belongs |
| Scale law for the pitch map | Unmeasured. Map marks itself stale on resize rather than rescaling by a guess |
| Phase machine, boca damping, sub-stepping | Built after the last test run — **entirely unverified in game** |
| `AtEntrance()` vs the reversal floor | The deadlock guard (  A72.14) treats the symptom; the two conditions still use different bases and should be reconciled |
| Two recovery mechanisms | `Retreating` drives to the standoff AND the phase machine returns violations to HOLD. They coexist; one should probably win |
| `LeverScale()` vs the FREECAL map | Both touch pene length, derived separately. Confirm they do not double-count |

---

## 5. What I would change in AIChat's implementation

`AIchat/AIChatProj/DialogInterceptorMod.Game/AutoSeekEngine.cs` (492 lines) is a **faithful port of
BE's original placement sequence** — its own comments say so ("BE-exact", "exactly as BE did"). That
fidelity was the right call when it was written: it reproduced known behaviour rather than inventing
new. But it means AIChat currently carries **every defect diagnosed above**, unchanged.

Ordered by impact. Items 1–4 are correctness bugs with measured evidence; 5–8 are quality.

### 5.1 The hole axis is wrong (highest impact, smallest change)

```csharp
private static Vector3 HoleDir => _holeChain.entrada.forward;   // outward normal (BE: Hole.forward)
```

That comment is the exact assumption that cost this session hours — and it is **wrong**. Measured
live on the vaginal hole:

| source | direction |
|---|---|
| `entrada.forward` | `(0, −1.000, +0.009)` |
| `worldOutHoleDirection` | `(0, −0.976, −0.219)` |

**13° apart.** `HoleDir` feeds the yaw solve, the pitch `vangle`, and the `VerticalAngleTooWide`
abort — so every angular decision AIChat makes is against an axis that is not the hole's.

**Change:** `HoleDir => _holeChain.worldOutHoleDirection.normalized`, falling back to
`entrada.forward` if zero. One line, and it is upstream of everything else.

### 5.2 The tip-length fudge

```csharp
dp.z += _pene.worldTipPartLength * 0.1f;
```

One tenth of the tip offset, applied to the depth axis only, so the vertical component is dropped
entirely. The seeker behaves as though the tip were shorter and lower than it is.

**Change:** delete it, and take the tip from the pene's own collision geometry — the surface point
furthest along the shaft from the base (`Collider.ClosestPoint` probed from far out along the shaft
axis). Three derivations from bone lengths failed here before measurement worked. Do **not**
re-introduce a seating offset into the tip *measurement*; put it on the target.

### 5.3 The pitch gate is closed almost always

```csharp
private static float MaxDepth => ThrustEngine.GetMaxDepth() / 100f * 0.2f / 2f;
...
if (Mathf.Abs(vangle) > 1f && (...) && Mathf.Abs(pelvisTarget.z) < MaxDepth)
```

AIChat's `MaxDepth` is even smaller than BE's (which resolved to 0.1 against a z range of
−0.5…+0.48). The pitch correction therefore runs in a narrow band around neutral and is inert
elsewhere. It is also gated on `(vangle < 0f || pelvisTarget.y < 0f)`, so in some poses it corrects
only **one sign** of error and silently tolerates the other.

**Change:** replace the `MaxDepth` test with a live headroom check against `_ctl.zRange` — "is
there room in the direction I need?" — and drop the sign gate. Then **log when it declines to act**.
A correction that silently refuses is indistinguishable from one that is badly tuned, and that
ambiguity is the single most expensive thing in this codebase.

### 5.4 There is no collinearity test at all

AIChat checks `vangle` (a pitch angle in one plane) and nothing else. It never asks whether the
shaft lies **on the hole's line** — so a shaft parallel to the axis but several centimetres to the
side satisfies every test it has, then drives forward and misses.

**Change:** add the perpendicular distance from the tip to the axis line as a first-class term,
gating the final approach alongside the angle. Keep it as a **vector** so translation can cancel it;
as a scalar it can only ever veto. This was the difference between "looks aligned" and "goes where
it is supposed to go".

### 5.5 Serial staging produces the mechanical feel

Each fix `return`s, so exactly one axis moves per frame in a fixed priority order: reset → yaw →
vertical → pitch → lateral → forward. Raising the speed only fast-forwards the same choreography.
`dp.z -= 1f` is a sentinel that forces a stage transition by corrupting the target.

**Change:** command every axis each tick; replace the sentinel with a continuous 0–1 factor that
throttles forward motion by alignment quality; move along the **normalised error direction** rather
than one axis at a time, so the path is a diagonal instead of a rectangle. Rotation should run
*alongside* translation, not block it.

### 5.6 Frame-rate-dependent motion

```csharp
float dv = Mathf.Clamp(-pelvisTarget.z, -0.01f, 0.01f);
```

That is 0.01 **per tick**, not per second — 1.44 m/s at 144 fps, 0.3 m/s at 30. The same placement
behaves differently on different machines, and identically-written code elsewhere in the file is
correctly `deltaTime`-scaled, so the inconsistency is invisible on reading.

**Change:** every step becomes a rate × `Time.deltaTime`. While there: drive a **velocity with
bounded acceleration** rather than position deltas — position deltas with no state between frames
are what makes the motion read as a sequence of small teleports.

### 5.7 No contact detection

AIChat exits only on `_pene.isPenetrating`, so it cannot distinguish *approaching*, *touching but
not entering*, and *slipped off*. Those want opposite responses, and geometry cannot tell them
apart: a tip that has ridden up over the entrance still reads as the right distance away.

**Change:** read `chain.penetraciones.currentHits.cantidadRealDeHitsContraPartes` — hole-specific,
already used by BE's `DragControl`. Then: no contact → keep closing (the target is a hypothesis,
contact is the evidence); contact below target → advance gently; at target → hold and let
`Penetraciones.AceptaPenetracion` run; contact lost after having it → back off and re-aim.

Also worth subscribing to `Penetrador.peneTryingEnterInHole` / `peneEnteredInHole` rather than
polling — the game states outright when it is attempting entry and when it succeeded, and
`GetNextCoolDown` means it paces its own retries. Pressing harder during that window actively
prevents entry by displacing the hole.

### 5.8 `UnreachableTarget` fires too eagerly

```csharp
if (dp.y + pelvisTarget.y > 0.2f) { _exit = ExitReason.UnreachableTarget; return; }
```

The comment reasons that the pelvis yRange is (−1.6, 0) and "can only DROP". True as a bound —
but it says nothing about where the pelvis **currently is**. Measured live this session:
`currentLocalTarget.y = −0.98`, i.e. **0.98 m of upward travel available** while I was asserting
there was none. A crouched character can absolutely stand up.

**Change:** test remaining travel against the live `currentLocalTarget`, not against the range
constant. And separate *transient* failures (bad pose this attempt — retry) from *geometric* ones
(genuinely unreachable — abort). Conflating them means one bad attempt ends the session.

### 5.9 What I would NOT port

- **The 6-arm audit.** Overkill for AIChat, and it was contaminated by its own blind spot anyway.
- **The 2-D pitch surface.** Built here, never yet exercised. Do not port unmeasured machinery.
- **My phase constants** (8 cm transit, 6 cm standoff, 3-contact target). They were tuned against
  one character at one scale on one hole. Port the *structure*; measure the numbers locally.

### 5.10 The one process change worth more than any of the above

AIChat and BE now have **two independent implementations of the same placement problem**, and this
session proved the shared ancestry propagates bugs silently — the `entrada.forward` comment
travelled from BE into AIChat and was believed in both.

Either extract the geometry into something both call, or at minimum record in AIChat's source that
its placement is a **port of a version now known to be wrong**, with a pointer to this report. The
worst outcome is a future session fixing one and citing the other as corroboration.

---

## 6. AIChat's `ThrustEngine` — what carries over

Important framing: **`ThrustEngine.cs` is the better implementation of the two.** This session began
by backporting *from* it into BE, and most of BE's stroke fixes were "make BE behave like AIChat".
Several things it does that BE did not:

- Speed backoff steps down **per bend EVENT, not per tick**, so a single bend cannot compound into a
  crawl the way BE's per-tick throttle did.
- The backoff scales the **target** and lets `LerpVelocity` ease toward it, so a bend cannot snap the
  speed — smoothing is applied in the right place.
- `MOTION_THRESHOLD` detects a pelvis that is not actually advancing and reverses. BE had no
  equivalent and would grind against blocked geometry.
- `ComputeDeformationFactor` exists at all — its own comment notes this was "the resistance signal
  AIChat's port was missing".

So the items below are a short list against otherwise sound code.

### 6.1 The bend ratchet — same defect, and here it latches a state machine

```csharp
float actualLen = (tip.physicBone.position - root.physicBone.position).magnitude;
return Mathf.InverseLerp(0f, 0.9f, actualLen / wl);      // wl = pene.worldLength
```

The denominator is a **fixed nominal length**. That measures total deformation, not current
deflection, and the two diverge the moment the shaft holds any persistent compression — the reading
stays low with the shaft visibly straight.

BE's version of this caused `bendPeak` to climb monotonically across an entire audit run *including
inside the all-features-off baseline arm*: nothing was changing behaviour, the ruler was drifting.

**In AIChat it is arguably worse**, because `deform` drives a latching state machine:

```csharp
if (deform < BEND_ENTER)  _bendRecovering = true;      // enter
else if (deform >= BEND_EXIT) _bendRecovering = false; // exit
```

If the reference is stale and `deform` never climbs back above `BEND_EXIT`, `_bendRecovering` stays
true **permanently** — the stroke withdraws forever and `_bendSpeedScale` never recovers, because
recovery is gated on `!_bendRecovering`. A drifting measurement becomes a stuck mode rather than
merely a wrong number.

**Change:** decay the reference instead of fixing it. Track the longest recently-observed length,
let it rise instantly (a shaft straightening is itself the evidence of what straight means) and
decay toward the current length over ~2.5 s, capped at `worldLength` so stretch never becomes the
new "straight". A genuine bend still reads immediately; a sustained one relaxes to zero and releases
the throttle. Keep the raw value alongside for comparison.

### 6.2 No absolute floor after the multiplied reductions

`targetSpeed *= _bendSpeedScale` with `BEND_SPEED_FLOOR`, then `LerpVelocity`. If any other
multiplicative reduction is ever added ahead of it — as happened in BE, where `BendThrottle` (floor
0.10) and `bendSpeedScale` (floor 0.25) multiplied to ~0.025 of commanded — the product bottoms out
at a dead stop that still *reports* a velocity.

BE's symptom was "it says it's moving and it isn't", and it took a live audit to catch because every
individual floor looked reasonable.

**Change:** one absolute floor applied **after everything**, including the bend backoff. Safety
reductions may slow the stroke; they may not stop it. A stalled stroke at a bad angle is not a safe
state — it is the state that has to be broken out of by hand.

### 6.3 Verify `MaxStrokeSeconds`-style limits are on the floor side

BE had a time-based stroke-rate floor that was written, deployed, and **inert for weeks** because it
was applied inside a function returning a *cap*, where the caller did `Min(commanded, cap)`. Raising
a ceiling cannot speed up a stroke already slower than it. The audit logged 17.8 s, 13.3 s and 9.2 s
strokes against a nominal 1.2 s limit.

I have not audited AIChat for the same shape. **Worth one pass** over every clamp in `ThrustEngine`
asking: *is this bounding the thing I think it is, on the side I think it is?* The failure is silent
by construction — the code reads correctly and simply never binds.

### 6.4 Depth basis, if a pose solve is ever added

BE's stroke read `ImmediateDepth = leftThighOffset.z` while the pose solve biased that same axis for
pitch — so positioning and thrusting became mutually exclusive, each reading the other's commands as
its own progress.

AIChat has no equivalent solver today, so this does not currently apply. But if angular assistance is
ever added to `ThrustEngine`, the stroke's depth reference must be **relative to that bias**
(`StrokeDepth = offset.z − alignBias`), or the same coupling reappears. Mean and deviation must
sum, not compete.

### 6.5 What not to port from BE's thrust



The straight-line stroke, the pitch surface, and the 6-arm audit are all either unmeasured or were
built to solve BE-specific breakage. AIChat's stroke does not have those problems. Porting them
would import complexity without evidence.


---

## 7. Operating guide — how to actually work on this

None of this was written down, and every item cost time to rediscover at least once.

### 7.1 Build and deploy

**Use the canonical path.** BE is built from **G:'s** decompiled tree by `build_master.ps1`, not
from F:'s. Editing F: and copying DLLs across is how stale deploys happened repeatedly.

```bash
# 1. if you edited F:, sync the source to G: first
cp F:/.../decompiled/BetterExperience/BetterExperience.Features/*.cs \
   G:/.../decompiled/BetterExperience/BetterExperience.Features/

# 2. build_master SKIPS BE when its DLLs already exist — delete to force
rm G:/.../BepInEx/plugins/BetterExperience/BetterExperience.dll

# 3. canonical build
cd G:/.../BepInEx/plugins && powershell -File build_master.ps1 -b
```

`deploy.sh` in the BE folder is a faster F:-side loop that verifies byte-identity on both installs
and **refuses loudly** if either is stale. Either is fine; silently trusting a `cp` is not.

**BE is a plugin, not a script** — changes need a **game restart**. F6 only reloads
`BepInEx/scripts` (the DevProbe). The game holds `BetterExperience.dll` open while running, so
deploys fail with "Device or resource busy" until it is closed.

**If builds get progressively slower**, check for orphaned MSBuild workers
(`Get-Process dotnet,MSBuild,VBCSCompiler`) — a killed build leaves lock-holding nodes and each kill
makes the next one worse. The actual build is ~2 s.

### 7.2 The probe (`:8910`)

Path roots: `T:` statics · `O:` GameObject · `C:` component on an object · `F:` first instance ·
`S:` anything reachable.

```bash
curl "http://localhost:8910/health"
curl "http://localhost:8910/type?name=PelvisMovementController"    # discover members
curl "http://localhost:8910/get?path=F:Penis.worldLength"
curl -X POST -d '' "http://localhost:8910/set?path=T:AutoSeekTuning.SpeedScale&value=6"
```

**Always use `/watch` to compare live values** — it samples every path in the same tick. Sequential
`/get` calls land on different frames and produced a completely fictitious 1.22 m measurement (§2.2).

```bash
curl "http://localhost:8910/watch?path=F:Penis.tipPhysics.position.y&path=F:FemaleChar.vagHole.entrada.position.y&sec=3&hz=15"
```

`POST` needs `-d ''` or `HttpListener` returns *"Length Required"*.

### 7.3 Useful live paths

| what | path |
|---|---|
| tip position | `F:Penis.tipPhysics.position` |
| pene length / erection | `F:Penis.worldLength` · `F:Penis.erection` |
| bend inputs | `F:Penis.realCurrentWorldLengthFromUnderSkin` ÷ `F:Penis.worldLengthFromUnderSkin` |
| hole entrance | `F:FemaleChar.vagHole.entrada.position` (also `anusHole`, `bocaHole`) |
| **hole axis** | `F:FemaleChar.vagHole.worldOutHoleDirection` — **not** `entrada.forward` (§2.9) |
| pelvis offset (live) | `F:PelvisMovementController.currentLocalTarget` |
| pelvis limits | `F:PelvisMovementController.yRange` / `zRange` / `xRange` |
| character scale | `F:MaleChar.escala` (the GameObject transform stays 1.0 — the skeleton root carries it) |
| player root | `F:MaleChar.animatorRootMotionTransform` |

**Root-local error**: project the world delta onto the root's forward/right/up. That is what
revealed the target was 39 cm *behind* the player while looking fine in world space (§2.9).

### 7.4 Remote test control

```bash
curl -X POST -d '' ".../set?path=T:BeTestControl.RequestFreeCal&value=true"
curl ".../get?path=T:BeTestControl.FreeCalComplete"
curl ".../get?path=T:BeTestControl.Summary"
```

Statics, because BE's `SessionService`s live in a registry the probe's object-graph search cannot
reach within budget — `S:AutoSeekerService` fails. Live tunables: `T:AutoSeekTuning.SpeedScale`,
`.ApproachTau`, `.RotateDegPerSec`, `.CollinearEnterDeg`, `.CollinearAbortDeg`, `.Verbose`.

### 7.5 Reading the logs

Everything is `[AutoSeek]` / `[AutoThrust/...]` in `BepInEx/LogOutput.log`.

| line | means |
|---|---|
| `TRANSIT 0.42m out` | closing ground, no gating |
| `HOLD at 0.018m - stopped, correcting` | arrived, self-correcting in place |
| `Transit -> Hold` etc. | phase transition, with the numbers behind it |
| `collinearity: angle=… lateral=…` | the two terms; `angle` small + `lateral` large = parallel-but-offset |
| `pitch drive idle: …` | the corrector is **declining to act**, and why |
| `arrived but NOT touching` | creeping because the target was a hypothesis and contact disagreed |
| `contact LOST during dock` | slipped off; backing off to re-aim |
| `outward command clamped to zero` | the §2.14 deadlock guard firing |
| `[AutoThrust/out] cmd=0` **repeating** | a deadlock — compare `pos` against `revFloor` |

### 7.6 Querying the codebase

```bash
py -3 BepInEx/plugins/AIchat/AICharTestTool/tools/xref.py --refs <Symbol>
```

**Never** read or grep `reference-graph.json` (85 MB). `XREF.md` rule 1 is *"query before grep"* —
querying properly is what surfaced `PenetradorHits.hayHits` and
`cantidadRealDeHitsContraPartes` in one pass.

### 7.7 The habit that would have saved the most time

Before believing any number, ask **"did I measure this, or infer it?"** The expensive errors this
session were all inferences that felt like facts:

- an axis taken from a *comment* rather than measured (13° wrong)
- a travel limit read off a range *constant* while the live value said otherwise (twice)
- a separation computed from two readings taken minutes apart
- an "OFF" arm that was never verified to be off

One `/watch` call answers each of these in seconds.
