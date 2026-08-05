# BE Feature Map — verdicts (seeded by live testing 2026-08-05; BE-2 fills the rest)

Method: static verdicts per BE-2 (dormancy query); live verdicts via manual probe protocol
(owner triggers, Claude watches :8901/logs). See BE_DEPENDENCY_TRUTH.md for evidence base.

| Feature | Verdict | Evidence | Notes |
|---|---|---|---|
| Alternative Genetics, Autorating, Alternative Ratings, Single Group Mode, Auto Training | OBSOLETE-MISTARGETED | BE_DEPENDENCY_TRUTH §8 (dormancy: legacy Base.Genetica 7 game-refs vs live TValle Alteradores 109) | CUT. Auto Training additionally save-corrupting. Not live-tested (deliberately). |
| AutoThrust | OBSOLETE-SUPERSEDED | Owner-confirmed live 2026-08-05: AIChat ThrustEngine replaces it | CUT (disable permanently). |
| AutoSeeker | WORKS-DEGRADED | Live test 2026-08-05 (owner): functions, but mis-locates penis tip — on NATIVE MALE bodies too, not futa-specific | Cause is BE-internal. Leading hypothesis: worldOutHole direction-vs-position misuse (the exact bug AIChat.s AutoSeekEngine port had to fix — check BE source for the same pattern). Possibly OBSOLETE-SUPERSEDED (AIChat AutoSeekEngine exists) — owner call. |
| PlayerPosture (bending), Story IK services | enabled + no errors observed in live session | passive observation only | not yet actively tested |
