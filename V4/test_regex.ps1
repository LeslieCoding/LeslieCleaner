$pattern = '^\s*(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\.\d{3}(?:\s+[+-]\d{2}:\d{2})?)\s+\[(?<level>[^\]]+)\]\s+\[(?<kind>[^\]]+)\]\s+(?<message>.*)$'
$ts = [DateTimeOffset]::Now.ToString('yyyy-MM-dd HH:mm:ss.fff zzz')

$lines = @(
    "$ts [INF] [ScanStart] Scan started",
    "$ts [WRN] [DeleteStart] Delete started",
    "$ts [ERR] [Exception] Error A",
    "System.IO.IOException: Simulated IO failure",
    "$ts [ERR] [Exception] Error B",
    "System.IO.IOException: Simulated IO failure again",
    "$ts [ERR] [Exception] Error C",
    "System.UnauthorizedAccessException: Simulated access failure"
)

foreach ($line in $lines) {
    if ($line -match $pattern) {
        Write-Host "MATCHED | $line"
        # In PowerShell, the automatic variable $Matches contains named groups
        Write-Host "  timestamp=$($Matches['timestamp'])"
        Write-Host "  level=$($Matches['level'])"
        Write-Host "  kind=$($Matches['kind'])"
        Write-Host "  message=$($Matches['message'])"
    } else {
        Write-Host "FAILED  | $line"
    }
}
