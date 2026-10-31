# Uruchamia scenariusze instalatora w jednorazowej Piaskownicy systemu Windows (bez sieci).
# Wymaga włączonej funkcji „Piaskownica systemu Windows”. Niczego nie instaluje na komputerze-gospodarzu.
. (Join-Path $PSScriptRoot 'common.ps1')
$sandbox = Join-Path $env:windir 'System32\WindowsSandbox.exe'
if (-not (Test-Path -LiteralPath $sandbox)) {
    throw 'Piaskownica systemu Windows nie jest włączona. Włącz ją w „Włącz lub wyłącz funkcje systemu Windows” (wymaga administratora i restartu) albo użyj maszyny wirtualnej — docs/INSTALLER-TESTS.md.'
}
$installer = Join-Path $RepoRoot 'artifacts/installer'
if (-not (Get-ChildItem $installer -Filter 'TempView-Setup-*.exe' -ErrorAction SilentlyContinue)) { throw 'Brak instalatora. Uruchom ./scripts/build-installer.ps1.' }
$work = Join-Path $RepoRoot 'artifacts/sandbox'
$results = Join-Path $work 'results'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$tests = Join-Path $RepoRoot 'installer/tests/sandbox'
$config = @"
<Configuration>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder><HostFolder>$installer</HostFolder><SandboxFolder>C:\TempViewTest\installer</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$tests</HostFolder><SandboxFolder>C:\TempViewTest\scripts</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$results</HostFolder><SandboxFolder>C:\TempViewTest\results</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand><Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\TempViewTest\scripts\Run-Scenarios.ps1</Command></LogonCommand>
</Configuration>
"@
$wsb = Join-Path $work 'TempView-installer-tests.wsb'
Set-Content -LiteralPath $wsb -Value $config -Encoding utf8
Start-Process -FilePath $wsb
Write-Host "Piaskownica uruchomiona. Wyniki: $results\report.txt (oraz dzienniki instalatora)."
