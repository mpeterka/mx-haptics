<#
.SYNOPSIS
    Převede design/icon-source.png na vícevelikostní design/icon.ico (ikona exe okna nastavení a zástupce).
    Kresbu nemění, jen zmenšuje (16–256 px, PNG uvnitř ICO).
.EXAMPLE
    pwsh tools/Convert-IconToIco.ps1
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '..\design\icon-source.png'),
    [string]$Out = (Join-Path $PSScriptRoot '..\design\icon.ico'),
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
)

Add-Type -AssemblyName System.Drawing

$src = [System.Drawing.Bitmap]::FromFile([System.IO.Path]::GetFullPath($Source))
$images = foreach ($s in $Sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
    $attr = New-Object System.Drawing.Imaging.ImageAttributes; $attr.SetWrapMode('TileFlipXY')
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle 0, 0, $s, $s), 0, 0, $src.Width, $src.Height, 'Pixel', $attr)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
}
$src.Dispose()

$fs = [System.IO.File]::Create([System.IO.Path]::GetFullPath($Out))
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $dim = if ($img.Size -ge 256) { 0 } else { $img.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$img.Data.Length); $w.Write([uint32]$offset)
    $offset += $img.Data.Length
}
foreach ($img in $images) { $w.Write($img.Data) }
$w.Dispose()
Write-Host "ICO uloženo: $([System.IO.Path]::GetFullPath($Out)) ($($Sizes -join ', ') px)"
