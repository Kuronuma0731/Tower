param(
  [string]$Repo = "G:\Claude\Tower"
)

# Copies data/*.csv into Assets/StreamingAssets/data/ so the game reads what the
# repo actually says.
#
# Why this exists: data/ at the repo root is the single source of truth (CLAUDE.md),
# but the Unity runtime loads from StreamingAssets. That copy had been made by hand
# and silently fell behind - five prologue dialogue lines were added to data/ and
# the game kept showing nothing, because it was reading a stale duplicate. Exactly
# the drift the Catalog refactor was meant to kill, just one layer further out.
#
# Run after editing anything in data/. CoreVerify checks the two stay in sync.
#
# ASCII-only comments: PS 5.1 mangles BOM-less UTF-8 CJK.

$src = Join-Path $Repo "data"
$dst = Join-Path $Repo "Assets\StreamingAssets\data"

if (-not (Test-Path $src)) { Write-Error "missing $src"; exit 1 }
New-Item -ItemType Directory -Force $dst | Out-Null

$copied = 0
foreach ($f in Get-ChildItem $src -Filter *.csv) {
  Copy-Item $f.FullName (Join-Path $dst $f.Name) -Force
  $copied++
}
"synced $copied csv files -> $dst"
