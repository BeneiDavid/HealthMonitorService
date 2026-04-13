param(
    [string]$TestRunId = "run_001",
    [int]$Duration = 60,
    [int]$Workers = 2
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$WorkerScript = Join-Path $env:TEMP 'thesis_cpu_worker.ps1'

function Log-Event {
    param(
        [string]$EventType,
        [string]$Details = ''
    )
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Add-Content -Path $EventsFile -Value "$timestamp,$TestRunId,$EventType,$Details"
}

@'
$ErrorActionPreference = "SilentlyContinue"
while ($true) {
    $x = 0
    for ($i = 0; $i -lt 500000; $i++) {
        $x += [Math]::Sqrt($i)
    }
}
'@ | Set-Content -Path $WorkerScript -Encoding UTF8

$children = @()
try {
    Log-Event 'stress_cpu_start' "duration=$Duration workers=$Workers"

    for ($i = 0; $i -lt $Workers; $i++) {
        $proc = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $WorkerScript `
            -PassThru -WindowStyle Hidden
        $children += $proc
    }

    Start-Sleep -Seconds $Duration
}
finally {
    foreach ($child in $children) {
        try {
            if ($null -ne $child -and -not $child.HasExited) {
                Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {}
    }

    Remove-Item -Path $WorkerScript -Force -ErrorAction SilentlyContinue
    Log-Event 'stress_cpu_stop' 'completed'
}
