<#
.SYNOPSIS
    Creates (or removes) the "MX Haptics Settings" shortcut in the current user's Start menu.
.EXAMPLE
    pwsh tools/Install-StartMenuShortcut.ps1
    pwsh tools/Install-StartMenuShortcut.ps1 -Target "$env:LOCALAPPDATA\Logi\LogiPluginService\Plugins\MxHaptics\settings\MxHapticsSettings.exe"
    pwsh tools/Install-StartMenuShortcut.ps1 -Remove
#>
param(
    [string]$Target = (Join-Path $PSScriptRoot '..\build\settings\MxHapticsSettings.exe'),
    [switch]$Remove
)

$programs = [Environment]::GetFolderPath('Programs')
$link = Join-Path $programs 'MX Haptics Settings.lnk'
# shortcut name used up to version 0.5.0
$legacy = Join-Path $programs 'MX Haptics – nastavení.lnk'

if (Test-Path -LiteralPath $legacy) { Remove-Item -LiteralPath $legacy; Write-Host "Removed old shortcut: $legacy" }

if ($Remove) {
    if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link; Write-Host "Shortcut removed: $link" }
    else { Write-Host "Shortcut does not exist: $link" }
    return
}

$exe = [System.IO.Path]::GetFullPath($Target)
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Settings window not found: $exe. Run 'dotnet build src/MxHapticsPlugin.csproj' first, or pass -Target."
}

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($link)
$sc.TargetPath = $exe
$sc.WorkingDirectory = Split-Path $exe
$sc.Description = 'Haptic feedback settings for the MX Master 4 (MX Haptics plugin for Logi Options+)'
$sc.IconLocation = "$exe,0"
$sc.Save()
Write-Host "Shortcut created: $link -> $exe"
