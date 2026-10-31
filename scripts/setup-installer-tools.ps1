# Instaluje Inno Setup w profilu użytkownika (bez uprawnień administratora, bez wpisów w systemie).
. (Join-Path $PSScriptRoot 'prerequisites.ps1')
$ErrorActionPreference = 'Stop'
$spec = $Prerequisites.InnoSetup
$target = Join-Path $env:LOCALAPPDATA "TempViewTools/InnoSetup-$($spec.Version)"
if (Test-Path -LiteralPath (Join-Path $target 'ISCC.exe')) { Write-Host "Inno Setup $($spec.Version): $target"; return }
$downloads = Join-Path ([IO.Path]::GetTempPath()) ('tempview-tools-' + [Guid]::NewGuid().ToString('N'))
try {
    $installer = Get-VerifiedFile $spec $downloads
    # /PORTABLE=1: rozpakowanie bez rejestracji w systemie; /CURRENTUSER: bez UAC.
    $process = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', '/PORTABLE=1', "/DIR=`"$target`"" -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $target 'ISCC.exe'))) { throw "Instalacja Inno Setup nie powiodła się (kod $($process.ExitCode))." }
    Write-Host "Inno Setup $($spec.Version): $target"
} finally {
    if (Test-Path -LiteralPath $downloads) { Remove-Item -LiteralPath $downloads -Recurse -Force }
}
