# BetterExperience for CMA — design

Target: Corrupted Modeling Agency 0.61 (IL2CPP, BepInEx 6). Feasibility and break counts:
`PORT_ASSESSMENT.md` in the CMA root. Tooling: the `il2cpp-port-map` skill.

---

## START HERE (state as of 2026-08-08)

**Nothing is working yet.** Stage 0 does not compile. Everything below is design plus a verified
toolchain, not running code.

| | |
|---|---|
| CMA install | `G:\Games\AAA\Corrupted Modeling Agency 0.61` |
| this project | `F:\...\BepInEx\plugins\BetterExperience\cma\` (untracked in git — commit it) |
| build | `bash build-cma.sh` (`-n` to skip deploy) |
| loader | BepInEx 6.0.0-pre.2 IL2CPP, installed and verified — chainloader starts clean |
| interop | 177 assemblies generated in `<CMA>\BepInEx\interop`; references resolve |

**Stage 0's two known errors**, both in the probe rather than the scaffold:

1. `CS0246: Il2CppAssets could not be found` — I guessed the namespace. Interop prefixes game
   namespaces with `Il2Cpp`, but the real name must be read from the graph, not assumed:
   `py -3 port_map.py --type PelvisMovementController`, or query
   `cma-interop.sqlite` for the `namespace` column.
2. `CS0012: 'Type' is defined in Il2Cppmscorlib` — touching `Il2CppSystem.Type` needs that
   assembly referenced explicitly; the wildcard covers it but the compiler wants it named.

**Things that will bite a fresh session:**

- **Two drives.** Design/build on F:, the game and its measurements on G:. Neither directory
  mentions the other except through the root `CLAUDE.md` router.
- **The SMA install is mid-experiment.** On G:, BetterExperience's features are config-DISABLED
  and `SeekLab.dll` in `BepInEx/scripts` owns the pelvis. Irrelevant to CMA, alarming if
  discovered by accident.
- **The SMA-side merge is on branch `seek-dock-cycle`, not `main`.**
- **`make-il2cpp-graph.sh` builds the DUMMY-DLL graph, which is superseded.** `port_map.py` now
  reads `cma-interop-graph.json`, extracted from `BepInEx/interop`. Regenerate that one (the
  command is in this file's git history / the skill) unless you specifically want dumper output.
- `Il2CppDumper` is no longer needed for the port — interop assemblies replaced it.

---

## Scope

Five features, all in core `BetterExperience.dll`, none needing Monkey or pydlr:

Auto Thrust · Auto Seeker (auto-starts thrust) · Mission Control · Scene Camera · Player Posture

## 1. It is an app on the in-game phone

CMA has a phone hub on **Tab**, and its app list is a plain DTO array — a genuine extension
point, not something to fight:

```csharp
// Assets.UI.Sma.SmaUI/PhoneApp
.ctor(string label, string icon, string accent, System.Action onTap, bool closeOnTap)

