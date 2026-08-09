param(
  [string]$OutDir = "G:\Claude\Tower\Assets\StreamingAssets\sprites"
)

# Generates the 8-frame impact burst used by collision battles.
#
# Why generated instead of sourced from the asset pack: the pack has no
# explosion animation, and an effect we draw ourselves carries no third-party
# licensing question (D14's pack is still unresolved on that front).
# Shape and timing copied from the original 6219_newMT.swf, measured frame by
# frame off a 60fps capture: a white-hot core that blooms into an 8-point
# yellow star, then breaks into orange sparks that radiate out and fade.
#
# ASCII-only comments on purpose: Windows PowerShell 5.1 reads BOM-less UTF-8
# as ANSI and mangles CJK, which has broken scripts in this project before.

Add-Type -AssemblyName System.Drawing

$SIZE = 48
$FRAMES = 8
New-Item -ItemType Directory -Force $OutDir | Out-Null

function New-Star {
  param([single]$cx, [single]$cy, [single]$outer, [single]$inner, [int]$points)
  $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
  for ($i = 0; $i -lt $points * 2; $i++) {
    $r = if ($i % 2 -eq 0) { $outer } else { $inner }
    $a = [Math]::PI * $i / $points - [Math]::PI / 2
    $pts.Add((New-Object System.Drawing.PointF(($cx + $r * [Math]::Cos($a)), ($cy + $r * [Math]::Sin($a)))))
  }
  return $pts.ToArray()
}

for ($f = 0; $f -lt $FRAMES; $f++) {
  $bmp = New-Object System.Drawing.Bitmap($SIZE, $SIZE, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'
  $g.Clear([System.Drawing.Color]::Transparent)

  $c = $SIZE / 2.0
  $t = $f / [single]($FRAMES - 1)     # 0..1

  if ($f -le 4) {
    # Bloom phase: core grows, star spikes extend
    $outer = 5 + 19 * ($f / 4.0)
    $inner = 2 + 7 * ($f / 4.0)
    $alpha = [int](255 * (1.0 - 0.15 * $f / 4.0))

    $star = New-Star -cx $c -cy $c -outer $outer -inner $inner -points 8
    $yellow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 255, 196, 64))
    $g.FillPolygon($yellow, $star)

    # White-hot core
    $coreR = 4 + 5 * ($f / 4.0)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 255, 250, 224))
    $g.FillEllipse($white, ($c - $coreR), ($c - $coreR), ($coreR * 2), ($coreR * 2))
  }
  else {
    # Break-up phase: sparks fly outward and fade
    $k = ($f - 4) / 3.0                # 0..1
    $ringR = 16 + 10 * $k
    $sparkR = [single](4.5 - 2.5 * $k)
    $alpha = [int](210 * (1.0 - $k))

    $orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($alpha, 255, 150, 48))
    $pale = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb([int]($alpha * 0.8), 255, 224, 150))
    for ($i = 0; $i -lt 8; $i++) {
      $a = [Math]::PI * 2 * $i / 8 - [Math]::PI / 2
      $sx = $c + $ringR * [Math]::Cos($a)
      $sy = $c + $ringR * [Math]::Sin($a)
      $g.FillEllipse($orange, ($sx - $sparkR), ($sy - $sparkR), ($sparkR * 2), ($sparkR * 2))
      $g.FillEllipse($pale, ($sx - $sparkR * 0.45), ($sy - $sparkR * 0.45), ($sparkR * 0.9), ($sparkR * 0.9))
    }
    # Residual faint star
    if ($k -lt 0.7) {
      $faint = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb([int](110 * (1 - $k / 0.7)), 255, 210, 110))
      $g.FillPolygon($faint, (New-Star -cx $c -cy $c -outer (22 - 6 * $k) -inner 6 -points 8))
    }
  }

  $g.Dispose()
  $path = Join-Path $OutDir ("fx_burst_f{0}.png" -f $f)
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

"generated $FRAMES burst frames in $OutDir"
