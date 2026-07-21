$PLUGIN = $PSScriptRoot
$BASE   = "$PLUGIN\decompiled"
$LOG    = "$PLUGIN\build_output.txt"

Write-Host "Building BetterExperience_2.0.sln..." -ForegroundColor Cyan

$output = dotnet build "$BASE\BetterExperience_2.0.sln" -c Release 2>&1
$output | Out-File -FilePath $LOG -Encoding utf8

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Write-Host ""

    foreach ($line in $output) {
        $text = $line.ToString()
        if ($text -match "\berror\b" -or $text -match "Error") {
            Write-Host $text -ForegroundColor Red
        } elseif ($text -match "\bwarning\b" -or $text -match "Warning") {
            Write-Host $text -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "Full log: $LOG" -ForegroundColor DarkGray
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host ""
Write-Host "Build succeeded. Copying DLLs..." -ForegroundColor Green

Copy-Item -Force "$BASE\BetterExperience\bin\Release\net472\BetterExperience.dll" $PLUGIN
Copy-Item -Force "$BASE\Better_Cloth\bin\Release\net472\Better_Cloth.dll"        $PLUGIN
Copy-Item -Force "$BASE\Better_Scene\bin\Release\net472\Better_Scene.dll"        $PLUGIN
Copy-Item -Force "$BASE\Better_Story\bin\Release\net472\Better_Story.dll"        $PLUGIN

Write-Host "Done." -ForegroundColor Green
Read-Host "Press Enter to exit"
