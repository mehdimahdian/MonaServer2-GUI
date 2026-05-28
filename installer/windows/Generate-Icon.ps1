<#
.SYNOPSIS
    Generates the MonaServer2 GUI application icon (multi-size .ico).
    Run once locally or called from CI before building the installer.
    Output: src/MonaServer2.Desktop/Assets/icon.ico
#>

param(
    [string]$OutPath = "$PSScriptRoot\..\..\src\MonaServer2.Desktop\Assets\icon.ico"
)

Add-Type -AssemblyName System.Drawing

function New-FrameBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Background — dark navy #1A1A2E
    $bg = [System.Drawing.Color]::FromArgb(255, 26, 26, 46)
    $g.Clear($bg)

    # Rounded-rect clip at 18% radius
    $radius = [int]($size * 0.18)
    $path   = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $radius*2, $radius*2, 180, 90)
    $path.AddArc($size - $radius*2, 0, $radius*2, $radius*2, 270, 90)
    $path.AddArc($size - $radius*2, $size - $radius*2, $radius*2, $radius*2, 0, 90)
    $path.AddArc(0, $size - $radius*2, $radius*2, $radius*2, 90, 90)
    $path.CloseFigure()
    $g.SetClip($path)

    # Background gradient (top accent)
    $accent = [System.Drawing.Color]::FromArgb(255, 60, 60, 100)
    $grad   = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Point]::new(0,0),
        [System.Drawing.Point]::new(0,$size),
        $accent, $bg)
    $g.FillRectangle($grad, 0, 0, $size, $size)

    # "M" letter — white/light lavender
    $fontSize  = [int]($size * 0.62)
    $fontStyle = [System.Drawing.FontStyle]::Bold
    $font      = New-Object System.Drawing.Font("Segoe UI", $fontSize, $fontStyle, [System.Drawing.GraphicsUnit]::Pixel)
    $color     = [System.Drawing.Color]::FromArgb(255, 224, 224, 255)
    $brush     = New-Object System.Drawing.SolidBrush($color)
    $sf        = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.DrawString("M", $font, $brush, $rect, $sf)

    $font.Dispose()
    $brush.Dispose()
    $g.Dispose()
    return $bmp
}

# Build ICO with sizes: 16, 24, 32, 48, 64, 128, 256
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = $sizes | ForEach-Object { New-FrameBitmap $_ }

# Write ICO manually (PNG-compressed for 256, BMP for smaller)
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([uint16]0)            # reserved
$writer.Write([uint16]1)            # type: icon
$writer.Write([uint16]$sizes.Count) # count

# Collect frame bytes first
$frameData = @()
foreach ($frame in $frames) {
    $ms = New-Object System.IO.MemoryStream
    if ($frame.Width -eq 256) {
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        $frame.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $frameData += ,($ms.ToArray())
    $ms.Dispose()
}

# Directory entries
$offset = 6 + $sizes.Count * 16
foreach ($i in 0..($sizes.Count-1)) {
    $w    = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $h    = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$w)          # width
    $writer.Write([byte]$h)          # height
    $writer.Write([byte]0)           # color count
    $writer.Write([byte]0)           # reserved
    $writer.Write([uint16]1)         # planes
    $writer.Write([uint16]32)        # bit count
    $writer.Write([uint32]$frameData[$i].Length) # bytes in resource
    $writer.Write([uint32]$offset)   # offset to resource
    $offset += $frameData[$i].Length
}

# Frame data
foreach ($data in $frameData) { $writer.Write($data) }

$writer.Flush()
$icoBytes = $stream.ToArray()
$stream.Dispose()
$writer.Dispose()
foreach ($f in $frames) { $f.Dispose() }

$dir = Split-Path $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[IO.File]::WriteAllBytes($OutPath, $icoBytes)
Write-Host "Icon written to: $OutPath ($([int]($icoBytes.Length/1KB)) KB, $($sizes.Count) sizes)"
