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
$cpuVeryHigh = [Math]::Max($cpuCores, 1)

$memMedium = [int][Math]::Floor($totalMemMb * 0.60)
$memHigh = [int][Math]::Floor($totalMemMb * 0.75)
$memVeryHigh = [int][Math]::Floor($totalMemMb * 0.85)

New-Item -ItemType Directory -Force -Path $DiskTargetDir | Out-Null
$driveName = ([System.IO.Path]::GetPathRoot($DiskTargetDir)).TrimEnd('\')
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
        'none' { return $null }
        default { throw "Unknown CPU level: $Level" }
    }
}

function Resolve-MemMb([string]$Level) {
    switch ($Level) {
        'medium' { return $memMedium }
        'high' { return $memHigh }
        'very_high' { return $memVeryHigh }
        'none' { return $null }
        default { throw "Unknown memory level: $Level" }
    }
}

function Resolve-DiskMb([string]$Level) {
    switch ($Level) {
        'medium' { return $diskMedium }
        'high' { return $diskHigh }
        'very_high' { return $diskVeryHigh }
        'none' { return $null }
        default { throw "Unknown disk level: $Level" }
    }
}

function Start-Stressor([string]$ScriptPath, [object[]]$ArgumentList) {
    $quotedScript = '"' + $ScriptPath + '"'
    $quotedArgs = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $quotedScript
    ) + ($ArgumentList | ForEach-Object {
        $s = [string]$_
        if ($s -match '\s') { '"' + $s + '"' } else { $s }
    })

    return Start-Process -FilePath 'powershell.exe' `
        -ArgumentList $quotedArgs `
        -PassThru `
        -WindowStyle Hidden
}

$children = @()

function Cleanup-Children {
    foreach ($child in $children) {
        try {
            if ($null -ne $child -and -not $child.HasExited) {
                Stop-Process -Id $child.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {}
    }
    $script:children = @()
}

trap {
    Cleanup-Children
    throw
}

function Run-Mixed([string]$CpuLevel, [string]$MemLevel, [string]$DiskLevel, [int]$Duration) {
    $details = "duration=${Duration}s"
    $script:children = @()
    $started = $false

    if ($CpuLevel -ne 'none') {
        $workers = Resolve-CpuWorkers $CpuLevel
        $script:children += Start-Stressor $CpuScript @($RunId, $Duration, $workers)
        $details += " cpu=$CpuLevel workers=$workers total_cores=$cpuCores"
        $started = $true
    }

    if ($MemLevel -ne 'none') {
        $mb = Resolve-MemMb $MemLevel
        $script:children += Start-Stressor $MemScript @($RunId, $Duration, $mb)
        $details += " memory=$MemLevel memory_mb=$mb total_mem_mb=$totalMemMb"
        $started = $true
    }

    if ($DiskLevel -ne 'none') {
        $fileMb = Resolve-DiskMb $DiskLevel
        $script:children += Start-Stressor $DiskScript @($RunId, $Duration, $DiskTargetDir, $fileMb)
        $details += " disk=$DiskLevel file_mb=$fileMb disk_free_mb=$diskFreeMb"
        $started = $true
    }

    if (-not $started) {
        throw 'Mixed scenario must enable at least one resource'
    }

    Log-Event 'scenario_start' $details

    $failed = $false
    foreach ($child in $script:children) {
        $null = $child.WaitForExit()
        if ($child.ExitCode -ne 0) {
            $failed = $true
        }
    }
    $script:children = @()

    if ($failed) {
        Log-Event 'scenario_stop' 'status=failed'
        throw 'One or more mixed stress processes failed'
    }

    Log-Event 'scenario_stop' 'status=completed'
}

Log-Event 'mixed_block_start' "schedule=$ScheduleFile cpu_cores=$cpuCores total_mem_mb=$totalMemMb disk_free_mb=$diskFreeMb disk_medium_mb=$diskMedium disk_high_mb=$diskHigh disk_very_high_mb=$diskVeryHigh"

Import-Csv -Path $ScheduleFile | ForEach-Object {
    $gap = [int]$_.gap_seconds
    $cpuLevel  = $_.cpu_level.Trim()
    $memLevel  = $_.mem_level.Trim()
    $diskLevel = $_.disk_level.Trim()
    $duration = [int]$_.duration_seconds

    Log-Event 'idle_period_start' "duration=${gap}s"
    Start-Sleep -Seconds $gap
    Log-Event 'idle_period_stop' 'completed'

    Run-Mixed $cpuLevel $memLevel $diskLevel $duration

    Log-Event 'cooldown_start' "duration=${CooldownSeconds}s"
    Start-Sleep -Seconds $CooldownSeconds
    Log-Event 'cooldown_stop' 'completed'
}

Log-Event 'mixed_block_stop' "schedule=$ScheduleFile completed"