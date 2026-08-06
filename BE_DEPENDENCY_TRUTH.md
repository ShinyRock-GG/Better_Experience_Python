# BE Dependency Truth — audit ledger

Generated 2026-08-05 by BE-1 session. Read-only audit; no fixes applied.

## 1. F: vs G: build parity

All four BE DLLs are **byte-identical** between F: and G: (MD5 match, identical timestamps):

| DLL | MD5 | Match |
|-----|-----|-------|
| BetterExperience.dll | 54b4e5f2fdaa2a365be13efd276756d4 | ✓ |
| Better_Cloth.dll | fed10353c4d9dc7bd701c2b77c4e0997 | ✓ |
| Better_Scene.dll | 00b9afdbb274de432c44b38a84e5b2df | ✓ |
| Better_Story.dll | 0f2de7a3508c9b4ca698680048ead978 | ✓ |

deploy.bat is keeping them in sync. No build-parity issues.

## 2. Assembly reference trees

### BetterExperience.dll (46 refs)

**Game/gameplay** (29): Assembly-CSharp, Assembly-D_C_Dependientes_ReSc,
Assembly-D_D_Characters_ReSc, Assembly-D_E_Chuchi_ReSc, Assembly-D_F_CharactersBasics_ReSc,
Base.BeachGirl.Mapas, Base.Configs, Base.Controllers, Base.CustomMonoBehaviours, Base.Genetica,
Base.Globales, Base.Joints, Base.Memoria, Base.Npc, Base.Plugins, Base.RootMotion,
Base.RootMotion.BeachGirl, Base.SingletonesAndSystemasGlobales, DialogSys, DialogueSystem,
Newtonsoft.Json, Otti, TValle.BeachGirl, TValle.BeachGirl.Alteradores,
TValle.BeachGirl.Alteradores.MapasDeAlteradores, TValle.BeachGirl.Characters.Male,
TValle.BeachGirl.Genetica.Alteradores, TValle.BeachGirl.MapasDeAlteradores, TValle.BeachGirl.UI

**Other game** (4): TValle.IU, TValle.Inputs, TValle.Pro.Entrevista, TValle.SystemasConstraints

**Framework** (2): 0Harmony, BepInEx

**Unity** (9): UnityEngine.AnimationModule, .AssetBundleModule, .CoreModule, .IMGUIModule,
.InputLegacyModule, .PhysicsModule, .UI, .UIElementsModule, .UIModule + Unity.TextMeshPro

**System** (2): mscorlib, System, System.Core

### Better_Cloth.dll (15 refs)

**Game** (7): Assembly-D_D/E/F_*_ReSc, Base.RootMotion, TValle.BeachGirl,
TValle.BeachGirl.VertExmotions, TValle.MeshCalcules, TValle.MeshCalcules.BeachGirl

**BE internal** (1): BetterExperience

**Framework** (2): 0Harmony, BepInEx

### Better_Scene.dll (38 refs)

**Game** (25): Assembly-CSharp, Assembly-D_C/D/E_*_ReSc (+DialogueSys), Base.BeachGirl.HDRP,
Base.Bones, Base.Bones.Gizmos (+BeachGirl), Base.Configs, Base.CustomMonoBehaviours,
Base.Globales, Base.Memoria, Base.Plugins, Base.RootMotion (+BeachGirl),
Base.SingletonesAndSystemasGlobales, Battlehub.RTEditor, DialogueSystem, Newtonsoft.Json,
Otti, TValle.BeachGirl, TValle.IU, Unity.RenderPipelines.Core/HighDefinition.Runtime

**BE internal** (2): BetterExperience, Better_Cloth

**Peer** (1): **Monkey** — hard dependency on Monkey.dll (exists on G:)

**Framework** (2): 0Harmony, BepInEx

### Better_Story.dll (27 refs)

**Game** (14): Assembly-CSharp, Assembly-D_C/D/E_*_ReSc, Base.Behaviours, Base.Controllers,
Base.Plugins, Base.RootMotion (+BeachGirl), Base.SingletonesAndSystemasGlobales,
TValle.BeachGirl, TValle.BeachGirl.Estimulos, TValle.IU

**BE internal** (2): BetterExperience, Better_Scene

**IronPython** (3): **IronPython, Microsoft.Dynamic, Microsoft.Scripting**

**Framework** (2): 0Harmony, BepInEx

## 3. Homework-only references

**Zero.** The Managed/ assembly inventories on F: and G: are identical — same files, same set.
No assembly that BE references exists only on F:. The "Homework variant" and 0.23.1_f1 ship the
same game assemblies (at least at the assembly-name level).

