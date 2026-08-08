# SeekLab — the AutoSeek / AutoThrust testbed

`SeekLab.dll` is a **hot-reloadable copy** of BetterExperience's two control loops, loaded by
ScriptEngine from `BepInEx\scripts`. While it is loaded it owns the pelvis; BE's own copies stand
down.

## Why

AutoSeek and AutoThrust are control loops, and control loops are not reasoned into correctness —
they are tuned by iteration. Inside BetterExperience, one gain change cost a rebuild, a **game
restart**, and walking the character back into position. Minutes per iteration, and the restart
destroys the state under investigation. That cost, not the difficulty of the control problem, is
what has dominated this work.

Here, a change costs a `deploy.sh` and an **F6**. No restart, no repositioning.

## Loop

```bash
bash F:/Games/AAA/SMA_23.1_HomeworkTestbed/BepInEx/plugins/BetterExperience/deploy.sh
```

Then in game press **F6**, or `curl -X POST http://localhost:8910/reload`. Confirm ownership:

```bash
curl "http://localhost:8910/get?path=T:SeekLabHandoff.Status"
```

Expected: `OWNS pelvis; 2 service(s) live on the current guest`. If it says
`registered, waiting for a guest`, the reload happened outside a session — set
`T:SeekLabHandoff.RequestReattach=true` once the guest is present rather than restarting.

Live tuning is unchanged (`T:AutoSeekTuning.*`, `T:BeTestControl.*`).

## The three failure modes this design exists to prevent

| failure | why it is worse than it looks | guard |
|---|---|---|
| **Both copies running** | Two controllers commanding the same pelvis produce a motion belonging to neither control law. The character still moves, so it reads as a bad gain — and it invalidates every measurement taken to judge either version. | `SeekLabHandoff.ExternalOwner`, checked per tick, not at startup |
| **Stale services after reload** | ScriptEngine destroys the GameObject but knows nothing about BE's scope graph. Anything not disposed keeps ticking from a dead assembly. | `Detach()` disposes every scope; live per-guest services are found **by assembly**, never by type name — after a reload two generations share every name |
| **Probe reading a dead generation** | `T:` lookups cached by name would keep resolving to the previous assembly: `set` writes a static nobody reads, `get` reports a value nobody wrote. Reads as "my change had no effect". | Status statics live in **BetterExperience** (loaded once, so unambiguous by construction); `Reflect.FindTypes` invalidates on assembly-count change and ranks newest-loaded first |

## Merge-back procedure

Three files are copied — `AutoSeekerFeature.cs`, `AutoThrustFeature.cs`, `MissionControlFeature.cs`
— byte-identical to BE's except where the move forced a change, and **every forced change is marked
`SEEKLAB:`**. That is the merge-back contract for the SOURCE.

It is not the whole job. Things OUTSIDE those files were rewired to make the lab own the pelvis,
and each one has to be put back or the merged code will not run. The checklist below is that list;
every entry is there because it already failed once.

### REWIRING CHECKLIST — everything outside the copied source

