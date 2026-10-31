# Pobiera wyłącznie na żądanie; NIE instaluje sterownika i NIE podnosi uprawnień.
param([string]$Destination = (Join-Path $env:USERPROFILE 'Downloads/TempView-PawnIO-2.1.0-setup.exe'))
$ErrorActionPreference = 'Stop'
$expectedHash = 'A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0'
$temporary = $Destination + '.' + [Guid]::NewGuid().ToString('N') + '.exe'
try {
    Invoke-WebRequest 'https://raw.githubusercontent.com/LibreHardwareMonitor/LibreHardwareMonitor/v0.9.6/LibreHardwareMonitor/Resources/PawnIO_setup.exe' -OutFile $temporary
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Instalator PawnIO ma nieoczekiwany SHA256.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $temporary
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(?i)CN=namazso\.eu(?:,|$)') { throw "Nieprawidłowy podpis instalatora: $($signature.Status), $($signature.SignerCertificate.Subject)" }
    Move-Item -LiteralPath $temporary -Destination $Destination -Force
    Write-Host "Pobrano i zweryfikowano: $Destination"
    Write-Host 'Instalator NIE został uruchomiony. Przeczytaj docs/DRIVER.md przed instalacją.'
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
