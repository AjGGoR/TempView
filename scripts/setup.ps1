# Instalacja SDK wyłącznie w profilu użytkownika; bez zmiany polityki zabezpieczeń.
$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'TempViewTools/dotnet'
$installer = Join-Path ([IO.Path]::GetTempPath()) ('tempview-dotnet-' + [Guid]::NewGuid().ToString('N') + '.ps1')
try {
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & $installer -Channel '10.0' -InstallDir $installDir
    # Skrypt .ps1 nie ustawia wiarygodnie $LASTEXITCODE; sprawdź wynik instalacji.
    if (-not (Test-Path -LiteralPath (Join-Path $installDir 'dotnet.exe'))) { throw 'Instalacja SDK nie powiodła się.' }
} finally {
    if (Test-Path $installer) { Remove-Item -LiteralPath $installer }
}
. (Join-Path $PSScriptRoot 'common.ps1')
Invoke-Dotnet --info
Write-Host 'SDK gotowe. Następnie uruchom ./scripts/test.ps1 i ./scripts/build.ps1.'