| # | What was changed | Where | Put back to | Symptom if missed |
|---|---|---|---|---|
| 1 | `AutoSeekerEnabled`, `EnableAutoThrust`, `EnableMissionControl` set **false** to stop BE registering its copies | `BepInEx/config/f95.betterexperience.cfg` (backup: `.preseeklab.bak`) | **true** | The merged features never register at all. `PluginService` skips `OnStart` for a disabled feature, so no factory reaches `InterviewServices` — no panel, no seek, and the only clue is a log line saying "waiting for a guest". |
| 2 | `Enabled => true` in all three lab features (`SEEKLAB:` marked) | the three copied files | `=> enableFeature.Value` | The config toggles stop working — the feature can no longer be turned off. This override exists ONLY because the lab shares BE's config keys, and entry 1 switched the lab off along with BE. |
| 3 | ScriptEngine `ReloadKey` moved **F6 → F8** | `BepInEx/config/com.bepis.bepinex.scriptengine.cfg` | F6 (owner's preference) | Nothing breaks; the key is just not where it was left. Mission Control's saved hotkey is **F4**, and it collided with F6 — pressing it reloaded the scripts mid-session. |
| 4 | `SeekLabHandoff.cs` + `[assembly: InternalsVisibleTo("SeekLab")]` | `BetterExperience/SeekLabHandoff.cs` | delete, or keep | Harmless to keep. Deleting requires also removing entry 5. |
| 5 | `if (SeekLabHandoff.ExternalOwner) { … return; }` guards | top of `AutoSeekerFeature.OnUpdate` and `AutoThrustFeature.OnUpdate` | delete, or keep | Harmless to keep (one branch per tick), and keeping 4+5 is what makes the lab re-usable next time. |
| 6 | `SeekLab.dll` / `.pdb` deployed to `BepInEx\scripts` on **both** installs | G: and F: | **delete both** | A stale copy raises `ExternalOwner`, disposes the merged services on sight, and the merged code silently never runs. |
| 7 | SeekLab build step | `deploy.sh` | remove the SeekLab block | Build failures for a project that no longer exists. |
| 8 | `AutoSeekTuning.UseOutHoleAxis` added as a live A/B flag | copied `AutoSeekerFeature.cs` | resolve the experiment, then hardcode the winner | An unresolved flag ships as a permanent fork in the control law. |
| 9 | `AutoSeekTuning.BocaUpTilt` live dial | copied `AutoSeekerFeature.cs` | keep, or bake the confirmed sign in as a constant | Same as 8 — but the SIGN has been derived wrong twice, so keeping the dial has real value. |

**Fixes that belong back in BetterExperience regardless of the lab's fate** (they are pre-existing
bugs, not artefacts of the port — the shipped mod has them today):

- `SetScriptsEnabled` crashed on a model change. `unwantedBehaviors` is captured once at `OnStart`;
  calling in a new model destroys those components, and a destroyed Unity object is not a null
  *reference* — the managed wrapper survives, so the list looks populated while the native objects
  are gone. `.enabled` then throws inside native code, the exception escapes `OnUpdate`, and
  `ScopeSupport` shuts the whole seeker down. Now prunes and re-captures.
- The "in position but off-axis — waiting for alignment" branch did not actually wait: no `return`,
  so it fell through into the press and its abort checks, and any approach starting off-parallel
  aborted before the pitch/yaw terms could close the angle.
- `ExitReason.UnreachableTarget` was used for a failed dock. That value is a geometry verdict that
  disarms the loop; a dock that simply did not land wants `Retry`.

### Then

1. `grep -rn "SEEKLAB:" seeklab/*.cs` — the complete in-source delta list:
   - `namespace SeekLab;` → `namespace BetterExperience.Features;`
   - the added `using BetterExperience;` / `using BetterExperience.Features;` / `using Logger = …`
     (all implicit in the original namespace)
   - `internal class AutoSeekerService` → `private class AutoSeekerService`
   - `Enabled => true` → `Enabled => enableFeature.Value` (checklist 2)
2. Copy the three files over `decompiled/BetterExperience/BetterExperience.Features/`.
3. Work the checklist above, top to bottom.
4. Restart (not reload) and confirm: F4 opens exactly ONE panel, and the log shows no
   `Service disabled` for the three features.

Reference coverage, before and after:

```bash
py -3 BepInEx/plugins/AIchat/AICharTestTool/tools/xref.py --refs <Symbol>
```

## What actually produced consistent collinearity

Measured result: shaft within 2–4° of the hole axis, lateral miss 5–13 mm, reaching the 3 mm
calibration point from a 3 cm standoff, repeatably. None of it came from tuning a gain. Every item
below was two things that were supposed to agree and didn't:

1. **ONE axis, everywhere.** `HoleOutDirection()` (`worldOutHoleDirection`) and `-Hole.forward` are
   ~120° apart on this rig, not the 13° assumed. They were mixed across the collinearity readout,
   the lateral measure, the calibration point, the retreat target and the commit target — so the
   shaft could satisfy every gate while aimed at empty space. `DockAxisOut()` is now the single
   source, and each place it was missed cost a full debugging round.
2. **Align at a FIXED station, not while closing.** Correcting during the approach means the
   geometry worsens as the error shrinks; "aligned" got satisfied somewhere the tip never was.
