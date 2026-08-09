param(
  [switch]$Editor,        # open the Godot editor instead of running the game
  [switch]$Import,        # (re)import assets headlessly, then exit
  [switch]$Build,         # dotnet build before running
  [string]$Resolution = "1280x720"
)

# Launcher for the Godot editor / game.
#
# winget installs Godot into a path nobody wants to type:
#   %LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*\...
# This resolves it, so day-to-day commands stay short:
#
#   powershell -File tools/godot.ps1            run the game
#   powershell -File tools/godot.ps1 -Editor    open the editor
#   powershell -File tools/godot.ps1 -Import    reimport assets (after adding sprites)
#
# ASCII-only comments: PS 5.1 mangles BOM-less UTF-8 CJK.

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

$exe = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono*" `
  -Recurse -Filter "Godot*win64.exe" -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notmatch "console" } | Select-Object -First 1

if (-not $exe) {
  Write-Error "Godot (Mono) not found. Install with: winget install --id GodotEngine.GodotEngine.Mono -e"
  exit 1
}

if ($Build) {
  & dotnet build (Join-Path $repo "Tower.csproj") -v q --nologo
  if ($LASTEXITCODE -ne 0) { Write-Error "build failed"; exit 1 }
}

if ($Import) {
  & $exe.FullName --headless --import --path $repo
  exit $LASTEXITCODE
}

$args = @("--path", $repo)
if ($Editor) { $args += "--editor" } else { $args += @("--resolution", $Resolution) }

& $exe.FullName @args
