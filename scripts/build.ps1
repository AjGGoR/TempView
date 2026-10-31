param([ValidateSet('win-x64')][string]$Runtime = 'win-x64')
. (Join-Path $PSScriptRoot 'common.ps1')
$output = Join-Path $RepoRoot "artifacts/TempView-$Runtime"
Push-Location $RepoRoot
try {
    # Usuń wyłącznie zweryfikowany katalog wynikowy tego projektu, aby nie pakować starych bibliotek.
    $artifactsRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    $resolvedOutput = [IO.Path]::GetFullPath($output)
    if (-not $resolvedOutput.StartsWith($artifactsRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Nieprawidłowy katalog publikacji.' }
    $existingExe = Join-Path $resolvedOutput 'TempView.exe'
    if (Test-Path -LiteralPath $existingExe) {
        try { $probe = [IO.File]::Open($existingExe, 'Open', 'ReadWrite', 'None'); $probe.Dispose() }
        catch { throw 'Zamknij działający TempView przed budowaniem. Pliki paczki pozostawiono bez zmian.' }
    }
    if (Test-Path -LiteralPath $resolvedOutput) { Remove-Item -LiteralPath $resolvedOutput -Recurse -Force }
    # Nie używaj single-file: biblioteki natywne i sterowniki muszą pozostać dostępne.
    Invoke-Dotnet publish 'src/TempView/TempView.csproj' -c Release -r $Runtime --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $output
    # Usługa czujników CPU w tym samym katalogu: wspólny runtime i biblioteki tej samej wersji.
    Invoke-Dotnet publish 'src/TempView.Sensors/TempView.Sensors.csproj' -c Release -r $Runtime --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $output
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'README.md') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs/licenses') -Destination $output -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs') -Destination $output -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'docs/DRIVER.md') -Destination $output -Force
    # Źródła modułów LGPL osadzonych przez LibreHardwareMonitor 0.9.6.
    $sources = Join-Path $output 'licenses/PawnIO.Modules-0.1.6-source.zip'
    Invoke-WebRequest 'https://codeload.github.com/namazso/PawnIO.Modules/zip/refs/tags/0.1.6' -OutFile $sources
    if ((Get-FileHash -LiteralPath $sources -Algorithm SHA256).Hash -ne '665B2F7A028F8A317BC35CD6FBF64986115909239019FF2D30D19260D7DD0982') { throw 'Niezgodny hash źródeł PawnIO.Modules.' }
    $zip = "$output.zip"
    Compress-Archive -Path (Join-Path $output '*') -DestinationPath $zip -Force
    Write-Host "EXE: $(Join-Path $output 'TempView.exe')"
    Write-Host "ZIP: $zip"
} finally { Pop-Location }
