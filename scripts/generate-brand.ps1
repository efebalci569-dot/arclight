<#
.SYNOPSIS
    Renders the Arclight mark to PNG files and a multi-size Windows .ico.

.DESCRIPTION
    The mark is defined once here as vector geometry (64x64 design grid) and rendered with WPF,
    so the app icon, favicon and website assets always match. Run with Windows PowerShell:

        powershell -ExecutionPolicy Bypass -File scripts\generate-brand.ps1
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\assets\brand')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# --- Design (64 x 64 grid) ---------------------------------------------------------------
$LegsPath = 'M 13.5 52 L 32 12 L 50.5 52'
$ArcPath = 'M 18.1 42 A 16 16 0 0 1 45.9 42'
$Background = '#09090B'
$Ink = '#F5F5F7'
$AccentA = '#8B5CF6'
$AccentB = '#38BDF8'

function New-Brush([string]$hex) {
    $brush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($hex))
    $brush.Freeze()
    return $brush
}

function Render-Mark([int]$size, [bool]$withTile, [string]$path) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $scale = $size / 64.0
    $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform $scale, $scale))

    if ($withTile) {
        $rect = New-Object System.Windows.Rect 0, 0, 64, 64
        $dc.DrawRoundedRectangle((New-Brush $Background), $null, $rect, 14, 14)
        $innerPen = New-Object System.Windows.Media.Pen ((New-Brush '#14FFFFFF')), 1
        $dc.DrawRoundedRectangle($null, $innerPen, (New-Object System.Windows.Rect 0.5, 0.5, 63, 63), 13.5, 13.5)
    }

    # Small sizes need heavier strokes to stay legible.
    $legWidth = if ($size -le 24) { 8.0 } elseif ($size -le 48) { 6.6 } else { 6.0 }
    $arcWidth = if ($size -le 24) { 6.0 } elseif ($size -le 48) { 5.0 } else { 4.5 }

    # The arc is drawn first so its ends tuck under the legs and read as the A's crossbar.
    $gradient = New-Object System.Windows.Media.LinearGradientBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($AccentA)), ([System.Windows.Media.ColorConverter]::ConvertFromString($AccentB)), 0
    $arcPen = New-Object System.Windows.Media.Pen $gradient, $arcWidth
    $arcPen.StartLineCap = 'Round'; $arcPen.EndLineCap = 'Round'
    $dc.DrawGeometry($null, $arcPen, [System.Windows.Media.Geometry]::Parse($ArcPath))

    $legsPen = New-Object System.Windows.Media.Pen ((New-Brush $Ink)), $legWidth
    $legsPen.StartLineCap = 'Round'; $legsPen.EndLineCap = 'Round'; $legsPen.LineJoin = 'Round'
    $dc.DrawGeometry($null, $legsPen, [System.Windows.Media.Geometry]::Parse($LegsPath))

    $dc.Pop()
    $dc.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap $size, $size, 96, 96, ([System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

function Write-Ico([string[]]$pngFiles, [int[]]$sizes, [string]$target) {
    $images = $pngFiles | ForEach-Object { , [System.IO.File]::ReadAllBytes($_) }
    $stream = [System.IO.File]::Create($target)
    $writer = New-Object System.IO.BinaryWriter $stream
    try {
        $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$images.Count)
        $offset = 6 + 16 * $images.Count
        for ($i = 0; $i -lt $images.Count; $i++) {
            $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([UInt16]1); $writer.Write([UInt16]32)
            $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write($image) }
    }
    finally { $writer.Dispose() }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path

$iconSizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$iconFiles = @()
foreach ($s in $iconSizes) {
    $file = Join-Path $OutDir "icon-$s.png"
    Render-Mark $s $true $file
    $iconFiles += $file
}

Write-Ico $iconFiles $iconSizes (Join-Path $OutDir 'arclight.ico')
Render-Mark 512 $true (Join-Path $OutDir 'arclight-icon-512.png')
Render-Mark 512 $false (Join-Path $OutDir 'arclight-mark-512.png')
Render-Mark 128 $false (Join-Path $OutDir 'arclight-mark-128.png')

# Keep only the files other projects consume.
$iconFiles | Where-Object { $_ -notmatch 'icon-(32|256)\.png$' } | Remove-Item
Write-Host "Brand assets written to $OutDir"
