<#
.SYNOPSIS
    Vytvoří (nebo smaže) zástupce „MX Haptics – nastavení“ v nabídce Start aktuálního uživatele.
.EXAMPLE
    pwsh tools/Install-StartMenuShortcut.ps1
    pwsh tools/Install-StartMenuShortcut.ps1 -Remove
#>
param(
    [string]$Target = (Join-Path $PSScriptRoot '..\build\settings\MxHapticsSettings.exe'),
    [switch]$Remove
)

$programs = [Environment]::GetFolderPath('Programs')
$link = Join-Path $programs 'MX Haptics – nastavení.lnk'

if ($Remove) {
    if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link; Write-Host "Zástupce smazán: $link" }
    else { Write-Host "Zástupce neexistuje: $link" }
    return
}

$exe = [System.IO.Path]::GetFullPath($Target)
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Okno nastavení nenalezeno: $exe. Nejdřív spusť 'dotnet build src/MxHapticsPlugin.csproj'."
}

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($link)
$sc.TargetPath = $exe
$sc.WorkingDirectory = Split-Path $exe
$sc.Description = 'Nastavení hmatové odezvy myši MX Master 4 (plugin MX Haptics pro Logi Options+)'
$sc.IconLocation = "$exe,0"
$sc.Save()
Write-Host "Zástupce vytvořen: $link -> $exe"
