[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testProject = Join-Path $projectRoot 'src\LeslieCleaner.Tests\LeslieCleaner.Tests.csproj'
$resultDirectory = Join-Path $projectRoot 'artifacts\test-results'

function Write-ProbeResult {
    param(
        [string]$Status,
        [bool]$Elevated,
        [string]$Message,
        [int]$ExitCode,
        [string]$ResultFile = ''
    )

    $result = [pscustomobject]@{
        Status = $Status
        Elevated = $Elevated
        Message = $Message
        ResultFile = $ResultFile
        SafetyBoundary = 'Only a random HKLM\SOFTWARE\LeslieCleaner.PrivilegedAcceptance.Tests subtree is created and removed.'
    }
    if ($AsJson) {
        $result | ConvertTo-Json -Depth 3 -Compress
    }
    else {
        $result | Format-List
    }
    exit $ExitCode
}

$runningOnWindows = [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT
if (-not $runningOnWindows -or -not (Test-Path -LiteralPath $testProject)) {
    Write-ProbeResult -Status 'UnsupportedEnvironment' -Elevated $false `
        -Message 'Windows and the LeslieCleaner V4 test project are required.' -ExitCode 3
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$isElevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$identity.Dispose()
if (-not $isElevated) {
    Write-ProbeResult -Status 'NeedsElevation' -Elevated $false `
        -Message 'Open an elevated PowerShell terminal and run this script again. No registry write was attempted.' `
        -ExitCode 2
}

New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$resultFileName = "v4-registry-privileged-$((Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')).trx"
$resultFile = Join-Path $resultDirectory $resultFileName
$previousGate = [Environment]::GetEnvironmentVariable('LESLIECLEANER_RUN_PRIVILEGED_ACCEPTANCE', 'Process')
try {
    [Environment]::SetEnvironmentVariable('LESLIECLEANER_RUN_PRIVILEGED_ACCEPTANCE', '1', 'Process')
    & dotnet test $testProject -c $Configuration --filter 'Category=Privileged' --no-restore `
        --logger "trx;LogFileName=$resultFileName" --results-directory $resultDirectory
    $testExitCode = $LASTEXITCODE
}
finally {
    [Environment]::SetEnvironmentVariable(
        'LESLIECLEANER_RUN_PRIVILEGED_ACCEPTANCE', $previousGate, 'Process')
}

if ($testExitCode -ne 0) {
    Write-ProbeResult -Status 'Failed' -Elevated $true `
        -Message 'Privileged registry acceptance failed. Inspect the TRX; no success is claimed.' `
        -ResultFile $resultFile -ExitCode $testExitCode
}

Write-ProbeResult -Status 'Passed' -Elevated $true `
    -Message 'HKLM 32/64-bit backup, exact-scope deletion, readback, and restore completed.' `
    -ResultFile $resultFile -ExitCode 0