3. **Gate on POSITION, not only angle.** A parallel shaft 3 cm off the line passes every angular
   test. The tip must physically occupy a point on the axis (3 mm) before anything commits.
4. **Match the dead zone to the tolerance.** `TRANSLATION_PRECISSION` is 5 mm; the gate asked for
   3 mm. A servo cannot hold a tolerance finer than the error at which it stops correcting, so the
   gate was unreachable by construction. `DockMovePrecision` is 0.8 mm inside the cycle.
5. **Delete the anti-windup debt.** `achieved ≈ step` every frame — the pelvis tracks its command
   1:1, so there was no lag to compensate and the debt only cancelled the command, converging to
   `pending == errY` and crawling at micrometres. Three "lockups" were that one equilibrium.
6. **One docking authority.** The original dock gate is looser and kept firing mid-cycle, taking
   the decision away. It now stands down while the cycle runs.
7. **Do not dump velocity on a telemetry change.** `SeekPhase` flaps near the hole; each flap
   called `ResetSmoothing()`, so the approach lost momentum several times a second. That was the
   visible jitter — a bookkeeping side effect, not a control decision.

Two lessons that generalise beyond this feature:

- **A status line that reads the same for success and failure is worse than none.** `"at standoff
  0.087m"` (standoff = 0.030) printed on both arrival and timeout, and hid a retreat that had never
  once arrived. Same defect in `"left the near field"`, which was really a `FinalStage` handover.
- **Never compare readings from separately-throttled log lines.** `|dp|` vs `calErr` appeared to
  differ 15-fold across two `InfoRare` streams; on one line, same frame, they agreed. Same-frame or
  it is not a measurement.

## The boca is a different control problem

The vag and anus are static targets: aim, approach, press, hold. The mouth is not, and every boca
fix came from abandoning an assumption that holds everywhere else.

**Her head responds to the player's hips.** Pitch is solved through the pelvis, so on the boca that
closes a loop *with the target*: each correction moves the mouth, which changes the error, which
triggers another correction — the hips climbing forever with no angle to settle on. Fix: the hips
get ONE say. Once the presentation angle is reached the pitch LATCHES, and translation, yaw and the
tap do the rest, because those move the tip without re-posing the hips.

**Contact is negotiated by tapping, not by pressing.** A static press gives her nothing to respond
to. A fast oscillation against the lips lands it — each cycle re-presents contact, which is the
event the game reacts to, and between touches she can settle. This is the opposite of the other
holes, which advance monotonically and hold.

**The tap must be applied DIRECTLY, not as a moving setpoint.** Routing it through the motion
smoother produced an approach that spent all day *almost* touching: a first-order smoother
attenuates any oscillation whose period approaches its time constant, so the commanded excursion
arrived shrunken and late. Commanded contact is not contact. It is now a velocity fed straight to
`Player.Move` (cos, so it integrates to zero displacement per cycle and cannot walk the character).

**Two frame/sign traps, both of which produce plausible-looking motion rather than an error:**

- `Player.Move` translates relative to the ActorController transform, i.e. it takes a **root-local**
  vector. Handing it the world-space hole axis turned "toward her mouth" into a sideways shuffle.
- The presentation tilt sign was derived wrong from `Quaternion.AngleAxis` conventions **twice**.
  It is now the live dial `T:SeekLab.AutoSeekTuning.BocaUpTilt`, and the align log prints `axisY`
  and `shaftY` next to it. A nose-up shaft means a NEGATIVE outward-axis y.

**The angle gate had to widen for the boca.** The 8° presentation tilt plus the pitch latch leave a
standing angle the controller is deliberately not allowed to null — measured rock-steady at 16.8°
while position sat at 0.0007 m against a 3 mm gate, failing every attempt on the one term it had
been forbidden to fix. Position keeps full strictness; angle widens to 30°.

## Why MissionControl had to come too

It binds to an `AutoThrustService` **instance** (`InitAutoThrust`), so BE's copy can only ever
resolve BE's type. Left in BE it would drive the dormant service: every toggle and slider would look
normal and do nothing — indistinguishable from the feature being broken.
