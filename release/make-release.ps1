# make-release.ps1 — build + package BetterExperience release tiers.
# Tiers: Lite (core+Cloth, zero deps), Standard (+Scene; Monkey DECLARED not bundled),
# Full (+Story +pydlr with licenses). Licensing is a HARD GATE: packaging fails without
# the license texts. decompiled/ and any non-runtime files are never included.
param([ValidateSet("Lite","Standard","Full","Complete","All")][string]$Tier = "All", [switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot                       # .../BetterExperience
$dec  = Join-Path $root "decompiled"
$out  = Join-Path $PSScriptRoot "out"
$gameG = "G:/Games/AAA/Some_Modeling_Agency_0.23.1_f1"
# Monkey's own redistributable, not the live game install. 2.1 MB and self-contained: it carries
# the config/Monkey tree with its placeholder readmes, which is what BE actually needs present.
# Taking Monkey.dll from the game folder instead shipped the DLL WITHOUT that tree, and BE died on
# a clean install with "Could not find a part of the path ...\config\Monkey\Assets".
$monkeyPkg = "F:/Games/AAA/0_mods/Monkey_Package"

# ── 1. License gate (auto-fetch once, then hard-require) ──────────────────────
$apache = Join-Path $PSScriptRoot "LICENSE-Apache-2.0.txt"
$mit    = Join-Path $PSScriptRoot "LICENSE-MIT-dotnet.txt"
if (-not (Test-Path $apache)) {
    Write-Host "Fetching Apache-2.0 license text (one-time)..."
    try { Invoke-WebRequest -Uri "https://www.apache.org/licenses/LICENSE-2.0.txt" -OutFile $apache -UseBasicParsing } catch {}
}
if (-not (Test-Path $mit)) {
    Write-Host "Fetching MIT (dotnet) license text (one-time)..."
    try { Invoke-WebRequest -Uri "https://raw.githubusercontent.com/dotnet/runtime/main/LICENSE.TXT" -OutFile $mit -UseBasicParsing } catch {}
}
foreach ($f in @($apache, $mit, (Join-Path $PSScriptRoot "THIRD-PARTY-NOTICES.md"))) {
    if (-not (Test-Path $f) -or (Get-Item $f).Length -lt 500) {
        throw "LICENSE GATE: missing/empty $(Split-Path $f -Leaf) — packaging refused. Fetch it manually and rerun."
    }
}

# ── 2. Build ──────────────────────────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host "Building Release..."
    dotnet build (Join-Path $dec "BetterExperience_2.0.sln") -c Release --nologo | Select-String "error|Build succeeded" | ForEach-Object { $_.Line }
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
}
$bin = @{
    BetterExperience = Join-Path $dec "BetterExperience/bin/Release/net472/BetterExperience.dll"
    Better_Cloth     = Join-Path $dec "Better_Cloth/bin/Release/net472/Better_Cloth.dll"
    Better_Scene     = Join-Path $dec "Better_Scene/bin/Release/net472/Better_Scene.dll"
    Better_Story     = Join-Path $dec "Better_Story/bin/Release/net472/Better_Story.dll"
}
foreach ($k in $bin.Keys) { if (-not (Test-Path $bin[$k])) { throw "missing build output: $($bin[$k])" } }

# ── 3. Package tiers ──────────────────────────────────────────────────────────
New-Item -ItemType Directory -Force $out | Out-Null
$stamp = Get-Date -Format "yyyyMMdd"

function Pack([string]$name, [string[]]$dlls, [bool]$withPydlr, [string]$readmeNote, [bool]$withMonkey = $false, [bool]$withBepInEx = $false) {
    $stage = Join-Path $out "_stage"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $beDir = New-Item -ItemType Directory -Force (Join-Path $stage "BepInEx/plugins/BetterExperience")
    foreach ($d in $dlls) { Copy-Item $bin[$d] $beDir }
    Set-Content (Join-Path $beDir "README.txt") ("BetterExperience — $name tier ($stamp)`r`n$readmeNote")
    if ($withPydlr) {
        $py = New-Item -ItemType Directory -Force (Join-Path $stage "BepInEx/plugins/pydlr")
        # runtime DLLs only — decompiled/ and any working material NEVER ship
        foreach ($dll in Get-ChildItem "$gameG/BepInEx/plugins/pydlr" -Filter "*.dll" -File) { Copy-Item $dll.FullName $py }
        Copy-Item $apache $py; Copy-Item $mit $py
        Copy-Item (Join-Path $PSScriptRoot "THIRD-PARTY-NOTICES.md") $py
    }
    if ($withMonkey) {
        # Monkey is a THIRD-PARTY mod redistributed here as a required prerequisite, taken from
        # its OWN distribution ($monkeyPkg) rather than from the live game folder.
        #
        # The config/Monkey tree comes with it, and that is the point: BE's MonkeyCompanion
        # resolves BepInEx/config/Monkey/Assets at startup and throws if the directory is absent,
        # which on a clean install killed BE outright ("BetterExperience is doomed"). The earlier
        # "~2.3 GB, not shippable" note described a populated LOCAL install; the redistributable
        # ships the same tree with placeholder readmes and weighs 2 MB. What Monkey needs is the
        # directories to exist, not their contents.
        $mkDll = "$monkeyPkg/BepInEx/plugins/Monkey/Monkey.dll"
        $mkCfg = "$monkeyPkg/BepInEx/config/Monkey"
        if (-not (Test-Path $mkDll)) { throw "MONKEY GATE: Monkey.dll not found at $mkDll" }
        if (-not (Test-Path "$mkCfg/Assets")) { throw "MONKEY GATE: config/Monkey/Assets not found at $mkCfg" }
        $mk = New-Item -ItemType Directory -Force (Join-Path $stage "BepInEx/plugins/Monkey")
        Copy-Item $mkDll $mk
        # Create the parent FIRST: Copy-Item -Recurse into a non-existent destination renames the
        # source folder to the destination name, so config/Monkey/* silently became config/*.
        $cfgDir = New-Item -ItemType Directory -Force (Join-Path $stage "BepInEx/config")
        Copy-Item $mkCfg $cfgDir -Recurse -Force
        $mkReadme = "$monkeyPkg/Monkey_Readme.txt"
        if (Test-Path $mkReadme) { Copy-Item $mkReadme $stage }
        Set-Content (Join-Path $mk "README-Monkey.txt") @"
Monkey is a third-party mod, bundled here because BetterExperience/Better_Scene
requires it at runtime. Taken from Monkey's own redistributable package.

Included: Monkey.dll, and BepInEx/config/Monkey/ (monkey.json, settings, and the
Assets/ Clothing/ Mods/ directories with their placeholder readmes). Those
directories must EXIST or BetterExperience fails at startup; their contents are
yours to populate.

Monkey remains the property of its author under its own license/terms.
"@
    }
    if ($withBepInEx) {
        # THE LOADER ITSELF. Without it the archive is plugins for a framework the player may not
        # have, and "extract over the game root" quietly does nothing. Ships the doorstop proxy
        # (winhttp.dll) and doorstop_config.ini at the ROOT — those are what the game actually
        # loads at startup; BepInEx/core is inert without them.
        #
        # Runtime only: no .xml doc files, no logs, no cache, and NOT BepInEx/config — a shipped
        # config would overwrite the settings of anyone who already has BepInEx, and BepInEx
        # regenerates defaults on first run anyway.
        $core = New-Item -ItemType Directory -Force (Join-Path $stage "BepInEx/core")
        foreach ($dll in Get-ChildItem "$gameG/BepInEx/core" -Filter "*.dll" -File) { Copy-Item $dll.FullName $core }
        foreach ($f in @("winhttp.dll", "doorstop_config.ini")) {
            $src = Join-Path $gameG $f
            if (-not (Test-Path $src)) { throw "BEPINEX GATE: $f not found at game root ($gameG)" }
            Copy-Item $src $stage
        }
        # BepInEx is LGPL-2.1; redistributing the binaries carries the same licensing duty as
        # pydlr's, so it gets the same treatment rather than an exception.
        Set-Content (Join-Path $core "README-BepInEx.txt") @"
BepInEx 5.x is a third-party mod loader, bundled here so this package can be
extracted straight over a clean game install.

Included: BepInEx/core runtime DLLs, plus winhttp.dll and doorstop_config.ini
at the game root (the loader entry point).

NOT included: BepInEx/config (yours is preserved; defaults regenerate on first
run), documentation XML, logs and cache.

BepInEx is licensed LGPL-2.1 and remains the property of its authors.
Source: https://github.com/BepInEx/BepInEx
"@
    }
    $zip = Join-Path $out "BetterExperience-$name-$stamp.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    # -Path the stage CONTENTS, not just BepInEx/, so root-level loader files are included.
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
    Remove-Item $stage -Recurse -Force
    # Verify: no forbidden content in the archive
    $entries = (Get-ChildItem $zip | ForEach-Object { [IO.Compression.ZipFile]::OpenRead($_.FullName) }).Entries.FullName
    if ($entries | Where-Object { $_ -match "decompiled|\.pdb$|\.cs$" }) { throw "FORBIDDEN CONTENT in $name package" }
    if ($withPydlr -and -not ($entries | Where-Object { $_ -match "THIRD-PARTY-NOTICES" })) { throw "$name package missing notices" }
    if ($withMonkey) {
        if (-not ($entries | Where-Object { $_ -match "Monkey/Monkey\.dll$" })) { throw "$name package missing Monkey.dll" }
        # The directory BE resolves at startup. Zips do not carry empty directories, so gate on a
        # FILE inside it — an entry for the folder alone would extract to nothing.
        if (-not ($entries | Where-Object { $_ -match "config/Monkey/Assets/" })) { throw "$name package missing config/Monkey/Assets contents" }
    }
    if ($withBepInEx) {
        # Gate on the two files that actually make the loader run. Shipping BepInEx/core without
        # the doorstop proxy produces an archive that looks complete and loads nothing.
        foreach ($need in @("^winhttp\.dll$", "^doorstop_config\.ini$", "BepInEx/core/BepInEx\.dll$")) {
            if (-not ($entries | Where-Object { $_ -match $need })) { throw "$name package missing $need" }
        }
    }
    Write-Host ("  {0,-10} {1,8:N0} KB  {2} entries" -f $name, ((Get-Item $zip).Length/1kb), $entries.Count)
}

Write-Host "Packaging to $out"
if ($Tier -in @("Lite","All"))     { Pack "Lite"     @("BetterExperience","Better_Cloth") $false "No prerequisites beyond BepInEx 5.x + SMA 0.23.1. Monkey-gated features (SceneCamera, PlayerScaler) are ACTIVE in this tier." }
if ($Tier -in @("Standard","All")) { Pack "Standard" @("BetterExperience","Better_Cloth","Better_Scene") $false "PREREQUISITE: Monkey mod must be installed (Better_Scene requires it). Not bundled — see THIRD-PARTY-NOTICES." }
if ($Tier -in @("Full","All"))     { Pack "Full"     @("BetterExperience","Better_Cloth","Better_Scene","Better_Story") $true "PREREQUISITES: Monkey mod (not bundled). Includes pydlr Python runtime (IronPython, Apache-2.0 — licenses included)." }
if ($Tier -in @("Complete","All")) {
    Pack "Complete" @("BetterExperience","Better_Cloth","Better_Scene","Better_Story") $true @"
Everything required, in game-matching folder structure — extract over the game root.
Includes: the BepInEx 5.x loader (core + winhttp.dll + doorstop_config.ini), all 4
BetterExperience DLLs, the pydlr Python runtime (IronPython, Apache-2.0), and
Monkey.dll (third-party, required by Better_Scene).
NOT bundled: BepInEx/config (yours is preserved), and Monkey's own config/assets
(BepInEx/config/Monkey, ~2.3 GB) — see plugins/Monkey/README-Monkey.txt.
"@ $true $true
}
Write-Host "Done."