## 4. IronPython / PyStory verdict

### Verdict: **LIVE**

Evidence:
1. **Better_Story.dll is deployed on G:**, byte-identical to the F: build.
2. **BepInEx loads it**: `LogOutput.log` shows `[Info : BepInEx] Loading [Better Story Mod 1.6.0]`.
3. **PyStoryRuntimeService runs**: log shows module import (60 modules in 367ms), python
   background boot, and py script discovery.
4. **IronPython DLLs exist on G:**: at `BepInEx/plugins/pydlr/` (IronPython.dll,
   IronPython.Modules.dll, Microsoft.Dynamic.dll, Microsoft.Scripting.dll). These are NOT
   in Better_Story's own directory — they're loaded from a separate `pydlr` plugin dir.
5. **IronPython is a NuGet PackageReference** in Better_Story.csproj (`<PackageReference
   Include="IronPython" Version="3.4.1" />`), which pulls Microsoft.Dynamic and
   Microsoft.Scripting transitively.
6. **No other BE DLL references Better_Story** — it's a leaf: BetterExperience→x, Better_Cloth→x,
   Better_Scene→x. Better_Story references BetterExperience + Better_Scene (inward deps only).
7. **Better_Story registers itself as a BepInPlugin** (`f95.betterexperience.pycs`) with hard
   dependencies on `f95.betterexperience` and `f95.betterexperience.cs`. It adds PyStoryFeature
   and ScriptPluginFeature to the core BE plugin.

**Implication for removal**: IronPython removal requires **extraction** (not deletion).
Better_Story is LIVE, loads Python, and integrates with the core BE plugin via AddService.
However, because no other BE DLL references Better_Story, the extraction is bounded:
- Delete Better_Story.dll (or stop deploying it) → BepInEx won't load it, PyStory stops.
- Delete pydlr/ directory → IronPython DLLs gone.
- The core BetterExperience.dll's `AddService` call is INBOUND (Better_Story calls
  BetterExperience, not vice versa), so core BE is unaffected.
- Risk: if PyStoryFeature/ScriptPluginFeature register patches or subscribe to events that
  other subsystems depend on, those subsystems silently lose functionality. Needs a test.

## 5. IL-vs-csproj cross-verification

### BetterExperience.dll

**csproj-only** (3): Base.Tiempo, TCharacters.Memory, TValle.Pro.Entrevista.Tiempo
— compile-time refs the code doesn't actually call at the IL level. Dead imports or type-only
references optimized away by the compiler. **Normal; not findings.**

### Better_Cloth.dll

No discrepancies (after accounting for UnityEngine/Base.Globales/Base.CustomMonoBehaviours
which the csproj lists but the IL trims as unused game refs).

### Better_Scene.dll

No unexplained discrepancies. Assembly-D_ReSc and Base.Joints are in csproj but not IL
(unused compile-time refs, same pattern).

### Better_Story.dll

**IL-only** (2): Microsoft.Dynamic, Microsoft.Scripting — explained: transitive NuGet deps
of the `<PackageReference Include="IronPython">`.

**csproj-only** (5): Assembly-D_F_CharactersBasics_ReSc, Assembly-D_ReSc,
Base.CustomMonoBehaviours, Base.Globales, DialogueSystem — unused compile-time refs
(dead code or type-only usage optimized away).

**All discrepancies explained. No unexplained differences.**

## 6. Version-drift intersection (F: vs G: game assemblies)

### Result: **empty worklist**

The xref extractor's `--hashes` / `--diff-hashes` comparison of per-method IL body hashes
(32,219 methods each) yielded:

- **Category (a) — absent in G:**: 0
- **Category (b) — signature changed**: 0
- **Category (c) — body changed**: 0

F: and G: game assemblies are **binary-identical at the method level**. The "Homework variant"
is not a different build — it's the same 0.23.1_f1 game installation (or at minimum, identical
compiled code). There are no version-drift breaks.

### BE→game boundary surface

For reference, the extractor produced 1,187 BE→game boundary edges:
- 866 call, 211 fieldRead, 61 fieldWrite, 26 subscribe, 23 patch

All 1,187 edges resolve to methods/fields that exist identically on both drives.

## 7. Recommendations for BE-3+ (cut vs fix)

### CUT (cheap, bounded risk)

