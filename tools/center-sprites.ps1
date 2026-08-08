# 素材置中處理器
#
# 生圖工具（ChatGPT 等）常把主體貼在大畫布上、偏離中心。這支腳本裁到 alpha
# 邊界框、置中、輸出正方形，讓所有素材能用同一個 pivot 對格。
#
# 用法：
#   pwsh tools/center-sprites.ps1 art/source/monsters art/sprites/monsters
#
# 原檔保留在 source/ 不動，處理結果寫進 sprites/。重跑安全（覆寫輸出）。

param(
  [Parameter(Mandatory)][string]$SourceDir,
  [Parameter(Mandatory)][string]$OutputDir,
  [double]$Padding = 1.10,      # 邊界框外留白倍率
  [int]$AlphaThreshold = 16     # 低於此 alpha 視為透明（濾掉去背殘留的半透明邊緣）
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Get-ChildItem "$SourceDir\*.png" | ForEach-Object {
  $bmp = New-Object System.Drawing.Bitmap $_.FullName
  $w = $bmp.Width; $h = $bmp.Height

  $data = $bmp.LockBits(
    (New-Object System.Drawing.Rectangle 0, 0, $w, $h),
    [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $bytes = New-Object byte[] ($data.Stride * $h)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
  $bmp.UnlockBits($data)

  $minX = $w; $maxX = -1; $minY = $h; $maxY = -1
  for ($y = 0; $y -lt $h; $y++) {
    $row = $y * $data.Stride
    for ($x = 0; $x -lt $w; $x++) {
      if ($bytes[$row + $x * 4 + 3] -gt $AlphaThreshold) {
        if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
      }
    }
  }

  if ($maxX -lt 0) {
    Write-Warning "$($_.Name): 整張透明，跳過"
    $bmp.Dispose(); return
  }

  $bw = $maxX - $minX + 1; $bh = $maxY - $minY + 1
  $side = [int][math]::Ceiling([math]::Max($bw, $bh) * $Padding)

  $out = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($out)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $g.DrawImage($bmp,
    (New-Object System.Drawing.Rectangle ([int](($side - $bw) / 2)), ([int](($side - $bh) / 2)), $bw, $bh),
    $minX, $minY, $bw, $bh, [System.Drawing.GraphicsUnit]::Pixel)
  $g.Dispose()

  $out.Save((Join-Path $OutputDir $_.Name), [System.Drawing.Imaging.ImageFormat]::Png)
  "{0,-24} {1}x{2} -> {3}x{3}" -f $_.Name, $bw, $bh, $side
  $out.Dispose(); $bmp.Dispose()
}
