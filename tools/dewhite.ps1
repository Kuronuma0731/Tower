# White-background remover (border flood fill).
#
# Turns the white background of AI-generated sprites transparent WITHOUT
# touching white pixels inside the subject (highlights, shirts, eyes):
# only pixels connected to the image border through near-white regions
# are cleared. Painterly dark outlines make the boundary crisp.
#
# Usage:
#   powershell -File tools/dewhite.ps1 -SourceDir <in> -OutputDir <out> [-Tolerance 232]
#
# A pixel is background if R,G,B are all >= Tolerance and it is reachable
# from the border via such pixels (4-neighbour flood fill).

param(
  [Parameter(Mandatory)][string]$SourceDir,
  [Parameter(Mandatory)][string]$OutputDir,
  [int]$Tolerance = 232
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Get-ChildItem "$SourceDir\*.png" | ForEach-Object {
  # Clone into a native 32bppArgb bitmap first. Source PNGs are often 24bpp
  # (no alpha channel); LockBits on those hands out a converted copy and
  # silently discards alpha writes on UnlockBits.
  $src = New-Object System.Drawing.Bitmap $_.FullName
  $w = $src.Width; $h = $src.Height
  $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.DrawImage($src, 0, 0, $w, $h)
  $g.Dispose(); $src.Dispose()

  $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
                        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $stride = $data.Stride
  $bytes = New-Object byte[] ($stride * $h)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

  # visited/background mask, BFS from all border pixels
  $mask = New-Object bool[] ($w * $h)
  $queue = New-Object 'System.Collections.Generic.Queue[int]'

  $isBg = {
    param($x, $y)
    $i = $y * $stride + $x * 4
    ($bytes[$i] -ge $Tolerance) -and ($bytes[$i+1] -ge $Tolerance) -and ($bytes[$i+2] -ge $Tolerance)
  }

  for ($x = 0; $x -lt $w; $x++) {
    foreach ($y in @(0, ($h-1))) {
      if (-not $mask[$y*$w+$x] -and (& $isBg $x $y)) { $mask[$y*$w+$x] = $true; $queue.Enqueue($y*$w+$x) }
    }
  }
  for ($y = 0; $y -lt $h; $y++) {
    foreach ($x in @(0, ($w-1))) {
      if (-not $mask[$y*$w+$x] -and (& $isBg $x $y)) { $mask[$y*$w+$x] = $true; $queue.Enqueue($y*$w+$x) }
    }
  }

  while ($queue.Count -gt 0) {
    $p = $queue.Dequeue()
    $px = $p % $w; $py = [int][math]::Floor($p / $w)
    foreach ($d in @(@(1,0), @(-1,0), @(0,1), @(0,-1))) {
      $nx = $px + $d[0]; $ny = $py + $d[1]
      if ($nx -lt 0 -or $nx -ge $w -or $ny -lt 0 -or $ny -ge $h) { continue }
      $ni = $ny * $w + $nx
      if (-not $mask[$ni] -and (& $isBg $nx $ny)) { $mask[$ni] = $true; $queue.Enqueue($ni) }
    }
  }

  # clear background pixels
  $cleared = 0
  for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
      if ($mask[$y*$w+$x]) {
        $i = $y * $stride + $x * 4
        $bytes[$i] = 0; $bytes[$i+1] = 0; $bytes[$i+2] = 0; $bytes[$i+3] = 0
        $cleared++
      }
    }
  }

  [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
  $bmp.UnlockBits($data)
  $bmp.Save((Join-Path (Resolve-Path $OutputDir) $_.Name), [System.Drawing.Imaging.ImageFormat]::Png)
  "{0,-24} cleared {1}% background" -f $_.Name, [math]::Round($cleared * 100.0 / ($w * $h))
  $bmp.Dispose()
}
