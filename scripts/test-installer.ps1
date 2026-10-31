# Testy instalatora, które niczego nie instalują: weryfikacja zależności (w tym podmienionych plików),
# logika decyzji PawnIO (Inno Setup) i spójność zbudowanego instalatora. Uruchom po build-installer.ps1.
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'prerequisites.ps1')
$failures = 0
function Assert([bool]$Condition, [string]$Name) {
    if ($Condition) { Write-Host "PASS $Name" } else { Write-Host "FAIL $Name"; $script:failures++ }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('tempview-installer-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    # 1. Zależności: poprawny plik przechodzi, zmieniony bajt i obcy wydawca są odrzucane.
    $original = Join-Path $RepoRoot 'artifacts/prerequisites/PawnIO_setup.exe'
    $spec = $Prerequisites.PawnIO
    Assert ($null -eq (Test-VerifiedFile $original $spec)) 'verified PawnIO payload accepted'
    $tampered = Join-Path $work 'PawnIO_setup.exe'
    Copy-Item -LiteralPath $original -Destination $tampered
    $bytes = [IO.File]::ReadAllBytes($tampered); $bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 0xFF; [IO.File]::WriteAllBytes($tampered, $bytes)
    Assert ((Test-VerifiedFile $tampered $spec) -match 'SHA-256') 'tampered PawnIO payload rejected'
    $foreign = $spec.Clone(); $foreign.Signer = 'CN=Someone Else'
    Assert ((Test-VerifiedFile $original $foreign) -match 'wydawca') 'unexpected publisher rejected'
    Assert ((Test-VerifiedFile (Join-Path $work 'missing.exe') $spec) -match 'Brak') 'missing payload rejected'

    # 2. Logika decyzji instalatora (instalator testowy kończy się przed instalacją).
    $iscc = Join-Path $env:LOCALAPPDATA "TempViewTools/InnoSetup-$($Prerequisites.InnoSetup.Version)/ISCC.exe"
    & $iscc /Q "/O$work" (Join-Path $RepoRoot 'installer/tests/DecisionTests.iss')
    Assert ($LASTEXITCODE -eq 0) 'decision tests compiled'
    $results = Join-Path $work 'results.txt'
    Start-Process (Join-Path $work 'DecisionTests.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', "/OUT=`"$results`"" -Wait | Out-Null
    $lines = Get-Content -LiteralPath $results -Encoding UTF8
    $lines | Where-Object { $_ -like 'FAIL *' } | ForEach-Object { Write-Host $_ }
    Assert (($lines -contains 'FAILURES 0') -and @($lines | Where-Object { $_ -like 'PASS *' }).Count -ge 17) 'installer decision logic'

    # 3. Zamykanie działającego TempView przez instalator (sygnał zakończenia), na zbudowanej paczce.
    & $iscc /Q "/O$work" (Join-Path $RepoRoot 'installer/tests/CloseTests.iss')
    Assert ($LASTEXITCODE -eq 0) 'close test compiled'
    $app = Start-Process (Join-Path $RepoRoot 'artifacts/TempView-win-x64/TempView.exe') -ArgumentList '--settings-file', "`"$(Join-Path $work 'settings.json')`"", '--diagnostics', "`"$(Join-Path $work 'diag.json')`"" -PassThru
    Start-Sleep -Seconds 6
    $closeResults = Join-Path $work 'close.txt'
    Start-Process (Join-Path $work 'CloseTests.exe') -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', "/OUT=`"$closeResults`"" -Wait | Out-Null
    $close = Get-Content -LiteralPath $closeResults -Encoding UTF8
    Assert ($close -contains 'RUNNING_BEFORE 1') 'installer detects running TempView'
    Assert (($close -contains 'CLOSED 1') -and $app.WaitForExit(5000)) 'installer closes running TempView gracefully'
    if (-not $app.HasExited) { Stop-Process -Id $app.Id -Force }

    # 4. Zbudowany instalator: suma kontrolna zgodna, nigdy wydanie unrestricted.
    $installer = Get-ChildItem (Join-Path $RepoRoot 'artifacts/installer') -Filter 'TempView-Setup-*.exe' | Sort-Object LastWriteTime | Select-Object -Last 1
    Assert ($null -ne $installer) 'installer built'
    if ($installer) {
        $recorded = ((Get-Content -LiteralPath "$($installer.FullName).sha256") -split '\s+')[0]
        Assert ($recorded -eq (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash) 'installer SHA-256 file matches'
    }
    $script = Get-Content -Raw (Join-Path $RepoRoot 'installer/TempView.iss')
    Assert ($script -notmatch '-unrestricted' -or $script -match 'never -unrestricted') 'unrestricted PawnIO edition never requested'
    Assert ($script -match "'-install -silent'") 'documented PawnIO silent install arguments'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
if ($failures -gt 0) { throw "$failures test(s) failed." }
Write-Host 'Installer tests passed.'
