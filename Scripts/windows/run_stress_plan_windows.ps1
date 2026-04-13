param(
    [string]$RunId = 'run_001'
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$CooldownSeconds = if ($env:COOLDOWN_SECONDS) { [int]$env:COOLDOWN_SECONDS } else { 10 }
$CpuScript = Join-Path $PSScriptRoot 'cpu_stress_windows.ps1'
$MemScript = Join-Path $PSScriptRoot 'memory_stress_windows.ps1'
$DiskScript = Join-Path $PSScriptRoot 'disk_io_stress_windows.ps1'
$DiskTargetDir = Join-Path $env:TEMP 'thesis_stress'

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Add-Content -Path $EventsFile -Value "$timestamp,$RunId,$EventType,$Details"
}

function Run-Scenario([string]$ScenarioName, [scriptblock]$Action) {
    Log-Event 'scenario_start' "name=$ScenarioName"
    & $Action
    Log-Event 'scenario_stop' "name=$ScenarioName"
    Log-Event 'cooldown_start' "seconds=$CooldownSeconds"
    Start-Sleep -Seconds $CooldownSeconds
    Log-Event 'cooldown_stop' 'completed'
}

Log-Event 'stress_plan_start' 'host_driven_itrunner=true monitoring_started_manually=true'
Run-Scenario 'cpu_short' { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $CpuScript $RunId 30 2 }
Run-Scenario 'memory_moderate' { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $MemScript $RunId 60 1024 }
Run-Scenario 'disk_io_short' { & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $DiskScript $RunId 60 $DiskTargetDir 256 }
Log-Event 'stress_plan_stop' 'completed'
