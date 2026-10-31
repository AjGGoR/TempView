$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $env:LOCALAPPDATA 'TempViewTools/dotnet/dotnet.exe'
$command = Get-Command dotnet -ErrorAction SilentlyContinue
$Dotnet = if ($command) { $command.Source } elseif (Test-Path $localDotnet) { $localDotnet } else { throw 'Brak .NET SDK. Uruchom ./scripts/setup.ps1.' }
function Invoke-Dotnet {
    & $Dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet zakończył się kodem $LASTEXITCODE" }
}
