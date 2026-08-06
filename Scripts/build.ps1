$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$godotBin = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { "Godot_v4.7-stable_mono_win64.exe" }

node (Join-Path $repoRoot "Scripts/build_content_bundle.mjs") `
    (Join-Path $repoRoot "Data/TitlePackages/Genesis/Lakewood") `
    (Join-Path $repoRoot "Source/Integration/Godot/Content/genesis-lakewood.bundle.json")
dotnet build (Join-Path $repoRoot "Mythos.sln") --configuration Release
dotnet run --project (Join-Path $repoRoot "Tests/Unit/Mythos.Framework.UnitTests.csproj") --configuration Release --no-build
dotnet run --project (Join-Path $repoRoot "Tests/Smoke/Mythos.SmokeTests.csproj") --configuration Release --no-build
& $godotBin --headless --path (Join-Path $repoRoot "Source/Integration/Godot") --editor --quit
& $godotBin --headless --path (Join-Path $repoRoot "Source/Integration/Godot") --quit-after 2
