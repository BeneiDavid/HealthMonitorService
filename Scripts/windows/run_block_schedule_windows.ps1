param(
    [Parameter(Mandatory=$true)][string]$ScheduleFile,
    [string]$RunId = 'run_001',
    [int]$CooldownSeconds = 15
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$CpuScript = Join-Path $PSScriptRoot 'cpu_stress_windows.ps1'
$MemScript = Join-Path $PSScriptRoot 'memory_stress_windows.ps1'
$DiskScript = Join-Path $PSScriptRoot 'disk_io_stress_windows.ps1'
$DiskTargetDir = Join-Path $env:TEMP 'thesis_stress'

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    Add-Content -Path $EventsFile -Value "$timestamp,$RunId,$EventType,$Details"
}

function Clamp([int]$Value, [int]$Min, [int]$Max) {
    if ($Value -lt $Min) { return $Min }
    if ($Value -gt $Max) { return $Max }
    return $Value
}

$cpuCores = [Environment]::ProcessorCount
$totalMemMb = [int][Math]::Floor((Get-CimInstance Win32_OperatingSystem).TotalVisibleMemorySize / 1024)
$cpuMedium = [Math]::Max([int][Math]::Floor($cpuCores / 2), 1)
$cpuHigh = [Math]::Max($cpuCores - 1, 1)
$cpuVeryHigh = $cpuHigh
$memMedium = [int][Math]::Floor($totalMemMb * 0.60)
$memHigh = [int][Math]::Floor($totalMemMb * 0.75)
$memVeryHigh = [int][Math]::Floor($totalMemMb * 0.85)

New-Item -ItemType Directory -Force -Path $DiskTargetDir | Out-Null
$driveName = ([System.IO.Path]::GetPathRoot($DiskTargetDir)).TrimEnd('\\')
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='${driveName}'"
$diskFreeMb = [int][Math]::Floor($disk.FreeSpace / 1MB)
$diskMedium = Clamp ([int][Math]::Floor($diskFreeMb * 0.02)) 64 256
$diskHigh = Clamp ([int][Math]::Floor($diskFreeMb * 0.05)) 128 512
$diskVeryHigh = Clamp ([int][Math]::Floor($diskFreeMb * 0.10)) 256 1024

function Resolve-CpuWorkers([string]$Level) {
    switch ($Level) {
        'medium' { return $cpuMedium }
        'high' { return $cpuHigh }
        'very_high' { return $cpuVeryHigh }
        default { throw "Unknown CPU level: $Level" }
    }
}

function Resolve-MemMb([string]$Level) {
    switch ($Level) {
        'medium' { return $memMedium }
        'high' { return $memHigh }
        'very_high' { return $memVeryHigh }
        default { throw "Unknown memory level: $Level" }
    }
}

function Resolve-DiskMb([string]$Level) {
    switch ($Level) {
        'medium' { return $diskMedium }
        'high' { return $diskHigh }
        'very_high' { return $diskVeryHigh }
        default { throw "Unknown disk level: $Level" }
    }
}

function Run-Cpu([string]$Level, [int]$Duration) {
    $workers = Resolve-CpuWorkers $Level
    Log-Event 'scenario_start' "resource=cpu level=$Level duration=${Duration}s workers=$workers total_cores=$cpuCores"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $CpuScript $RunId $Duration $workers
    Log-Event 'scenario_stop' "resource=cpu level=$Level completed"
}

function Run-Memory([string]$Level, [int]$Duration) {
    $mb = Resolve-MemMb $Level
    Log-Event 'scenario_start' "resource=memory level=$Level duration=${Duration}s memory_mb=$mb total_mem_mb=$totalMemMb"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $MemScript $RunId $Duration $mb
    Log-Event 'scenario_stop' "resource=memory level=$Level completed"
}

function Run-Disk([string]$Level, [int]$Duration) {
    $fileMb = Resolve-DiskMb $Level
    Log-Event 'scenario_start' "resource=disk level=$Level duration=${Duration}s file_mb=$fileMb disk_free_mb=$diskFreeMb"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $DiskScript $RunId $Duration $DiskTargetDir $fileMb
    Log-Event 'scenario_stop' "resource=disk level=$Level completed"
}

Log-Event 'block_start' "schedule=$ScheduleFile cpu_cores=$cpuCores total_mem_mb=$totalMemMb disk_free_mb=$diskFreeMb disk_medium_mb=$diskMedium disk_high_mb=$diskHigh disk_very_high_mb=$diskVeryHigh"

Import-Csv -Path $ScheduleFile | ForEach-Object {
    $gap = [int]$_.gap_seconds
    $resource = $_.resource
    $level = $_.level
    $duration = [int]$_.duration_seconds

    Log-Event 'idle_period_start' "duration=${gap}s"
    Start-Sleep -Seconds $gap
    Log-Event 'idle_period_stop' 'completed'

    switch ($resource) {
        'cpu' { Run-Cpu $level $duration }
        'memory' { Run-Memory $level $duration }
        'disk' { Run-Disk $level $duration }
        default { throw "Unknown resource type: $resource" }
    }

    Log-Event 'cooldown_start' "duration=${CooldownSeconds}s"
    Start-Sleep -Seconds $CooldownSeconds
    Log-Event 'cooldown_stop' 'completed'
}

Log-Event 'block_stop' "schedule=$ScheduleFile completed"
