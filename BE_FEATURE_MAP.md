# BE Feature Map — verdicts (seeded by live testing 2026-08-05; BE-2 fills the rest)

Method: static verdicts per BE-2 (dormancy query); live verdicts via manual probe protocol
(owner triggers, Claude watches :8901/logs). See BE_DEPENDENCY_TRUTH.md for evidence base.

| Feature | Verdict | Evidence | Notes |
|---|---|---|---|
| Alternative Genetics, Autorating, Alternative Ratings, Single Group Mode, Auto Training | OBSOLETE-MISTARGETED | BE_DEPENDENCY_TRUTH §8 (dormancy: legacy Base.Genetica 7 game-refs vs live TValle Alteradores 109) | CUT. Auto Training additionally save-corrupting. Not live-tested (deliberately). |
| AutoThrust | FIXABLE-KNOWN-FIX | Owner 2026-08-05: solved by findings AIChat implemented in its ThrustEngine port | BACKPORT the fix into BE — release target is the public (no AIChat assumed). |
| AutoSeeker | WORKS-DEGRADED | Live test 2026-08-05 (owner): functions, but mis-locates penis tip — on NATIVE MALE bodies too, not futa-specific | Cause is BE-internal. Leading hypothesis: worldOutHole direction-vs-position misuse (the exact bug AIChat.s AutoSeekEngine port had to fix — check BE source for the same pattern). FIXABLE-KNOWN-FIX candidate: backport the worldOutHole fix from AIChat.s AutoSeekEngine port. Public release cannot assume AIChat. |
| PlayerPosture (bending), Story IK services | enabled + no errors observed in live session | passive observation only | not yet actively tested |
| EmoSpy | WORKS | Live test 2026-08-05 (batch 1): overlay appears, functioning per owner; zero log errors | — |
| DragControl | WORKS | Owner-confirmed 2026-08-05 (batch 1) | — |
| NotAMic | WORKS | Owner-validated 2026-08-05 (batch 1) | — |
| BetterHand | WORKS | Owner-validated 2026-08-05 (batch 1) | — |
| AmateurModel | WORKS | Owner-validated 2026-08-05 (batch 1) | — |
| NoMeansNo | UNRESOLVED — hard to invoke | The trigger condition (the "want me to leave?" prompt) is hard to produce on demand | Absence-test; needs the prompt's trigger conditions from source to provoke deliberately, or extended passive play. Not blocking. |
| VelocityControl | DRIFTED — old math | Owner 2026-08-05: functions, but clamps values from the pre-23.1 motion math; needs redo against the new (entrance-resistance era) computation | Owner: documentation exists on why — locate it in the fix session (likely the AIChat thrust/resistance findings or the F: trove). Fix belongs with the AutoThrust+MissionControl cluster: same math, one session. |
| MissionControl | WORKS / DRIFTED-TUNING | Owner (prior + batch 1): window works; backthrust bound likely TOO LOW for 23.1's new entrance-resistance mechanic | COUPLED: MissionControl is the control UI FOR AutoThrust (owner 2026-08-05) — the backthrust bound is AutoThrust tuning. Fix as ONE feature set with the AutoThrust backport: port AIChat thrust findings + re-derive bounds against 23.1 entrance resistance. KEYBIND CONFLICT found: MissionControl key collides with ScriptEngine ReloadKey F6 (pressing it hot-loads FutaConversion) — ScriptEngine rebound to F9 (config, 2026-08-05). |

## Release doctrine (owner, 2026-08-05)

BE is being fixed FOR THE PUBLIC, not for this install. Consequences:
1. "Superseded by AIChat" is never a shipping verdict — AIChat's fixes get BACKPORTED into
   BE where applicable (ThrustEngine + AutoSeekEngine findings are the first two).
2. Fixes must not depend on any other mod beyond BE's declared deps (Monkey for
   Better_Scene).
3. Test matrix is TWO environments: (a) instrumented ensemble (this install — discovery +
   debugging, full telemetry), then (b) clean validation pass — BE + Monkey only, vanilla
   0.23.1_f1, LogOutput + visual only — before any release. A feature is not "fixed" until
   it passes (b).

## Live findings log (batch 1, 2026-08-05)

- IronPython strand throws at boot in this runtime: Microsoft.Dynamic requires System.Xaml
  (absent from Unity Mono) → 3x TypeLoadException during HarmonyX assembly scan. PyStory
  likely degraded at Xaml-dependent edges. Additional evidence for Better_Story + pydlr
  removal (BE_DEPENDENCY_TRUTH §4).
- PARKED: ScriptEngine ReloadKey rebound F6→F9 in config, but a fresh boot still hot-loaded
  FutaConversion on F6 (owner, 2026-08-05). Not adjudicated — deprioritized by owner.
  Candidates when someone cares: ScriptEngine version ignores cfg / another F6 listener.

## Batch-2 physics incident (2026-08-05)

