[CmdletBinding()]
param(
    [switch]$SkipSoak
)

$ErrorActionPreference = 'Stop'
$v4Root = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $v4Root
$dotnet = Join-Path $repositoryRoot '.dotnet-sdk\dotnet.exe'
$results = Join-Path $v4Root 'artifacts\test-results'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw "Portable .NET SDK was not found: $dotnet"
}

$userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
$bundledPython = Join-Path $userProfile '.cache\codex-runtimes\codex-primary-runtime\dependencies\python'
if (Test-Path -LiteralPath $bundledPython -PathType Container) {
    $env:PATH = "$bundledPython;$env:PATH"
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_HOST_PATH = $dotnet
New-Item -ItemType Directory -Path $results -Force | Out-Null

function Invoke-DotNetStep {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet step failed with exit code $LASTEXITCODE: $($Arguments -join ' ')"
    }
}

Invoke-DotNetStep @(
    'build', (Join-Path $v4Root 'LeslieCleaner.V4.sln'),
    '-c', 'Release', '--no-restore', '-p:GenerateTargetFrameworkAttribute=false'
)

Invoke-DotNetStep @(
    'test', (Join-Path $v4Root 'src\LeslieCleaner.Tests\LeslieCleaner.Tests.csproj'),
    '-c', 'Release', '--no-build', '--filter', 'Category!=LongRunning&Category!=Soak',
    '--logger', "trx;LogFileName=v4-$stamp-round1-main.trx", '--results-directory', $results
)

Invoke-DotNetStep @(
    'test', (Join-Path $v4Root 'src\LeslieCleaner.PluginHost.IntegrationTests\LeslieCleaner.PluginHost.IntegrationTests.csproj'),
    '-c', 'Release', '--no-build',
    '--logger', "trx;LogFileName=v4-$stamp-round1-pluginhost.trx", '--results-directory', $results
)

Invoke-DotNetStep @(
    'test', (Join-Path $v4Root 'src\LeslieCleaner.Tests\LeslieCleaner.Tests.csproj'),
    '-c', 'Release', '--no-build', '--filter', 'Category=LongRunning',
    '--logger', "trx;LogFileName=v4-$stamp-round2-longrunning.trx", '--results-directory', $results
)

if (-not $SkipSoak) {
    Invoke-DotNetStep @(
        'test', (Join-Path $v4Root 'src\LeslieCleaner.Tests\LeslieCleaner.Tests.csproj'),
        '-c', 'Release', '--no-build', '--filter', 'Category=Soak',
        '--logger', "trx;LogFileName=v4-$stamp-round2-soak.trx", '--results-directory', $results
    )
}

Write-Host "V4 two-round verification completed. Results: $results"
