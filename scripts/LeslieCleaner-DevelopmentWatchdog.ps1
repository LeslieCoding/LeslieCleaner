[CmdletBinding()]
param(
    [int]$IntervalSeconds = 600,
    [string]$ProjectRoot = 'D:\SoftWare\LeslieCleaner',
    [string]$FixedDevice = '92f73a16',
    [switch]$RunOnce
)

$ErrorActionPreference = 'Continue'
$logDir = Join-Path $ProjectRoot 'logs'
$logPath = Join-Path $logDir 'DEVELOPMENT_WATCHDOG.log'
$requestPath = Join-Path $logDir 'CONTINUE.request'
$deviceGate = Join-Path $ProjectRoot 'Android_App\DEVICE_TEST_GATE.PASSED'
$validator = Join-Path $ProjectRoot 'Android_App\tools\Validate-AndroidBaseline.ps1'
$adb = 'D:\SoftWare\LeslieMusic\_dev\android-sdk\platform-tools\adb.exe'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

function Write-Heartbeat([string]$state, [string]$detail) {
    $line = '[{0}] state={1}; detail={2}' -f (Get-Date).ToString('o'), $state, $detail
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
}

function Invoke-Check {
    if (Test-Path -LiteralPath $validator) {
        try {
            $result = & $validator 2>&1 | Out-String
            Write-Heartbeat 'baseline-ok' ($result.Trim() -replace '\s+', ' ')
        } catch {
            Write-Heartbeat 'baseline-failed' $_.Exception.Message
        }
    } else {
        Write-Heartbeat 'baseline-missing' $validator
    }

    if ((Test-Path -LiteralPath $deviceGate) -and (Test-Path -LiteralPath $adb)) {
        $state = (& $adb -s $FixedDevice get-state 2>&1 | Out-String).Trim()
        if ($state -eq 'device') {
            Write-Heartbeat 'device-online' "fixedSerial=$FixedDevice"
        } else {
            Write-Heartbeat 'device-offline' "fixedSerial=$FixedDevice state=$state"
        }
    } elseif (-not (Test-Path -LiteralPath $deviceGate)) {
        Write-Heartbeat 'device-gated' 'ADB check deferred until Android_App/DEVICE_TEST_GATE.PASSED exists'
    } else {
        Write-Heartbeat 'adb-missing' $adb
    }

    Set-Content -LiteralPath $requestPath -Value ('CONTINUE ' + (Get-Date).ToString('o')) -Encoding UTF8
}

Write-Heartbeat 'watchdog-started' "intervalSeconds=$IntervalSeconds fixedSerial=$FixedDevice"
do {
    Invoke-Check
    if ($RunOnce) { break }
    Start-Sleep -Seconds $IntervalSeconds
} while ($true)
Write-Heartbeat 'watchdog-stopped' 'normal exit'
