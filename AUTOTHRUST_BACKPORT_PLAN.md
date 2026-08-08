# AutoThrust backport plan — restoring BE's dead backstroke bound in SMA 23.1

Author: Fable 5 session, 2026-08-06.
Status: **STEPS 1–2 IMPLEMENTED + BUILT on F: (2026-08-06)** — owner-directed scope:
*"We just make the current stuff work with the updated values. No additions."*
Steps 3 (width easing) and §7 (live zRange bound) are therefore **OUT OF SCOPE / not done**.
NOT yet verified in game.

Source of the knowledge being backported: `G:\...\BepInEx\plugins\AIchat\AUTOTHRUST_ENHANCEMENT_PLAN.md`
(status log, entries "BE-faithful real-signal reversal" and "Pop-out floor from the game's OWN
threshold"). That work was itself a port OUT of BE; this returns what was learned fixing it.

---

## 1. The finding — BE's backstroke is governed by a stub that returns zero

`BetterExperience.Features/AutoThrustFeature.cs` (decompiled tree, 573 lines):

```csharp
private float GetPenetrationDepth()
{
    return 0f; // penetracionLocalActual removed in SMA 23.1
}
```

Everything derived from it is therefore inert in 23.1:

| Member | Definition | Value in 23.1 |
|---|---|---|
| `GetPenetrationFactor()` | `GetPenetrationDepth() / MaxWorldPenetration` | **always 0** |
| `GetPenetrationRatio()` | `InverseLerp(min, max, GetPenetrationDepth())` | **always 0** |
| `Sequence.HoleDepthLimit` | `=> false` (`maximaProfundidadVirtualAlcanzada` obsolete) | **always false** |

Consequences, read off `Process()`:

- **OUT stroke (the backstroke — this is the reported symptom):**
  ```csharp
  if (penetrationFactor > depenetrationThreshold || deformationFactor < 1f) Thrust(-activeVelocity);
  else Thrust(0.01f);
  ```
  With `penetrationFactor == 0` the first clause can never be true, so the backstroke is
  governed **solely by shaft deformation**. The moment the shaft relaxes to undeformed
  (`deformationFactor >= 1`), BE stops withdrawing and applies a small FORWARD nudge
  (`Thrust(0.01f)`). The anatomy-relative "how far out should this pull" bound
  (`GetDepenetrationThreshold`) is computed and then never applied.
  Velocity shaping on the same branch (`Mathf.Lerp(..., penetrationFactor³)` and
  `Mathf.Lerp(inVelocity, activeVelocity, penetrationFactor)`) also collapses to its
  zero-penetration end — the balanced/asymmetric stroke tuning is dead with it.
- **IN stroke:** `atLimit` is always false and `pRatio < 1f` always true, so both depth guards
  are dead; the in-stroke survives on the signals that ARE alive — `ImmediateDepth` (effector Z,
  vs a FIXED `MaxDepth = 0.2f`), the `deltaDepth > 0.00015f` stall test, and
  `deformationFactor > 0.6f`. **These three are exactly what the G: side independently
  validated as correct**, which is why BE's in-stroke still feels right and only the
  backstroke misbehaves.

## 2. The fix — one accessor, then two refinements

**Step 1 (the whole bug, one line).** Replace the stub with the live signal the GAME's own
auto-sex controller uses:

```csharp
private float GetPenetrationDepth()
    => base.Session.Player.Character.pene.penetratingWorldLength;
```

Verified available in 23.1:
- `Penetrador.penetratingWorldLength => m_worldlargoInsideHole` — public, world units
  (`Penetrador.cs:396`); declared on `IPene` (`IPene.cs:50`).
- `Penis : Penetrador` (`Penis.cs:11`), so `Session.Player.Character.pene` reaches it with no
  cast and no Traverse.
- Native precedent: `ControlladorDeAutoSexV2` reads `pene.penetratingWorldLength` at `:544`
  and `:1077` for its own penetration weight — the game grades depth this way itself.

This single change revives, with no other edits, `GetPenetrationFactor`,
`GetPenetrationRatio`, `GetDepenetrationThreshold`, the OUT-stroke bound, the velocity
shaping, and the anatomy-relative expectations — which already use `pene.worldTipPartLength`
and `pene.worldLength`, i.e. **the same two quantities the G: work arrived at independently**.
Units are consistent: min/max expectations are already world lengths, so comparing them
against a world length restores the intended arithmetic.

**Step 2 — backward floor at 15 % (owner ruling, supersedes G:'s 3 % pop-out fraction).**
G: floored the backstroke just above the tip (`POPOUT_SAFETY_FRAC = 0.03`). The owner's call
for BE is a higher floor: **Backward Target 0 % maps to 15 % of full length**, not to the tip.
Implemented in `GetMinPenetrationExpectation`:

```csharp
private const float BackwardFloorFraction = 0.15f;

float minWorldPenetration = Mathf.Max(
    pene.worldTipPartLength * GetDepenetractionScaleFactor(),
    MaxWorldPenetration * BackwardFloorFraction);
```

`Max()` means this only ever RAISES the floor — it can never let the stroke pull out further
than before. Everything downstream (`GetMaxPenetrationExpectation`, `GetDepenetrationThreshold`)
reads through the same accessor, so the whole range re-anchors from one constant.

**Step 3 — OUT OF SCOPE (owner: no additions).** Kept for the record only. `Sequence.HoleDiameterLimit` /
`HoleDepthLimit` are obsolete; the live equivalent is the hole's
`anchuraVirtualUnClampWeigth` (width) / `profundidadPhysicsUnClampWeigth` (depth ratio,
1.0 = natural wall). Only if width-based easing is wanted back.

## 3. What NOT to change (rules earned the hard way)

- **BE is authoritative for its own structure** (memory `betterexperience-is-authoritative`:
  every "improvement" deviation from BE caused a new in-game failure). Keep `Process()`'s
  shape, its velocity model, `LerpVelocity`, the thrust-balance asymmetry. Change the dead
  accessor; do not restyle the algorithm around it.
- Do **not** port G:'s pattern engine, per-hole profiles, automodes or UI. Those are AIChat
  features built on top; this backport is strictly the physics-signal repair.
- Do not touch `smoothTime`/`maxSpeed` defaults. G: learned that reducing `smoothTime` removes
  the game's natural first/deep-penetration resistance — it must stay opt-in.

## 4. Verification plan (before/after, in-game)

1. **Prove the signal is live first** (T4 — validate the instrument before trusting it): log or
   expose `penetratingWorldLength`, `worldTipPartLength`, `worldLength` for one stroke cycle and
   confirm `penetratingWorldLength` moves 0 → ~worldLength and back. If it reads 0, STOP — the
   premise is wrong and the rest of the plan is void.
2. **Symptom test (the reported one):** run autothrust at a shallow/fast setting and watch the
   withdrawal. Predicted BEFORE: withdrawal stops early / nudges forward once the shaft
   relaxes. Predicted AFTER: withdrawal continues to the user's backward target and reverses
   there; never exits (step 2).
3. **Regression:** in-stroke behaviour must be unchanged (it never depended on the stub) —
   confirm depth reach, stall-on-clothing, and deformation reversal all behave as before.
4. **Instrument:** BEProbe on `:8903` (`/features`, `/invoke`, `/errors`) can toggle the feature
   and surface exceptions without a restart. Check `/errors` is clean across the test window.

## 5. Ownership / build path

- BE source of truth is the **decompiled tree on F:**
  (`BepInEx/plugins/BetterExperience/decompiled/BetterExperience/BetterExperience.csproj`) —
  the `.7z` and the G: copies are artifacts, not sources.
- Build with F:'s `build.ps1` / `build.bat`; `deploy.bat` copies DLLs + decompiled source to G:.
  It no longer mirrors the Python packages (that step was removed to stop it clobbering live
  G: work) — do not re-add it.
- **This plan touches a different drive's repo than the session that wrote it.** Owner decides
  whether the edit happens here or in a BE-scoped session.

## 6. Answers that were already in the documentation (no owner input needed)

The three questions an earlier draft asked are answered in the G: docs; recording them here so
the next reader does not re-ask.

1. **What the symptom is / what "the backstroke difference" means.** `AUTOTHRUST_ENHANCEMENT_PLAN.md`
   status log documents the whole arc: the outstroke floor was first bound by *accumulated deltas*
   and a *running-max* of observed depth, and both drift — "shallow/fast patterns mis-calibrated the
   running-max, so the floor was wrong" (Pound/Finish popped out). The cure, in order:
   (a) bind the floor to a REAL self-correcting position (`ImmediateDepth` = the controller's
   effector Z) — "reverses exactly at the Shallow floor at any speed/pattern and never pulls fully
   out"; (b) then re-base depth on the pene's own world lengths so 0 % means the pop-out threshold.
   BE's equivalent bound (`penetrationFactor > depenetrationThreshold`) is the one that is dead in
   23.1 — same class of defect, different mechanism.
2. **Is the width/diameter limit in scope?** The docs already classify it: *"Optional Feature-1
   polish still available: `LerpVelocity` accel ramp, diameter limit via
   `anchuraVirtualUnClampWeigth`."* It is an optional follow-on, not part of the repair. Keep it
   out of the first change.
3. **Does `UserBackwardTarget` need re-tuning once the bound is live?** Its meaning is defined by
   the same doc's shallow-end semantics: **0 % must mean "just before pop-out"**, implemented as a
   reverse just ABOVE `worldTipPartLength` with `POPOUT_SAFETY_FRAC = 3 %`. So the target does not
   need re-tuning so much as re-anchoring: `GetMinPenetrationExpectation` already starts from
   `worldTipPartLength`; adding the safety fraction gives `UserBackwardTarget = 0` the documented
   meaning instead of "wherever the dead comparison left it".

## 7. Additional backport item found in the same pass — the hardcoded depth bound

`VersionDelta.md` §1 (23.1 API delta): `PelvisMovementController` gained `xRange/yRange/**zRange**`
(each a `Range`), and `IDepthPositionContainer` exposes `maxDepth`/`minDepth`;
`HandUserController` likewise. The doc's own note: *"We can query `controller.zRange` at runtime to
get the actual valid Z bounds instead of hardcoding `DEFAULT_MAX_DEPTH_Z = 0.20f`."*

BE has the identical hardcode — `public float MaxDepth { get; set; } = 0.2f;` consumed by
`GetRequestedDepth()`. Same fix applies: read the live range instead of assuming 0.2. This is
independent of the backstroke repair (it bounds the IN stroke) and can ship separately.

Also from `VersionDelta.md` §3: 23.1 added a full penetration stress/deformation layer
(`AcumularForcePorStress`, `CalculeStressModPolarizado`, `PelvisMovementLimitSegunHoleFondo.SetMods`
et al.) — *"the game now computes this internally too"*, alongside BE's own
`GetDeformationFactor()`. Worth knowing before tuning deformation thresholds: two systems now
observe the same physical event, and G: already hit this (the pelvis-limiter stress branch,
`PENE_INVESTIGATION.md` §13).

## 8. Ordered work list

1. Repair `GetPenetrationDepth()` → `pene.penetratingWorldLength` (§2 step 1). Verify by §4.1 first.
2. Add the pop-out safety floor so `UserBackwardTarget = 0` means "just before pop-out" (§2 step 2).
3. Separately: replace the hardcoded `MaxDepth = 0.2f` with the live `zRange` bound (§7).
4. Optional, only if wanted: width easing via `anchuraVirtualUnClampWeigth` (§2 step 3).


---

## 9. CORRECTION (owner, 2026-08-06): the IN stroke changes too — 100 % is redefined

An earlier note in this session claimed "the in-stroke never depended on the stub, so it is
unchanged". **Wrong.** Reviving `GetPenetrationDepth()` re-anchors the entire user-facing
scale, and re-arms two in-stroke paths that were dead:

**The scale.** Both ends of the slider range are computed from the repaired accessor:

| Control | Before (dead depth) | After |
|---|---|---|
| Backward Target 0 % | tip-derived value, but the comparison never ran, so it meant nothing | **15 % of `pene.worldLength`** (`BackwardFloorFraction`) |
| Forward Target 100 % | never enforced | `Lerp(min, worldLength * 0.75, 1.0)` = **75 % of `pene.worldLength`** |

So "100 %" now means *75 % of the member's real length*, and the whole 0–100 % span is
anatomy-relative instead of arbitrary. On a longer pene every percentage is a longer stroke;
on a shorter one, shorter. That is the intended behaviour — it is the same "scale to the
actual member" property the G: side adopted — but it means **existing slider positions do not
carry the same meaning as before the change.** Expect to re-dial.

**Two in-stroke paths that were dead and now fire:**
1. `pRatio < 1f` in the advance condition. `GetPenetrationRatio()` was always 0, so this was
   permanently true and the forward bound never engaged. It now reverses the stroke when the
   pene reaches `GetMaxPenetrationExpectation()` — a real depth ceiling where there was none.
2. The velocity cap under `if (UserForwardTarget < 1f)`
   (`GetVelocityForPenetrationFactor(penetrationFactor, maxExpectation / MaxWorldPenetration)`)
   was computed from a constant 0 and is now live — the stroke should EASE as it approaches
   the forward target instead of running at full speed into it.
   Also `Sequence.NonDeformedExitPRatio` can now hold a real value, which feeds
   `GetDeformationFactor`'s early-out and the thrust-balance auto-tuning.

**Interaction to watch (still-hardcoded second bound).** `GetRequestedDepth()` returns
`MaxDepth = 0.2f` in effector-Z units, and the in-stroke is bounded by BOTH that and the new
`pRatio` ceiling — whichever is reached first. The two live in different unit spaces, so which
one dominates depends on the character's pene length: for a long member the 0.2 effector cap
may still cut in before 75 % of length is reached, making the Forward Target appear to top out
early. **This is why §7 (drive the bound from the live `zRange` instead of 0.2) is worth
revisiting** — it was cut from scope as "no additions", but it is the same class of defect as
the one just repaired, and it now has a visible consequence.


---

## 10. AS-BUILT (2026-08-06 23:26, deployed to G:)

Four signals repaired in `AutoThrustFeature.cs`. Nothing added; no new controls, readouts or
behaviours. DLL built by F:'s `build.ps1`, copied to G: with its `decompiled/` source; verified
byte-identical.

| Was (dead / mismatched) | Now |
|---|---|
| `GetPenetrationDepth() => 0f` | `hole.estadoDePuntos.actualLocal.penetratedDepthLocalInternals` (her space), falling back to `pene.penetratingWorldLength` |
| `MaxWorldPenetration => pene.worldLength` | `holeConfig.maxProfundidadVirtual` (reflected, per-hole cached), falling back to `pene.worldLength` |
| `HoleDepthLimit => false` | `hole.maximaProfundidadPhysicsAlcanzada` — the live successor to the obsolete `maximaProfundidadVirtualAlcanzada` |
| backward floor at the tip segment | `BackwardFloorFraction = 0.15f` of the active 100 % reference |

**100 % now means her authored depth for THIS hole** — a per-hole constant, identical at
session start and after she has relaxed, independent of pene size. Vaginal / anal / oral have
different authored depths, so the same slider position reaches differently per hole BY DESIGN.

### Parity with AIChat, stated exactly (owner asked)

- **Primary basis: identical.** AIChat's `ThrustEngine.CurrentDepth` primary is
  `_internalsDepth / _anatomicalDepth`, a plain ratio off the same reflected
  `maxProfundidadVirtual`. Same field, same maths. An earlier draft used
  `maxProfundidadPhysicsLocal` instead — a DIFFERENT field that AIChat reserves for its depth
  governor — and was corrected before the build.
- **Fallback basis: deliberate divergence, owner-approved.** AIChat's fallback is
  `InverseLerp(tip, full, pen)` (0 % = pop-out threshold); BE keeps the plain ratio
  `pen / full`. Reason: BE compares the factor against
  `depenetrationThreshold = min / max`, so numerator and denominator MUST share a basis —
  a tip offset in one side only would corrupt the comparison. The tip is handled in
  `GetMinPenetrationExpectation` instead.
- **Consequence, verified by arithmetic:** in the comparison that sets the outstroke floor the
  normalisation cancels (`depth/max > min/max ⟺ depth > min`), so **the floor is identical
  either way** — it is set by the 15 % floor, not by the tip. The divergence only shifts the
  velocity-shaping lerps (`pf³`, `Lerp(inVelocity, activeVelocity, pf)`) and the deformation
  tolerance ramp (`InverseLerp(0.7, 1, pf)`), and ONLY in the fallback path where no hole is
  registered. In the anatomical basis — normal use — there is no divergence at all.

### To verify in game (not yet done)

1. Mission Control (F6) → **Active P-ratio** must MOVE during a stroke. It sat at 0 before;
   if it still does, the depth basis is not resolving and everything here is inert (T4 — check
   this before judging anything else).
2. **Backward Target** must now change the withdrawal depth; at 0 % the stroke should bottom
   out around 15 % of her depth rather than retreating toward the tip.
3. Withdrawal should run to the target and reverse, instead of stalling and nudging forward
   once the shaft relaxes.
4. Depth reach should now differ between vaginal / anal / oral at the same slider position.
5. In-stroke behaviour DOES change (§9): the `pRatio < 1` ceiling and the approach-easing
   velocity cap were both dead and now fire.
