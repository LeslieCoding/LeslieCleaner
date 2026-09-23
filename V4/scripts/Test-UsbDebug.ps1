[CmdletBinding()]
param(
    [string]$AndroidSdkPath,
    [switch]$IncludeAllUsb,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

function Resolve-DebugTool {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $command = Get-Command "$Name.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = [System.Collections.Generic.List[string]]::new()
    $projectRoot = Split-Path -Parent $PSScriptRoot
    $candidates.Add((Join-Path $projectRoot "tools\android-platform-tools\platform-tools\$Name.exe"))
    $candidates.Add((Join-Path $projectRoot "tools\platform-tools\$Name.exe"))
    if ($AndroidSdkPath) {
        $candidates.Add((Join-Path $AndroidSdkPath "platform-tools\$Name.exe"))
        $candidates.Add((Join-Path $AndroidSdkPath "$Name.exe"))
    }

    if ($env:ANDROID_SDK_ROOT) {
        $candidates.Add((Join-Path $env:ANDROID_SDK_ROOT "platform-tools\$Name.exe"))
    }

    if ($env:ANDROID_HOME) {
        $candidates.Add((Join-Path $env:ANDROID_HOME "platform-tools\$Name.exe"))
    }

    $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ($localAppData) {
        $candidates.Add((Join-Path $localAppData "Android\Sdk\platform-tools\$Name.exe"))
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Invoke-DebugTool {
    param(
        [string]$Tool,
        [string[]]$Arguments
    )

    if (-not $Tool) {
        return @()
    }

    try {
        return @(& $Tool @Arguments 2>&1 | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ })
    }
    catch {
        return @("ERROR: $($_.Exception.Message)")
    }
}

$adb = Resolve-DebugTool -Name 'adb'
$fastboot = Resolve-DebugTool -Name 'fastboot'
$adbOutput = Invoke-DebugTool -Tool $adb -Arguments @('devices', '-l')
$fastbootOutput = Invoke-DebugTool -Tool $fastboot -Arguments @('devices')

$adbDevices = @($adbOutput | Where-Object {
    $_ -notmatch '^List of devices attached' -and
    $_ -notmatch '^\* daemon' -and
    $_ -notmatch '^adb server' -and
    $_ -notmatch '^ERROR:'
})

$adbReady = @($adbDevices | Where-Object { $_ -match '^\S+\s+device(?:\s|$)' })
$adbUnauthorized = @($adbDevices | Where-Object { $_ -match '^\S+\s+unauthorized(?:\s|$)' })
$adbOffline = @($adbDevices | Where-Object { $_ -match '^\S+\s+offline(?:\s|$)' })
$fastbootDevices = @($fastbootOutput | Where-Object {
    $_ -notmatch '^ERROR:' -and $_ -match '\S'
})

$serialPorts = @()
try {
    $serialPorts = @([System.IO.Ports.SerialPort]::GetPortNames() | Sort-Object)
}
catch {
    $serialPorts = @()
}

$usbDevices = @()
$pnpError = $null
if (Get-Command Get-PnpDevice -ErrorAction SilentlyContinue) {
    try {
        $usbDevices = @(Get-PnpDevice -PresentOnly -ErrorAction Stop |
            Where-Object {
                $_.InstanceId -like 'USB*' -or
                $_.Class -in @('AndroidUsbDeviceClass', 'Ports') -or
                $_.FriendlyName -match 'Android|ADB|Fastboot|USB Serial|USB Composite'
            } |
            Sort-Object Class, FriendlyName |
            ForEach-Object {
                [ordered]@{
                    Status = $_.Status
                    Class = $_.Class
                    Name = $_.FriendlyName
                    InstanceId = $_.InstanceId
                }
            })
    }
    catch {
        $pnpError = $_.Exception.Message
    }
}

$debugUsbDevices = @($usbDevices | Where-Object {
    $_.Class -in @('AndroidUsbDeviceClass', 'Ports') -or
    $_.Name -match 'Android|ADB|Fastboot|USB Serial'
})

$state = if ($adbReady.Count -gt 0) {
    'AdbReady'
}
elseif ($adbUnauthorized.Count -gt 0) {
    'AdbAuthorizationRequired'
}
elseif ($adbOffline.Count -gt 0) {
    'AdbOffline'
}
elseif ($fastbootDevices.Count -gt 0) {
    'FastbootDetected'
}
elseif ($serialPorts.Count -gt 0) {
    'SerialDetected'
}
elseif ($debugUsbDevices.Count -gt 0 -and -not $adb) {
    'DebugToolsMissing'
}
elseif ($debugUsbDevices.Count -gt 0) {
    'UsbDetected'
}
elseif (-not $adb -and -not $fastboot) {
    'DebugToolsMissing'
}
else {
    'NoDevice'
}

$nextAction = switch ($state) {
    'AdbReady' { 'ADB is ready for non-destructive board tests.' }
    'AdbAuthorizationRequired' { 'Unlock the board and accept its USB debugging RSA prompt.' }
    'AdbOffline' { 'Reconnect the data cable or restart ADB, then run this check again.' }
    'FastbootDetected' { 'The device is in Fastboot. Do not flash it until the test procedure requires it.' }
    'SerialDetected' { 'A serial port is present. Board model, baud rate and protocol are still required.' }
    'UsbDetected' { 'Windows sees USB hardware but no debug channel. Check the driver and board-side debug setting.' }
    'DebugToolsMissing' { 'Android platform-tools are missing. Install them first if this is an Android board.' }
    default { 'No board was found. Connect a data-capable USB cable and enable debugging on the board.' }
}

$report = [ordered]@{
    Timestamp = (Get-Date).ToString('o')
    State = $state
    NextAction = $nextAction
    AdbPath = $adb
    FastbootPath = $fastboot
    AdbDevices = $adbDevices
    FastbootDevices = $fastbootDevices
    SerialPorts = $serialPorts
    DebugUsbDevices = $debugUsbDevices
    UsbDevices = $usbDevices
    PnpEnumerationError = $pnpError
}

if ($AsJson) {
    $report | ConvertTo-Json -Depth 5
}
else {
    Write-Host "USB debug state: $state"
    Write-Host "Next action: $nextAction"
    Write-Host "ADB: $(if ($adb) { $adb } else { 'not found' })"
    Write-Host "Fastboot: $(if ($fastboot) { $fastboot } else { 'not found' })"

    if ($adbDevices.Count -gt 0) {
        Write-Host 'ADB devices:'
        $adbDevices | ForEach-Object { Write-Host "  $_" }
    }

    if ($fastbootDevices.Count -gt 0) {
        Write-Host 'Fastboot devices:'
        $fastbootDevices | ForEach-Object { Write-Host "  $_" }
    }

    if ($serialPorts.Count -gt 0) {
        Write-Host "Serial ports: $($serialPorts -join ', ')"
    }

    if ($debugUsbDevices.Count -gt 0) {
        Write-Host 'Debug-capable USB/PnP devices:'
        $debugUsbDevices | ForEach-Object {
            Write-Host "  [$($_.Status)] $($_.Class) - $($_.Name)"
        }
    }

    if ($IncludeAllUsb -and $usbDevices.Count -gt 0) {
        Write-Host 'All Windows USB/PnP devices:'
        $usbDevices | ForEach-Object {
            Write-Host "  [$($_.Status)] $($_.Class) - $($_.Name)"
        }
    }

    if ($pnpError) {
        Write-Host "PnP enumeration warning: $pnpError"
    }
}

if ($state -eq 'AdbReady') {
    exit 0
}

exit 2