// Assets.Productos.Juegos.Reception.Scripts.Entrevistas
AgencyPhone.BuildPhoneApps(PhoneContext) -> Il2CppReferenceArray<PhoneApp>
PhoneHubUITK.Open(apps, title, subtitle, clock, Action<bool>)
PhoneHubUITK.OnAppActivate(PhoneApp)
```

**Mechanism:** Harmony postfix on `BuildPhoneApps`, appending one `PhoneApp`. The hub renders it
like any built-in app. `OnTap` opens our panel; `closeOnTap: false` so we behave like Messages
(an app *inside* the phone) rather than the browser-style overlay.

**Why this and not a ported window:** `TValle.IU` is 33 of the 52 known breaks. This path does not
touch `TValle.IU` at all, so the hardest third of the port evaporates.

**Presentation:** UI Toolkit, reusing the phone's own stylesheet — `PhoneHubUITK` exposes
`ScrimModifierClass` and `WindowModifierClass`, so the panel inherits CMA's theme by construction
instead of by matching colours by eye. Accent colour and icon are set on the `PhoneApp` itself.

**This part is CMA-only, permanently.** `PhoneApp`, `AgencyPhone` and `PhoneHubUITK` live in
`SMA.Features` / `SmaUI`, which do not exist in SMA 23.1. Features stay shared with the SMA build;
presentation forks. That is a decision, not a limitation to fix later.

## 2. Hot-reload, designed in rather than retrofitted

ScriptEngine does not exist for BepInEx 6, but two things in this stack are better than what the
Mono side had: `BasePlugin` exposes `Unload`, and .NET 6 has **collectible AssemblyLoadContext** —
real unload, which Mono never supported.

```
BetterExperience.CMA.Contract.dll   IModule { Start(ctx); Stop(); }   default ALC, never reloaded
BetterExperience.CMA.Host.dll       thin, never changes; owns the ALC and the reload hotkey
BetterExperience.CMA.Module.dll     everything real; loaded INTO a collectible ALC
```

Reload = `Stop()` → `alc.Unload()` → load fresh bytes from disk → `Start()`. No game restart, and
no second copy of the mod to suppress — the failure that dominated the SeekLab work.

Three rules, each earned the hard way on the SMA side today:

1. **The host must never hold a Module type.** A single strong reference pins the ALC and it never
   collects — you get a silent second generation instead of a reload. All interaction goes through
   the Contract interface, which lives in its own assembly in the default context.
2. **`Stop()` must Harmony-unpatch everything.** A live patch both pins the ALC and keeps
   executing into unloaded code. Teardown is the hard half of hot-reload, not loading.
3. **Load module bytes via `File.ReadAllBytes`, never `Assembly.LoadFrom`** — the latter locks the
   DLL and the next build cannot overwrite it.

Verification hook: the host logs a generation counter and the live-object census on every reload.
"It reloaded" must be observable, because on the SMA side a reload that silently did nothing cost
several rounds of debugging.

## 3. Its own config file

`BepInEx/config/rock.betterexperience.cma.cfg` — **separate from the SMA mod's
`f95.betterexperience.cfg`**, deliberately:

- the two builds have different feature sets, so a shared file would carry keys that do nothing on
  one side and silently miss keys on the other
- the SMA install currently disables features by config to hand control to a testbed; that state
  must never leak into CMA
- an installed SMA config in a CMA install (or the reverse, via a copied folder) should be inert
  rather than half-applied

## 4. Settings apply immediately — no restart

The SMA build reads `Enabled` once, at service registration, so toggling a feature needs a
restart. On CMA that is wrong on its own terms and doubly wrong next to a reload hotkey.

- **Feature toggles** — each feature is start/stoppable at runtime: switching off runs the same
  teardown as module unload (unpatch, unsubscribe, dispose), switching on constructs it fresh.
  The panel writes the config entry; a `SettingChanged` subscription performs the transition.
  Effect is immediate and visible.
- **Hotkeys** — bound through an indirection that re-reads the config entry, not captured at
  registration. Rebinding takes effect on the next frame.
- Config writes are debounced so dragging a slider does not thrash the file.

This makes per-feature enable/disable the same operation as module reload, one level down —
so it is one teardown path to get right, exercised constantly rather than only at reload.

## Build

`cma/build-cma.sh` → `_build/BetterExperience.CMA/` → `CMA/BepInEx/plugins/BetterExperience/`.
Separate from `deploy.sh` (which builds net472 for two SMA installs); sharing them would produce a
command that half-succeeds when one target is absent.

Sources are added to the csproj **explicitly, one group at a time** — a glob over ~200 core files
yields hundreds of errors with no way to separate a real port problem from a missing reference.

## Order of work

1. Stage 0 loads under BepInEx 6 — toolchain proof, no ported code. *(in progress)*
2. Host + Contract + empty Module; prove reload works and is observable.
3. Phone app appears, opens an empty themed panel.
4. Port Auto Thrust + Auto Seeker — the actuator surface is intact, so these are the best proof.
5. Mission Control as a UITK panel; feature toggles and hotkey rebinding land here.
6. Scene Camera, Player Posture.

## Open

- The 18 remaining SIGNATURE breaks are unexamined; the compiler will surface them in step 4.
- `PhoneContext` shape is unknown — needed for the postfix signature in step 3.
- Whether the phone hub is reachable outside the office scene.
