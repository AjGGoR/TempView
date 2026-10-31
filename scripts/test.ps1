param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location $RepoRoot
try {
    Invoke-Dotnet test 'tests/TempView.Tests/TempView.Tests.csproj' -c $Configuration --logger 'trx;LogFileName=TempView.trx' --results-directory 'TestResults'
} finally { Pop-Location }
