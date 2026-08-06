# make-release.ps1 — build + package BetterExperience release tiers.
# Tiers: Lite (core+Cloth, zero deps), Standard (+Scene; Monkey DECLARED not bundled),
# Full (+Story +pydlr with licenses). Licensing is a HARD GATE: packaging fails without
# the license texts. decompiled/ and any non-runtime files are never included.
param([ValidateSet("Lite","Standard","Full","All")][string]$Tier = "All", [switch]$SkipBuild)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot                       # .../BetterExperience
$dec  = Join-Path $root "decompiled"
$out  = Join-Path $PSScriptRoot "out"
$gameG = "G:/Games/AAA/Some_Modeling_Agency_0.23.1_f1"

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

function Pack([string]$name, [string[]]$dlls, [bool]$withPydlr, [string]$readmeNote) {
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
    $zip = Join-Path $out "BetterExperience-$name-$stamp.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path (Join-Path $stage "BepInEx") -DestinationPath $zip
    Remove-Item $stage -Recurse -Force
    # Verify: no forbidden content in the archive
    $entries = (Get-ChildItem $zip | ForEach-Object { [IO.Compression.ZipFile]::OpenRead($_.FullName) }).Entries.FullName
    if ($entries | Where-Object { $_ -match "decompiled|\.pdb$|\.cs$" }) { throw "FORBIDDEN CONTENT in $name package" }
    if ($withPydlr -and -not ($entries | Where-Object { $_ -match "THIRD-PARTY-NOTICES" })) { throw "$name package missing notices" }
    Write-Host ("  {0,-10} {1,8:N0} KB  {2} entries" -f $name, ((Get-Item $zip).Length/1kb), $entries.Count)
}

Write-Host "Packaging to $out"
if ($Tier -in @("Lite","All"))     { Pack "Lite"     @("BetterExperience","Better_Cloth") $false "No prerequisites beyond BepInEx 5.x + SMA 0.23.1. Monkey-gated features (SceneCamera, PlayerScaler) are ACTIVE in this tier." }
if ($Tier -in @("Standard","All")) { Pack "Standard" @("BetterExperience","Better_Cloth","Better_Scene") $false "PREREQUISITE: Monkey mod must be installed (Better_Scene requires it). Not bundled — see THIRD-PARTY-NOTICES." }
if ($Tier -in @("Full","All"))     { Pack "Full"     @("BetterExperience","Better_Cloth","Better_Scene","Better_Story") $true "PREREQUISITES: Monkey mod (not bundled). Includes pydlr Python runtime (IronPython, Apache-2.0 — licenses included)." }
Write-Host "Done."
