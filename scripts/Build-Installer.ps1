$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$env:DOTNET_CLI_HOME = Join-Path $root '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Push-Location $root
try {
    dotnet publish src\AIUsageWidget.App\AIUsageWidget.App.csproj -c Release -r win-x64 --self-contained false -o publish\win-x64
    New-Item -ItemType Directory -Path artifacts\installer -Force | Out-Null

    $iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if (-not $iscc) {
        throw 'No se encontro ISCC.exe. Instala Inno Setup y vuelve a ejecutar este script.'
    }

    & $iscc.Source installer\AIUsageWidget.iss
}
finally {
    Pop-Location
}
