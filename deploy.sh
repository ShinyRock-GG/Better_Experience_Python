#!/usr/bin/env bash
# Build BetterExperience and deploy to BOTH installs, verifying byte-identity.
#
# WHY THIS EXISTS. Deploying to G: fails whenever the game holds the DLL open, and a `cp` that
# fails is easy to skim past — which happened twice in one session, and both times the owner then
# tested a build that did not contain the fix being tested. A stale deploy is worse than a failed
# build: a failed build announces itself, while a stale one silently invalidates the next round of
# observations and everything reasoned from them.
#
# So this exits NON-ZERO and says so loudly if either install ends up different from the build.

set -u

SRC="F:/Games/AAA/SMA_23.1_HomeworkTestbed/BepInEx/plugins/BetterExperience/decompiled"
FP="F:/Games/AAA/SMA_23.1_HomeworkTestbed/BepInEx/plugins/BetterExperience"
GP="G:/Games/AAA/Some_Modeling_Agency_0.23.1_f1/BepInEx/plugins/BetterExperience"
DLLS="BetterExperience Better_Cloth Better_Scene Better_Story"

echo "=== building ==="
( cd "$SRC" && dotnet build BetterExperience_2.0.sln -c Release --nologo 2>&1 \
    | grep -iE "error CS|Build succeeded" | head -20 )

BUILT="$SRC/BetterExperience/bin/Release/net472/BetterExperience.dll"
if [ ! -f "$BUILT" ]; then
  echo "!!! BUILD PRODUCED NO DLL - nothing deployed"
  exit 1
fi

fail=0
for n in $DLLS; do
  s="$SRC/$n/bin/Release/net472/$n.dll"
  [ -f "$s" ] || continue
  cp -f "$s" "$FP/$n.dll" 2>/dev/null || { echo "!!! F: copy failed: $n"; fail=1; }
  cp -f "$s" "$GP/$n.dll" 2>/dev/null || { echo "!!! G: copy failed (file locked - game running?): $n"; fail=1; }
done

# Source mirror, so G:'s decompiled tree matches what its DLL was built from.
for c in AutoThrustFeature AutoSeekerFeature MissionControlFeature; do
  cp -f "$SRC/BetterExperience/BetterExperience.Features/$c.cs" \
        "$GP/decompiled/BetterExperience/BetterExperience.Features/$c.cs" 2>/dev/null
done

# === SEEKLAB — the hot-reload testbed ===
# Built AFTER BetterExperience because it references BE's build output; building it first would
# link it against the previous BE and produce a lab that disagrees with the mod it is testing.
# Deployed to BepInEx\scripts (NOT plugins) so ScriptEngine owns it and F6 reloads it.
LAB="$FP/seeklab"
if [ -f "$LAB/SeekLab.csproj" ]; then
  echo "=== building SeekLab (testbed) ==="
  ( cd "$LAB" && MSBUILDDISABLENODEREUSE=1 dotnet build SeekLab.csproj -c Release --nologo -nodeReuse:false 2>&1 \
      | grep -iE "error CS|Build succeeded" | head -10 )
  LABBUILT="F:/Games/AAA/SMA_23.1_HomeworkTestbed/_build/SeekLab/Release/SeekLab.dll"
  if [ ! -f "$LABBUILT" ]; then
    echo "!!! SEEKLAB BUILD PRODUCED NO DLL"
    fail=1
  else
    for scripts in "$FP/../../scripts" "$GP/../../scripts"; do
      mkdir -p "$scripts" 2>/dev/null
      cp -f "$LABBUILT" "$scripts/SeekLab.dll" 2>/dev/null \
        || { echo "!!! SeekLab copy failed (game running with it loaded?): $scripts"; fail=1; }
      cp -f "${LABBUILT%.dll}.pdb" "$scripts/SeekLab.pdb" 2>/dev/null
      cmp -s "$LABBUILT" "$scripts/SeekLab.dll" && echo "OK   $scripts/SeekLab.dll" \
        || { echo "!!!! STALE: $scripts/SeekLab.dll"; fail=1; }
    done
  fi
fi

echo "=== verifying ==="
for tgt in "$FP" "$GP"; do
  if cmp -s "$BUILT" "$tgt/BetterExperience.dll"; then
    echo "OK   $tgt"
  else
    echo "!!!! STALE: $tgt/BetterExperience.dll does NOT match the build"
    fail=1
  fi
done

if [ $fail -ne 0 ]; then
  echo ""
  echo "############################################################"
  echo "# DEPLOY INCOMPLETE - DO NOT TEST. Close the game and rerun. #"
  echo "# Testing now measures the OLD build and wastes the run.     #"
  echo "############################################################"
  exit 1
fi
echo "deployed + byte-verified on both installs"
