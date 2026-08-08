# Measured capability map — what the player character can actually do

Author: Fable 5 session, 2026-08-07. **Measured, not derived.** Companion to
`ALIGNMENT_THEORY.md` (the geometry) and `AUTOTHRUST_BACKPORT_PLAN.md` (the stroke repair).
Source: `[FREECAL]` run 1, free space, no partner, no penetration — which is what makes it
clean. Every earlier calibration was taken mid-stroke inside a partner and was contaminated
by the stroke's own motion.

## 1. Conditions this map is valid for

    scale = 1.200      pene worldLengthFromUnderSkin = 0.3226      no penetration

**Both are user-adjustable.** Distances scale with the character; angular authority per unit
of travel scales roughly as 1/(L−d). Store gains in the normalised forms below, never raw.

## 2. Ranges (raw == limited at rest)

| axis | min | max | note |
|---|---|---|---|
| x (lateral) | −0.333 | +0.333 | symmetric |
| y (height) | **−1.600** | **0.000** | **asymmetric — 0 is the TOP** |
| z (depth) | **−0.500** | +0.475 | **backward travel EXISTS** |

Two corrections to earlier assumptions in this session:

- **z has backward travel.** An earlier session note suspected `zRange.min ≈ 0`, i.e. no
  backward movement. Wrong: it is −0.5.
- **y's usable direction depends on where you start.** The run began at y = 0, which is the
  range's maximum, so the "up" leg had nothing to sweep. A zero-span leg is not a failure.

## 3. Pitch authority, ranked

| axis | pitch span over full range | deg/unit | ×len (normalised) |
|---|---|---|---|
| **z (depth)** | **68.0°** (−20.0° @ z=−0.50 → +48.0° @ z=+0.48) | 69.0 | 22.3 |
| y (height) | 22.3° (−1.6° @ y=0 → +20.7° @ y=−1.6) | 14.0 | 4.50 |
| x (lateral) | ~0° (3.86° vs 3.90° at the extremes) | ~0 | ~0 |
| yaw | ~0° on pitch (it is a yaw control) | ~0 | ~0 |

**Depth is roughly three times the pitch lever that height is** — and the whole session up to
this point corrected pitch using y, the weaker axis.

x confirms the sanity check: lateral translation does not change pitch, in either direction.

## 4. The axes are INDEPENDENT — the envelope is a box

z span measured at three heights:

| level | at y | z span |
|---|---|---|
| 0 | −1.613 | 0.9828 |
| 1 | −0.789 | 0.9967 |
| 2 | 0.000 | 0.9966 |

Within 1.4 % across the entire height range. The hypothesis that height limits back/forward
travel is **not supported at rest**. A solver may treat the axes as independent — subject to
§6, because this was measured in free space.

## 5. Free-space gain ≠ in-hole gain, and both are correct

| context | deg per unit y |
|---|---|
| free space | 13.95 |
| inside, measured by `[ALIGNTEST]` YSweep | 98 – 186 |

Different mechanisms, not a contradiction. In free space the shaft merely translates and
pitch changes only through the aperture coupling (`CalculeAperture`, |y| → up to 50°). Inside,
the **tip is captured** and base movement PIVOTS the shaft about the entrance, with authority
≈ 1/(L−d). The pivot dominates by an order of magnitude.

Consequence: alignment authority inside is far greater than the character's intrinsic
articulation suggests — but it is depth-dependent, so it cannot be read off this map alone.

## 6. The corrected control law

Since **z is the strongest pitch lever AND the stroke axis**, the natural structure is:

> **The stroke's CENTRE-POINT z sets the pitch; the stroke amplitude oscillates around it.**

i.e. decompose the depth command into `z_centre + stroke(t)`, where `z_centre` is chosen for
alignment and `stroke(t)` is the thrust. y remains a secondary trim (3× weaker), x is for
lateral offset only, and yaw handles lateral *angle*.

This supersedes every earlier approach in this session: angle-chasing with a learned sign
(flapped, then walked the hips out), and the `e⊥` position solve (demanded ~10 cm of base
travel because it ignored the aperture rotation entirely).

## 7. What this map does NOT tell us

- **Anything under load.** Measured in free space; a partner's body, the hole constraint and
  `PelvisMovementLimitSegunHoleFondo` all push back during real use. §4's independence is a
  rest-state result and may not survive contact — hence the live envelope monitor.
- **Yaw's effect on YAW.** The yaw legs were summarised on *pitch* (≈0, correctly). The
  per-tick `worldYaw` column carries the real signal; it has not been reduced yet.
- **Scale sensitivity.** One character at scale 1.2. The normalised columns are a hypothesis
  until a second, very differently sized character is measured.


## 8. Two regimes: acquisition vs steady state (owner, 2026-08-07)

