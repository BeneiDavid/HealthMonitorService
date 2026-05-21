param(
    [string]$TestRunId = 'run_001',
    [int]$Duration = 60,
    [int]$Workers = 2
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$LogEventScript = if ($env:LOG_EVENT_SCRIPT) { $env:LOG_EVENT_SCRIPT } else { Join-Path $PSScriptRoot 'log_event_windows.ps1' }
$CpuCores = [Environment]::ProcessorCount
$WorkerScriptPath = Join-Path $env:TEMP 'thesis_cpu_worker.ps1'

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    & $LogEventScript $EventsFile $TestRunId $EventType $Details
}

if ($Workers -lt 1) {
    throw 'CPU workers must be a positive integer.'
}
if ($Workers -gt $CpuCores) {
    $Workers = $CpuCores
}

@'
$ErrorActionPreference = "SilentlyContinue"
while ($true) {
    $x = 0
    for ($i = 0; $i -lt 500000; $i++) {
        $x += [Math]::Sqrt($i)
    }
}
'@ | Set-Content -Path $WorkerScriptPath -Encoding UTF8

$ChildProcesses = @()
$Success = $false

try {
    Log-Event 'stress_cpu_start' "duration=$Duration workers=$Workers total_cores=$CpuCores"

    for ($i = 0; $i -lt $Workers; $i++) {
        $ChildProcess = Start-Process -FilePath 'powershell.exe' `
            -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $WorkerScriptPath `
            -PassThru -WindowStyle Hidden
        $ChildProcesses += $ChildProcess
    }

    Start-Sleep -Seconds $Duration
    $Success = $true
}
finally {
    foreach ($ChildProcess in $ChildProcesses) {
        try {
            if ($null -ne $ChildProcess -and -not $ChildProcess.HasExited) {
                Stop-Process -Id $ChildProcess.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {}
    }

    Remove-Item -Path $WorkerScriptPath -Force -ErrorAction SilentlyContinue

    if ($Success) {
        Log-Event 'stress_cpu_stop' 'status=completed'
    }
    else {
        Log-Event 'stress_cpu_stop' 'status=failed'
    }
}
