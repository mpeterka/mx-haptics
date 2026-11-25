<#
.SYNOPSIS
    Vygeneruje ikonu pluginu: malý pes drží v tlapkách kurzor, kolem vlnky vibrace.
.EXAMPLE
    pwsh tools/Make-Icon.ps1
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\src\package\metadata\Icon256x256.png')
)

Add-Type -AssemblyName System.Drawing

function New-Brush([int]$r, [int]$g, [int]$b, [int]$a = 255) {
    New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($a, $r, $g, $b))
}
function New-Pen([int]$r, [int]$g, [int]$b, [float]$w, [int]$a = 255) {
    $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($a, $r, $g, $b)), $w
    $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
    $p
}
function Pt([float]$x, [float]$y) { New-Object System.Drawing.PointF $x, $y }

$s = 256
$bmp = New-Object System.Drawing.Bitmap $s, $s
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

# --- podklad: zaoblený čtverec s gradientem ---
$r = 56
$bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
$bgPath.AddArc(8, 8, $r, $r, 180, 90); $bgPath.AddArc($s - 8 - $r, 8, $r, $r, 270, 90)
$bgPath.AddArc($s - 8 - $r, $s - 8 - $r, $r, $r, 0, 90); $bgPath.AddArc(8, $s - 8 - $r, $r, $r, 90, 90)
$bgPath.CloseFigure()
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (Pt 0 0), (Pt $s $s), `
    ([System.Drawing.Color]::FromArgb(255, 0, 214, 180)), ([System.Drawing.Color]::FromArgb(255, 0, 120, 140))
$g.FillPath($bg, $bgPath)

$outline = New-Pen 0 60 70 5
$fur = New-Brush 214 150 80
$furDark = New-Brush 120 70 30
$muzzle = New-Brush 245 225 190
$ink = New-Brush 40 25 15

# --- pes: sedící tělo + hlava vpravo dole, natočený ke kurzoru ---
$body = New-Object System.Drawing.Drawing2D.GraphicsPath
$body.AddEllipse(132, 168, 92, 76)
$g.FillPath($fur, $body); $g.DrawPath($outline, $body)
# bříško
$g.FillEllipse($muzzle, 158, 190, 44, 46)

# uši (za hlavou)
$g.FillEllipse($furDark, 124, 92, 30, 58); $g.DrawEllipse($outline, 124, 92, 30, 58)
$g.FillEllipse($furDark, 204, 92, 30, 58); $g.DrawEllipse($outline, 204, 92, 30, 58)
# hlava
$g.FillEllipse($fur, 136, 84, 86, 80); $g.DrawEllipse($outline, 136, 84, 86, 80)
# čenich
$g.FillEllipse($muzzle, 156, 124, 46, 32)
# oči – koukají nahoru doleva na kurzor
$g.FillEllipse($ink, 154, 108, 12, 14); $g.FillEllipse($ink, 190, 108, 12, 14)
$g.FillEllipse([System.Drawing.Brushes]::White, 155, 109, 5, 5); $g.FillEllipse([System.Drawing.Brushes]::White, 191, 109, 5, 5)
# nos a pusa s jazykem
$g.FillEllipse($ink, 170, 126, 18, 12)
$mouth = New-Pen 40 25 15 3
$g.DrawArc($mouth, 167, 133, 12, 12, 0, 160); $g.DrawArc($mouth, 179, 133, 12, 12, 20, 160)
$g.FillEllipse((New-Brush 240 100 110), 174, 143, 10, 10)

# --- kurzor (velký, šikmo, špička vlevo nahoře) ---
$arrow = [System.Drawing.PointF[]]@(
    (Pt 34 30), (Pt 34 166), (Pt 66 136), (Pt 90 186), (Pt 114 175), (Pt 90 126), (Pt 134 124))
$g.FillPolygon([System.Drawing.Brushes]::White, $arrow)
$g.DrawPolygon((New-Pen 0 60 70 6), $arrow)

# nožky spojující tlapky s tělem
$leg = New-Pen 214 150 80 18
$g.DrawLine($leg, 122, 186, 146, 196); $g.DrawLine($leg, 126, 200, 150, 206)
# --- přední tlapky drží stopku kurzoru ---
foreach ($paw in @(@(76, 160, 34, 26), @(98, 176, 34, 26))) {
    $g.FillEllipse($fur, $paw[0], $paw[1], $paw[2], $paw[3])
    $g.DrawEllipse($outline, $paw[0], $paw[1], $paw[2], $paw[3])
    # polštářky prstů
    $toe = New-Pen 120 70 30 3
    $g.DrawLine($toe, $paw[0] + 11, $paw[1] + 5, $paw[0] + 11, $paw[1] + 11)
    $g.DrawLine($toe, $paw[0] + 19, $paw[1] + 4, $paw[0] + 19, $paw[1] + 10)
}

# --- vlnky vibrace vpravo nahoře ---
foreach ($i in 0..2) {
    $d = 44 + $i * 30
    $g.DrawArc((New-Pen 255 255 255 9 (255 - $i * 60)), 168 - $d / 2, 52 - $d / 2, $d, $d, -60, 75)
}

$g.Dispose()
$full = [System.IO.Path]::GetFullPath($Out)
$bmp.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Ikona uložena: $full"
