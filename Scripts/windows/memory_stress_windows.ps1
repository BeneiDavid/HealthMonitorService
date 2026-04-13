param(
    [string]$TestRunId = "run_001",
    [int]$Duration = 60,
    [int]$Mb = 512
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }

function Log-Event {
    param(
        [string]$EventType,
        [string]$Details = ''
    )
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Add-Content -Path $EventsFile -Value "$timestamp,$TestRunId,$EventType,$Details"
}

Log-Event 'stress_memory_start' "duration=$Duration memory=${Mb}MB"

# Allocate and touch the pages to make the pressure real.
$blocks = New-Object 'System.Collections.Generic.List[byte[]]'
try {
    for ($i = 0; $i -lt $Mb; $i++) {
        $arr = New-Object byte[] (1MB)
        $arr[0] = 1
        $blocks.Add($arr)
    }

    Start-Sleep -Seconds $Duration
}
finally {
    $blocks.Clear()
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Log-Event 'stress_memory_stop' 'completed'
}
