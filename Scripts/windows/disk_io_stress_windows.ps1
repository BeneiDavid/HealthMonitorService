param(
    [string]$TestRunId = "run_001",
    [int]$Duration = 60,
    [string]$TargetDir = "$env:TEMP\thesis_stress",
    [int]$FileMb = 256
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

function Write-RandomFile {
    param(
        [string]$Path,
        [int]$SizeMb
    )

    $buffer = New-Object byte[] (1MB)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        for ($j = 0; $j -lt $SizeMb; $j++) {
            $rng.GetBytes($buffer)
            $stream.Write($buffer, 0, $buffer.Length)
            $stream.Flush()
        }
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
        $rng.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
Log-Event 'stress_disk_start' "duration=$Duration file=${FileMb}MB"

$end = (Get-Date).AddSeconds($Duration)
$i = 0
while ((Get-Date) -lt $end) {
    $file = Join-Path $TargetDir ("file_{0}.bin" -f $i)
    Write-RandomFile -Path $file -SizeMb $FileMb
    Remove-Item -Path $file -Force -ErrorAction SilentlyContinue
    $i++
}

Log-Event 'stress_disk_stop' 'completed'