1. **Better_Story.dll + pydlr/**: Remove from deployment. IronPython is LIVE but isolated —
   no other BE DLL depends on Better_Story. Risk: loss of py-scripted story features.
   Test: run game without Better_Story.dll; verify no MissingMethodException or
   silent behavior loss in core BE features.

2. **Dead csproj refs** (Base.Tiempo, TCharacters.Memory, TValle.Pro.Entrevista.Tiempo in
   BetterExperience; Assembly-D_ReSc/Base.Joints in Better_Scene; 5 in Better_Story):
   Remove `<Reference>` entries — they don't affect the built DLL.

### FIX (nothing — no version-drift breaks)

The version-drift worklist is empty. No game method that BE calls has changed or vanished
between F: and G:. If BE has runtime failures, they are NOT caused by version-drift between
the Homework variant and 0.23.1_f1 — they're behavioral bugs in BE's own code or
runtime-wired dependencies invisible to static analysis.

### INVESTIGATE (if BE exhibits runtime failures)

- PyStoryFeature/ScriptPluginFeature service registrations: do other BE subsystems
  query these services? If so, removing Better_Story may silently degrade them.
- pydlr plugin: does it register Harmony patches or modify game state independently
  of Better_Story? (It's a separate BepInEx plugin dir, not just a dependency.)
- Monkey dependency: Better_Scene.dll hard-references Monkey.dll. If Monkey is ever
  removed, Better_Scene breaks. Document this coupling.

## 8. ADDENDUM 2026-08-05 (Fable, full-install graph): genetics dormancy analysis

Section 6's empty worklist compared the WRONG version pair — F: and G: are both 0.23.1_f1
(hence identical). The real drift is 10.4e (BE's authoring target) → 23.1. Since both current
installs carry 23.1, drift manifests not as missing APIs (all 1,187 BE→game refs resolve)
but as DORMANT SYSTEMS: game code the game itself no longer drives.

Method: full-install xref graph (20 assemblies, 97,782 nodes / 293,796 edges). For every
genetics-flavored game type, count game-internal inbound edges (excluding intra-type and
intra-genetics edges) vs BE inbound edges.

**Finding — the game has TWO genetics implementations (owner-confirmed architecture):**

| Cluster | game-internal refs | BE refs |
|---|---|---|
| `TValle.BeachGirl.Genetica.Alteradores` (current) | 109 | ~0 |
| `Base.Genetica` (legacy) + `TValle.Pro.Entrevista` rating models | 7 | 41 |

BE's genetics family (Alternative Genetics, Autorating, Alternative Ratings, Single Group
Mode, Auto Training) is wired to the LEGACY implementation, which current game code barely
references (interview rating Modelo classes + IConjuntoDeGenes/ISujeto*: ZERO game-internal
callers). The game rewrote genetics in the TValle Alteradores cluster; BE never followed.
Auto Training's save-corruption fits: it writes state through the legacy pipeline the
current game no longer reads coherently.

**Verdict: genetics family = OBSOLETE-MISTARGETED. Recommend CUT, not fix.** A "fix" means
re-implementing all five features against TValle.BeachGirl.Genetica.Alteradores — a rewrite,
not a repair.

Caveats before deletion (static blind spots): reflection/Unity-serialization usage is
invisible to the graph. Confirmation test: disable the five features, run an interview
scoring flow in-game, verify native rating works and no errors on :8901/unitylog.

## 9. CORRECTION 2026-08-05 (owner-caught, post-cut-session): Better_Story is LOAD-BEARING

§4/§7's "CUT Better_Story + pydlr" recommendation was WRONG for this install and executed
in error (restored within minutes): **Better_Story's PyStory runtime is how AlmostSentient
loads** — the live Python behavior package (Packages/rock.almostsentient.be, its own repo,
core ecosystem mod: phone UI, gaze, lighting, navigation) runs ON the IronPython/PyStory
stack. "Loss of py-scripted story features" in §4 IS AlmostSentient.

Standing verdicts corrected:
- Better_Story + pydlr: **KEEP — load-bearing infrastructure for AlmostSentient.**
- IronPython dependency: not accidental-removable; it is AlmostSentient's runtime. The
  System.Xaml TypeLoadExceptions at boot remain a real wart — fixable by shipping/stubbing
  System.Xaml or trimming the scan, NOT by removal.
- The genetics-family cut (this session, commit 729e333) is UNAFFECTED and stands.

Process lesson (the owner had just said "we need to figure out the interdependencies too"):
plugin→PACKAGE dependencies (Better_Story → Packages/rock.almostsentient.be) are invisible
to the assembly-reference graph — the xref extractor sees DLL edges, not "this plugin
executes that directory of Python." The interdependency pass must include runtime loader
relationships, declared per-mod in the registry. Registry updated accordingly.