Getting into place and holding position are different control problems and must not share a
gain:

| regime | when | behaviour |
|---|---|---|
| **ACQUIRE** | error > 8 deg | move decisively (0.25/s). A slow approach only prolongs a visibly wrong pose. |
| **HOLD** | error < 4 deg | small, heavily smoothed corrections (0.05/s). At this scale the residual is mostly the STROKE's own motion, and chasing it produces jitter and fights the thrust. |

Hysteresis between 4 and 8 deg so the mode cannot flap on the boundary. The active mode is
logged as `mode=ACQUIRE|HOLD` on every alignment line.

This also guards the failure seen earlier in the session, where a single fast gain kept
correcting during steady state and ended up walking the hips out of position.

## 9. Live envelope monitoring (`[ENVELOPE]`)

§4's independence and §2's ranges are REST-STATE facts. Under load the reachable space is
smaller and dynamic — the partner's body blocks travel, the hole constrains the tip, and
`PelvisMovementLimitSegunHoleFondo` drives the same axes we command. A solver trusting the
rest-state box will keep requesting poses that cannot happen.

So every command this feature issues is registered, and each 4 s window reports, per axis,
**achieved / commanded**:

    [ENVELOPE] effX=.. effY=.. effZ=.. usedX=[..] usedY=[..] usedZ=[..] blockedMask=N

`eff` near 1 = that axis is free; sustained below 0.25 = blocked HERE AND NOW whatever the
nominal range says, and the blocked set is announced the moment it CHANGES rather than only
on the periodic line. `used*` records the envelope actually exercised during play, which is
the real-world counterpart to §2.

Cost is a few float operations per tick and throttled logging (S5). It measures only; it
never steers (R10).


## 10. AS-BUILT control structure (2026-08-07, end of session)

Deployed and byte-verified on G:. Mission Control toggles, all default OFF except where noted.

### 10.1 Two-stage placement (owner's structure)

| stage | actuator | objective | why it is safe |
|---|---|---|---|
| **COARSE** — `Align COARSE (avatar)` | `Session.Player.Move`, horizontal only | bring \|e-perp\| under 4 cm AND keep hips near neutral | BOUNDED, self-limiting target; stops at tolerance; 40 cm total cap; hands the fine stage a reachable problem from a centred hip pose |
| **FINE** — `Align hips` / `Align lateral` | pelvis z (primary), y (trim), x (lateral) | null the residual | bounded ranges, cannot run away |

While COARSE is acting the fine trims stand down, so the two stages never fight over the same
error. Vertical placement is deliberately NOT done by walking the avatar — the pelvis y range
is far finer.

**Why avatar movement is acceptable here when it was catastrophic before:** the old lateral
corrector called `Session.Player.Move` with an OPEN-ENDED objective ("reduce this angle") and
no guards, so it translated the player sideways indefinitely and levered the shaft into a
severe bend. The coarse stage has a measurable stopping condition, a displacement cap, and
runs only while the fine stage demonstrably cannot reach.

### 10.2 Fine stage, by axis

- **z (primary)** — `wantZ = clamp(-vangle / 69, ±0.22)`, 69 deg/unit MEASURED (§3). The sign
  comes from the measurement (pitch rises with z), so there is NO sign learner. Biasing z
  shifts the pelvis while the stroke's reversal points stay in DEPTH units, so the same depths
  are reached from a different pelvis position — the pitch change we want, without fighting
  the thrust.
- **y (trim)** — kept, but 3x weaker and ONE-SIDED from a top-of-range start (§2).
- **x (lateral)** — `AddHorizontalDelta`, NOT avatar movement. Bounded ±0.333, ~0 pitch
  authority, guarded like the vertical (safety gate, give-up latch, ±3.5 cm accumulation cap,
  epsilon rule on the sign learner).

### 10.3 Guards that exist because each was earned by a failure

| guard | the failure it prevents |
|---|---|
| epsilon rule on sign learners | sign flapped -1/+1 and netted zero motion |
| bounded accumulation (±3.5 cm) | committed to a wrong direction and walked the hips out |
| give-up latch (6 s without progress) | pushed forever at an angle hips cannot fix |
| safety depth gate — **now 8 %, was 25 %** | 25 % switched alignment OFF in shallow/unwarmed holes, exactly when it was needed |
| ACQUIRE/HOLD regimes | one fast gain kept correcting in steady state and drifted |
| coarse displacement cap | unbounded avatar translation |

### 10.4 Stroke fixes in the same build

- **Time-based stroke-rate floor**: `MinVelocity` is a fixed SPEED, so a short window still
  crawls. A stroke may now not take longer than `MaxStrokeSeconds = 1.2 s` to traverse its
  window, whatever the window's size. This was the "really slow at min depth while the hole is
  still shallow" symptom.

## 12. The line cast and the straight-line stroke (owner, 2026-08-07)

