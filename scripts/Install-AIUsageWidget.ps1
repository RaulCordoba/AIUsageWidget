param(
    [string]$InstallDir = "$env:LOCALAPPDATA\Programs\AIUsageWidget",
    [switch]$DesktopShortcut,
    [switch]$StartWithWindows
)

$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot '..\publish\win-x64'
$exeName = 'AIUsageWidget.App.exe'
$exePath = Join-Path $InstallDir $exeName

if (-not (Test-Path (Join-Path $source $exeName))) {
    throw "No se encontro la publicacion en $source. Ejecuta dotnet publish antes de instalar."
}

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $InstallDir -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$programs = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programs 'AI Usage Widget.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $InstallDir
$shortcut.Save()

if ($DesktopShortcut) {
    $desktop = [Environment]::GetFolderPath('DesktopDirectory')
    $desktopShortcut = $shell.CreateShortcut((Join-Path $desktop 'AI Usage Widget.lnk'))
    $desktopShortcut.TargetPath = $exePath
    $desktopShortcut.WorkingDirectory = $InstallDir
    $desktopShortcut.Save()
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if ($StartWithWindows) {
    New-ItemProperty -Path $runKey -Name 'AIUsageWidget' -Value "`"$exePath`"" -PropertyType String -Force | Out-Null
}

Write-Host "AI Usage Widget instalado en $InstallDir"
Write-Host "Ejecutable: $exePath"
