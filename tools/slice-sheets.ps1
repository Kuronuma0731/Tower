# RPG Maker 系 4x4 表格切片器
#
# 素材庫（D14）的策展層檔案一律 128x128 = 4 列 x 4 行的 32px 格。
# 列 = 變體（顏色/種類），行 = 動畫幀。本腳本切成單格 PNG，命名 <name>_r<列>_c<行>.png。
#
# 用法：
#   powershell -File tools/slice-sheets.ps1 -SourceDir "G:\圖片放置\魔塔" -OutputDir art\pixel\raw
#
# 非 128x128 的檔案（地形條、HUD 外框等）依 32px 網格盡量切，切不整除者原樣複製。

param(
  [Parameter(Mandatory)][string]$SourceDir,
  [Parameter(Mandatory)][string]$OutputDir,
  [int]$Cell = 32
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Get-ChildItem "$SourceDir\*.png" | ForEach-Object {
  $src = New-Object System.Drawing.Bitmap $_.FullName
  $name = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
  $cols = [int][math]::Floor($src.Width / $Cell)
  $rows = [int][math]::Floor($src.Height / $Cell)

  if ($cols -lt 1 -or $rows -lt 1 -or ($src.Width % $Cell) -ne 0 -or ($src.Height % $Cell) -ne 0) {
    Copy-Item $_.FullName (Join-Path $OutputDir $_.Name) -Force
    "{0,-28} {1}x{2}  (非 32 網格，原樣複製)" -f $_.Name, $src.Width, $src.Height
    $src.Dispose(); return
  }

  for ($r = 0; $r -lt $rows; $r++) {
    for ($c = 0; $c -lt $cols; $c++) {
      $cellBmp = New-Object System.Drawing.Bitmap $Cell, $Cell, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
      $g = [System.Drawing.Graphics]::FromImage($cellBmp)
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
      $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
      $g.DrawImage($src,
        (New-Object System.Drawing.Rectangle 0, 0, $Cell, $Cell),
        ($c * $Cell), ($r * $Cell), $Cell, $Cell,
        [System.Drawing.GraphicsUnit]::Pixel)
      $g.Dispose()
      $out = Join-Path $OutputDir ("{0}_r{1}_c{2}.png" -f $name, $r, $c)
      $cellBmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
      $cellBmp.Dispose()
    }
  }
  "{0,-28} {1}x{2}  -> {3} 格" -f $_.Name, $src.Width, $src.Height, ($rows * $cols)
  $src.Dispose()
}