Two mechanisms replacing the depth-dependent target of §1/ALIGNMENT_THEORY. Both are live in
the deployed build.

### 12.1 The line cast — a STATIONARY target

`B_ideal = E − â(L − d)` moved with `d`, so it oscillated at stroke frequency and the corrector
chased its own thrust. The owner's replacement is a fixed station on the hole's axis:

    outward = HoleEntrance.forward           (unit, out of the hole)
    target  = E + outward · (L · 0.5)        BaseTargetInsertionFrac = 0.5
    e       = target − B                     slow-EMA'd into lineErrSlow (AlignSlowTau)

Half a pene length out is the natural station: from there the shaft spans the entrance with
half its length in reserve either way, so the stroke has symmetric headroom. `d` does not appear,
so **the target does not move with the stroke** — the single change that removes the oscillation.

The error is then split against the axis:

| part | meaning | driven by |
|---|---|---|
| `axial = e · outward` | base is too near / too far along the hole's own line | the stroke's **z centre** — `wantZ = clamp(−axial / mPerUnit, ±AlignZBiasMax)` |
| `e⊥ = e − outward·axial` | base is off the line | COARSE avatar placement, then x/y hip trim |

This is also the owner's third requirement — *"the Z axis would point the pene in a straight
line at the hole from that expected distance"*. It is satisfied without a separate orienting
term because z is simultaneously the strongest pitch lever (69 deg/unit, §3) and the axis
along the hole line: driving z to close `axial` both moves the base to the station AND pitches
the shaft onto the line. One command, both effects — which is why §6 chose z as primary.

### 12.2 Straight-line stroke — `Straight-line stroke` toggle

The pelvis's z is the character's depth axis, not the hole's. Unless they happen to coincide,
a pure-z stroke travels at an angle to the hole and levers the shaft. So the stroke magnitude is
expressed in the hole's frame and split across all three hip axes:

    inLocal = root.InverseTransformDirection(−HoleEntrance.forward).normalized
    AddProfundidadDelta(mag·inLocal.z); AddVerticalDelta(mag·inLocal.y); AddHorizontalDelta(mag·inLocal.x)

Each component is headroom-clamped against its own remaining range, so a saturated axis reduces
the stroke rather than bending the path. Logged as `[AutoThrust/straight]`. Applies to both the
outstroke path and the general `Thrust()` fallback.

**Why a toggle and not always-on:** it re-routes every stroke command through three actuators
instead of one, and a mis-signed `inLocal` would be a runaway. It stays opt-in until §11.1 is
discharged.

## 13. Instruments: ANGLE readout and STROKE AUDIT

### 13.1 `ANGLE readout` — drawn lines, via `Tracer` (the same utility AutoSeek uses)

| colour | what it is |
|---|---|
| CYAN | the hole's axis, one pene-length out of the entrance — where a perfectly straight shaft would lie. The reference. |
| YELLOW | the shaft, base→tip. Overlaying cyan = aligned; the fan between them **is** the error. |
| GREEN | the line cast's L/2 station (small cross) + a connector to the real base. That connector's length is what the corrector drives to zero. |
| MAGENTA | a short entrance-normal stub so the entrance is findable on screen. |

Runs with **or without** penetration, on purpose: the approach angle before entry is what
separates an approach problem from an in-hole one. Both AutoSeek's lines and these can be on
together.

### 13.2 `Stroke AUDIT (A/B)` — a controlled experiment, not a log dump

Cycles the feature set ITSELF — 5 arms x 8 strokes: OFF, align, align+coarse, align+straight,
all — then restores the pre-audit toggle state and reports medians per arm. Self-driving A/B is
the point: comparing arms by memory across separate play sessions cannot detect a 15 % effect,
and every quantity that decides whether this work helps is sub-visual.

Per stroke it records `bendPeak bendMean pathPerp axisErr shaftAng axial perp secs popouts`.
Two of those are the ones the eye cannot supply:

- **`pathPerp`** — RMS perpendicular deviation of the BASE path from the hole's axis line. This
  is literally "is the stroke a straight line into the hole", in metres. A centimetre already
  bows the shaft and looks like nothing.
- **`axisErr`** — angle between the achieved base displacement and the axis we believe we are
  commanding along. A wrong sign in the straight-stroke local-frame mapping reads as *"a bit
  off"* on screen and as **>90°** here.

The audit recomputes the line-cast error independently of the corrector, so it is not reading
back the corrector's own opinion of itself.

**Pre-registered verdicts** (written before any data — the thresholds cannot be fitted
afterwards): V1 frame sanity `axisErr < 25°` (>90° = inverted frame, loud); V2 straight cuts
`pathPerp` ≥20 % vs align-only; V3 alignment cuts `shaftAng` ≥20 % vs baseline; V4 bend ≤ +10 %;
V5 stroke time ≤ +25 %; V6 `|axial|` last third < first third; V7 pop-outs not worse.

