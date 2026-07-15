$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$godotBin = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { "Godot_v4.7-stable_mono_win64.exe" }

dotnet build (Join-Path $repoRoot "Mythos.sln") --configuration Release
dotnet run --project (Join-Path $repoRoot "Tests/Smoke/Mythos.SmokeTests.csproj") --configuration Release --no-build
& $godotBin --headless --path (Join-Path $repoRoot "Source/Integration/Godot") --editor --quit
& $godotBin --headless --path (Join-Path $repoRoot "Source/Integration/Godot") --quit-after 2
