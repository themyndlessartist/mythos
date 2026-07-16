#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet build "$repo_root/Mythos.sln" --configuration Release
dotnet run --project "$repo_root/Tests/Unit/Mythos.Framework.UnitTests.csproj" --configuration Release --no-build
dotnet run --project "$repo_root/Tests/Smoke/Mythos.SmokeTests.csproj" --configuration Release --no-build
"${GODOT_BIN:-/Applications/Godot-4.7-dotnet.app/Contents/MacOS/Godot}" \
  --headless \
  --path "$repo_root/Source/Integration/Godot" \
  --editor \
  --quit
"${GODOT_BIN:-/Applications/Godot-4.7-dotnet.app/Contents/MacOS/Godot}" \
  --headless \
  --path "$repo_root/Source/Integration/Godot" \
  --quit-after 2