**V8 is the validation gate**: if the hole entrance itself moves faster than 0.05 m/s median,
the station is not stationary, the line cast is chasing a moving target, and V2/V3/V6 report
**INCONCLUSIVE** rather than pass/fail. An instrument that cannot see a stationary target cannot
judge a stationary-target theory, and printing a number anyway is the worse failure.

V4/V5/V7 exist because the plausible bad outcome is not "alignment does nothing" — it is
"alignment buys 20 % straightness with 30 % stroke rate", which feels fine in the moment and is
a net loss.

## 14. Stroke/bias decoupling — the next change, and why it blocks the rest

### 14.1 The defect

The pose solve corrects pitch by calling `AddProfundidadDelta`, which is the **same actuator the
stroke uses**, and the stroke measures its progress in **absolute depth**. So when the solve
shifts z for pitch, the stroke sees that shift as stroke progress and effectively stands still
until the solve finishes. Observed in game as positioning and thrust being mutually exclusive.

Independently visible in the audit as `axisErr = -1` — the sentinel for *zero base displacement
across an entire stroke*. Depth changed; the base did not move. Same cause.

### 14.2 The fix

The stroke's reversal points must be **relative to `alignZBias`**, not absolute:

    strokeDepth = ImmediateDepth - biasDepthOffset

where `biasDepthOffset` accumulates each solve-commanded `zStep` converted into depth units
(`internalsPerWorld` in hole space, 1.0 in pene-lens space — the two spaces must never be mixed,
see §DEPTH BASIS in the source). Every reversal comparison — `reversalFloor`, `guardFloor`,
`TotalForwardFraction`, the depenetration threshold — reads `strokeDepth`.

The result is the separation the owner specified: **positioning sets the mean, thrusting sets the
deviation about it, and the two sum rather than compete.** The same depths are reached from a
different pelvis position, which is the whole point of biasing z for pitch in the first place.

### 14.3 What it unblocks

- **Fast, smooth AutoSeek.** A quick approach is only safe once positioning can move without the
  stroke misreading it as progress; otherwise speeding up placement starves the stroke harder.
- **The teardrop path**, which is by definition a deviation about a moving mean.
- **The bend ratchet investigation**, which needs a clean audit run to isolate, which needs the
  stroke not to stall mid-arm.

## 15. Calibration by TABLE, not by equation (owner, 2026-08-07)

The pitch/hip-z relationship is strongly non-linear — FREECAL measured 37 deg/unit in the lower
half and 100 deg/unit in the upper, and the outermost part of the forward stroke steepens more
than any lever model predicts (an `asin` fit needs k=0.65 low and k=1.54 high; a factor of 2.4,
so a single lever is not the mechanism).

So do not fit a curve. **Sample a table** during the FREECAL sweep — hundreds of `(hipZ,
peneAngle)` pairs come free at a high polling rate — decimate to 32–64 points per leg, and invert
by binary search plus linear interpolation. Cheaper at runtime than the `asin` it replaces,
exact by construction, and it handles whatever is happening at the extremes without a model.

**One table per direction** (mid→forward, forward→mid, mid→reverse, reverse→mid) so directional
hysteresis is measured rather than averaged away. If the tables agree, that is itself the finding.

**Assert monotonicity** per leg. Inversion is only unique if pitch rises monotonically with z.
All data so far says it does — at wildly varying rates — but a non-monotonic leg would mean
something other than z moved the shaft during the sweep, and that must be reported loudly rather
than silently interpolated over.

## 11. Open items for the next session

1. **Verify in game** — nothing in §10 or §12 has been tested; the last confirmed-good state is
   the stroke work, not the alignment restructure.
1b. **`BaseTargetInsertionFrac = 0.5` is a choice, not a measurement.** It is geometrically
   natural but has never been compared against 0.4 or 0.6 under load.
1c. **`Align COARSE` and `Straight-line stroke` had no UI control at all** until this build —
   both were declared and handled but never added to the layout, so neither had ever run.
   Any earlier "coarse didn't help" impression is void.
2. **`degPerUnit x lever ≈ 22` is a one-run hypothesis.** Needs a second, very differently
   sized character to become a law. Until then gains are per-character.
3. **Yaw→yaw authority never reduced** — the FREECAL yaw legs were summarised on pitch (~0,
   correctly); the per-tick `worldYaw` column holds the real signal.
4. **Avatar residue**: the old lateral corrector's translations were never reversible. If the
   character sits off to one side, that is historical, not the current build.
5. **`MaxDepth = 0.2f` is still a hardcode** standing in for the live `zRange` (VersionDelta
   §1) — the last stale constant outranking a real signal.
