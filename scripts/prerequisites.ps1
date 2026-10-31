# Pobieranie i weryfikacja zależności budowania. Plik jest dołączany (dot-source) przez inne skrypty.
$Prerequisites = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'prerequisites.psd1')

# Zwraca opis problemu albo $null, gdy plik ma przypięty SHA-256 i (jeśli wymagany) ważny podpis oczekiwanego wydawcy.
function Test-VerifiedFile([string]$Path, [hashtable]$Spec) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return "Brak pliku $Path." }
    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    if ($hash -ne $Spec.Sha256) { return "Niezgodny SHA-256 pliku $($Spec.File): $hash, oczekiwano $($Spec.Sha256)." }
    if ($Spec.Signer) {
        $signature = Get-AuthenticodeSignature -LiteralPath $Path
        if ($signature.Status -ne 'Valid') { return "Nieprawidłowy podpis $($Spec.File): $($signature.Status)." }
        # Porównanie bez adresu e-mail: niektórzy wydawcy umieszczają go w podmiocie (E=...).
        $subject = ($signature.SignerCertificate.Subject -split ',\s*' | Where-Object { $_ -notmatch '^E=' }) -join ', '
        if ($subject -ne $Spec.Signer) { return "Nieoczekiwany wydawca $($Spec.File): $subject." }
    }
    return $null
}

# Pobiera plik do pustego katalogu docelowego (lub używa już zweryfikowanej kopii) i przerywa przy każdej niezgodności.
function Get-VerifiedFile([hashtable]$Spec, [string]$Directory) {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    $target = Join-Path $Directory $Spec.File
    if ((Test-Path -LiteralPath $target) -and -not (Test-VerifiedFile $target $Spec)) { return $target }
    $temporary = "$target.$([Guid]::NewGuid().ToString('N')).download"
    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $Spec.Url -OutFile $temporary -UseBasicParsing
        $problem = Test-VerifiedFile $temporary $Spec
        if ($problem) { throw $problem }
        Move-Item -LiteralPath $temporary -Destination $target -Force
        return $target
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
    }
}