With SitIK + HandsOffset + GuestSelfCollision enabled together: player spine hyperextended
~90° backward (silent — zero exceptions), NPC hands twitchy. All three disabled → all clear
(T2 prediction confirmed). ATTRIBUTION PENDING (three flags flipped together — T5):
cause is within the trio; unknown whether one mechanism (owner hypothesis: all IK-related)
or two (split hypothesis: posture-IK bends player, self-collision twitches hands).
Attribution plan: restart A = GuestSelfCollision alone (discriminates both hypotheses);
restart B = SitIK alone. Rows meanwhile:

| SitIK | SUSPECT — in broken trio | batch-2 incident | off pending attribution |
| HandsOffset | SUSPECT — in broken trio | batch-2 incident | off pending attribution |
| GuestSelfCollision | SUSPECT — in broken trio | batch-2 incident | off pending attribution |

Note: these are restart-bound config flags — even BE-Probe automation needs game restarts
to attribute them; the harness cheapens observation, not restarts.

### IK-cluster run result (2026-08-05, SitIK+KneelIK+HandsOffset on, self-collision off)

- No spine contortion reported this run (pending explicit owner confirm) → if confirmed,
  contortion attributes to GuestSelfCollision by elimination; IK cluster exonerated.
- SitIK/KneelIK: SUBTLE — effects are small foot-placement offsets by design
  (PlayerPostureFeature.cs:197-215), visually near-invisible during C-crouch lowering.
  Activation unconfirmed (isSitting/pelvisGrounded conditions may not fire from hover-
  crouch). Confirmation = BEProbe A/B screenshots or state probe, not eyes.
- Disposition (owner): even if working, sit/kneel would be HEAVILY MODIFIED for our
  purposes (futa penetrado positioning — see FutaConversion BACKLOG). TWO TRACKS: public
  BE release ships stock-behavior sit/kneel (fixed if broken); the heavy modification is
  owner-direction work, separate scope, possibly a BE fork/extension feature.

### Batch-2 incident RESOLVED (2026-08-05, owner-confirmed)

Contortion completely absent with full IK cluster on and GuestSelfCollision off.
**VERDICT: GuestSelfCollision = BROKEN-PHYSICS — convicted by elimination** (two runs:
trio-on = contortion + NPC hand twitch; IK-cluster-on/self-collision-off = completely
clean). Both symptoms (player spine hyperextension AND NPC hand twitch) attribute to it —
the split hypothesis was wrong, the all-IK hypothesis was wrong; it was self-collision all
along, including the player-side effect. Mechanism (unverified but classic): overlapping
rig colliders at rest fight per physics substep (~4x/frame in Script mode).
Rows final: SitIK/KneelIK/HandsOffset EXONERATED (rows above stand: subtle, modify-track);
GuestSelfCollision = BROKEN-PHYSICS, keep OFF, fix session = collider-matrix work (likely
needs per-bone collision-layer exclusions for adjacent/overlapping colliders).

### First AUTOMATED verdicts (2026-08-05 evening, via BEProbe — no owner input)

| GuestIO | WORKS | /invoke CommandExport (nested GIOService) → "Wrote file" x2; fresh valid JSON dumps in Better_Exchange (18.7KB appearance + 87.8KB personality) with guest present | Console-command driven (gio export/import/backup/eve/randomize) + optional dumpOnArrive. Fully harness-testable. |
| LexiconProcessor (+Conv) | WORKS | Boot-time export: conversations.json 7.2MB (553 entries), words.json 1.3MB (21), expressions.json, body_parts.json — all valid JSON | Data quirk: words.json contains one EMPTY-STRING key (legal JSON; possible empty word entry upstream — minor). Process() re-invoke is a no-op when nothing changed (by design). |
| NaturalLanguage | ENABLED, untested | active this boot, no errors | needs typed <Enter> input in-game — owner or future input-injection. |

Note for harness protocol: BEProbe filenames/timestamps are UTC; local is UTC-4. Compare
mtimes in one timezone (the maiden session confused itself for a few minutes).
| GeneTool | WORKS (prediction refuted) | Screenshot-verified 2026-08-05: window opens (hotkey F5, NOT F7 as inventoried), fully populated live gene sliders (alterador-layer names, sane 0-1 values), tabs/filter/batch/watch UI intact | KEY INSIGHT: GeneTool fronts the LIVE TValle alterador morph layer — NOT the dormant Base.Genetica cluster its 5 sibling genetics features target. It escapes the family OBSOLETE verdict. Full functional test (slider→model change) pending owner/harness. Cosmetic: EmoSpy overlay text z-fights the GeneTool window. Cross-instrument note: EmoSpy shows native ConsentToHero=33, matching AIChat emodiag exactly. |
| SceneCamera | UNTESTED — owner's habitual free-fly is MONKEY's, not BE's | /screenshot mid-flight: active camera = "MonkeyFlyCamera" (named attribution, 2026-08-05) | BE SceneCamera keys (defaults): WASDQE move, X = switch cam, + edit/save/del binds. Overlap finding: TWO free-camera systems installed (Monkey [R] + BE SceneCamera) — owner uses Monkey's; BE's may be redundant here but is release-relevant (masses may not run Monkey... except Better_Scene hard-requires Monkey, so it's always present — evaluate whether BE SceneCamera is worth keeping vs Monkey's). |
