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
