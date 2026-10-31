# Buduje instalator offline: publikacja self-contained + zweryfikowany PawnIO + Inno Setup.
# Podpisywanie (opcjonalne): -SignThumbprint <odcisk certyfikatu w magazynie CurrentUser\My lub LocalMachine\My>.
# Bez certyfikatu powstaje instalator oznaczony jako niepodpisany (nazwa pliku kończy się na -unsigned).
param(
    [string]$SignThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipBuild
)
. (Join-Path $PSScriptRoot 'common.ps1')
. (Join-Path $PSScriptRoot 'prerequisites.ps1')
$publish = Join-Path $RepoRoot 'artifacts/TempView-win-x64'
$prereq = Join-Path $RepoRoot 'artifacts/prerequisites'
$output = Join-Path $RepoRoot 'artifacts/installer'

if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
foreach ($file in 'TempView.exe', 'TempView.Sensors.exe') {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $file))) { throw "Brak $file w $publish. Uruchom ./scripts/build.ps1." }
}

# Zależności: pobierane podczas budowania z kanonicznych źródeł i weryfikowane (SHA-256 + wydawca). Instalator ich nie pobiera.
foreach ($name in 'PawnIO', 'PawnIOSource', 'PawnPPSource') { Get-VerifiedFile $Prerequisites[$name] $prereq | Out-Null }

$iscc = Join-Path $env:LOCALAPPDATA "TempViewTools/InnoSetup-$($Prerequisites.InnoSetup.Version)/ISCC.exe"
if (-not (Test-Path -LiteralPath $iscc)) { & (Join-Path $PSScriptRoot 'setup-installer-tools.ps1') }

$version = ((Get-Item (Join-Path $publish 'TempView.exe')).VersionInfo.FileVersion -split '\.')[0..2] -join '.'
$signed = [bool]$SignThumbprint
$name = "TempView-Setup-$version-x64" + $(if ($signed) { '' } else { '-unsigned' })
$isccArgs = @("/DAppVersion=$version", "/DPublishDir=$publish", "/DPrereqDir=$prereq", "/DPawnIOVersion=$($Prerequisites.PawnIO.Version)",
    "/DPawnIOSha256=$($Prerequisites.PawnIO.Sha256)", "/DOutputName=$name", '/Q')

if ($signed) {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
    if (-not $signtool) { throw 'Brak signtool.exe (Windows SDK).' }
    $sign = @('sign', '/sha1', $SignThumbprint, '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/d', 'TempView')
    # Własne pliki TempView; biblioteki firm trzecich zachowują podpisy wydawców.
    $own = Get-ChildItem $publish -File | Where-Object { $_.Name -like 'TempView*.exe' -or $_.Name -like 'TempView*.dll' }
    & $signtool.FullName @sign $own.FullName
    if ($LASTEXITCODE -ne 0) { throw "Podpisywanie plików TempView nie powiodło się ($LASTEXITCODE)." }
    $isccArgs += '/DSign', "/Stvsign=`"$($signtool.FullName)`" $($sign -join ' ') `$f"
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
& $iscc @isccArgs (Join-Path $RepoRoot 'installer/TempView.iss')
if ($LASTEXITCODE -ne 0) { throw "Kompilacja instalatora nie powiodła się ($LASTEXITCODE)." }
$installer = Join-Path $output "$name.exe"
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
Set-Content -LiteralPath "$installer.sha256" -Value "$hash  $name.exe" -Encoding ascii

# Inwentarz składników (wersje, licencje, sumy kontrolne i status podpisu).
$deps = Get-Content -Raw (Join-Path $publish 'TempView.deps.json') | ConvertFrom-Json
$runtime = ($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'runtimepack.Microsoft.WindowsDesktop.App.Runtime.*' }) -replace '^.*/', ''
$packages = foreach ($project in 'src/TempView/TempView.csproj', 'src/TempView.Sensors/TempView.Sensors.csproj') {
    $list = & $Dotnet list (Join-Path $RepoRoot $project) package --include-transitive --format json | ConvertFrom-Json
    foreach ($framework in $list.projects.frameworks) { foreach ($p in @($framework.topLevelPackages) + @($framework.transitivePackages)) { if ($p) { "$($p.id) $($p.resolvedVersion)" } } }
}
$commit = (git -C $RepoRoot rev-parse HEAD).Trim()
$dirty = [bool](git -C $RepoRoot status --porcelain)
$inventory = [ordered]@{
    installer = [ordered]@{ file = "$name.exe"; sha256 = $hash; signed = $signed; signer = $(if ($signed) { $SignThumbprint } else { $null }); toolchain = "Inno Setup $($Prerequisites.InnoSetup.Version)" }
    tempview = [ordered]@{ version = $version; commit = $commit; uncommittedChanges = $dirty; license = 'MIT' }
    dotnetRuntime = [ordered]@{ version = $runtime; deployment = 'self-contained win-x64'; license = 'MIT' }
    pawnio = [ordered]@{ version = $Prerequisites.PawnIO.Version; file = $Prerequisites.PawnIO.File; sha256 = $Prerequisites.PawnIO.Sha256; publisher = $Prerequisites.PawnIO.Signer; source = $Prerequisites.PawnIO.Url; license = 'GPL-2.0-or-later with IOCTL exception'; correspondingSource = @($Prerequisites.PawnIOSource.File, $Prerequisites.PawnPPSource.File) }
    nugetPackages = @($packages | Sort-Object -Unique)
}
$inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output "$name.inventory.json") -Encoding utf8
Write-Host "Instalator: $installer"
Write-Host "SHA-256:    $hash"
Write-Host "Podpis:     $(if ($signed) { 'podpisany (' + $SignThumbprint + ')' } else { 'NIEPODPISANY - tylko do testów, nie do publicznej dystrybucji' })"
