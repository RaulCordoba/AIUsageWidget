$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDir = Join-Path $root 'publish\win-x64'
$artifactDir = Join-Path $root 'artifacts\installer'
$stageDir = Join-Path $root 'artifacts\iexpress'
$zipPath = Join-Path $stageDir 'AIUsageWidget.zip'
$installerPath = Join-Path $artifactDir 'AIUsageWidget-Setup.exe'
$sedPath = Join-Path $stageDir 'AIUsageWidget.sed'
$installPs1 = Join-Path $stageDir 'install.ps1'
$installCmd = Join-Path $stageDir 'install.cmd'

$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Push-Location $root
try {
    dotnet publish src\AIUsageWidget.App\AIUsageWidget.App.csproj -c Release -r win-x64 --self-contained false -o publish\win-x64
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish fallo con el codigo $LASTEXITCODE. No se generara un instalador con archivos antiguos."
    }

    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
    if (Test-Path $stageDir) {
        Remove-Item $stageDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

    @'
$ErrorActionPreference = 'Stop'

$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$zipPath = Join-Path $packageRoot 'AIUsageWidget.zip'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\AIUsageWidget'
$exePath = Join-Path $installDir 'AIUsageWidget.App.exe'

if (Get-Process AIUsageWidget.App -ErrorAction SilentlyContinue) {
    Get-Process AIUsageWidget.App -ErrorAction SilentlyContinue | Stop-Process -Force
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Expand-Archive -Path $zipPath -DestinationPath $installDir -Force

$shell = New-Object -ComObject WScript.Shell
$programs = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programs 'AI Usage Widget.lnk'
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Save()

Start-Process -FilePath $exePath -WorkingDirectory $installDir
'@ | Set-Content -Path $installPs1 -Encoding UTF8

    @'
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
'@ | Set-Content -Path $installCmd -Encoding ASCII

    $installerTargetPath = Join-Path $stageDir 'AIUsageWidget-Setup.exe'
    $stageSourcePath = $stageDir + '\'
    $sedContents = @"
[Version]
Class=IEXPRESS
SEDVersion=3
[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=
DisplayLicense=
FinishMessage=AI Usage Widget instalado correctamente.
TargetName=$installerTargetPath
FriendlyName=AI Usage Widget
AppLaunched=install.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=install.cmd
UserQuietInstCmd=install.cmd
SourceFiles=SourceFiles
[Strings]
FILE0="AIUsageWidget.zip"
FILE1="install.ps1"
FILE2="install.cmd"
[SourceFiles]
SourceFiles0=$stageSourcePath
[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
"@
    # Write a Windows-format SED regardless of the checkout's line endings.
    ($sedContents -replace '\r?\n', "`r`n") | Set-Content -Path $sedPath -Encoding ASCII

    $iexpress = (Get-Command iexpress.exe -ErrorAction Stop).Source
    $packageStartedAt = Get-Date
    # Use the local filename: IExpress rejects a quoted SED argument.
    $packageProcess = Start-Process -FilePath $iexpress -ArgumentList @('/N', '/Q', (Split-Path -Leaf $sedPath)) -WorkingDirectory $stageDir -Wait -PassThru -WindowStyle Hidden
    if ($packageProcess.ExitCode -ne 0) {
        throw "IExpress fallo con el codigo $($packageProcess.ExitCode)."
    }

    if (-not (Test-Path $installerTargetPath)) {
        throw "No se genero el instalador en $installerTargetPath"
    }
    if ((Get-Item -LiteralPath $installerTargetPath).LastWriteTime -lt $packageStartedAt) {
        throw "IExpress no actualizo el instalador en $installerTargetPath"
    }
    Move-Item -LiteralPath $installerTargetPath -Destination $installerPath -Force

    Write-Host "Instalador generado: $installerPath"
}
finally {
    Pop-Location
}
