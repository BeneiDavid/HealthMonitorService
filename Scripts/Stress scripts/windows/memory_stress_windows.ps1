param(
    [string]$TestRunId = 'run_001',
    [int]$Duration = 60,
    [string]$LevelOrPercent = 'high'
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$LogEventScript = if ($env:LOG_EVENT_SCRIPT) { $env:LOG_EVENT_SCRIPT } else { Join-Path $PSScriptRoot 'log_event_windows.ps1' }
$OsInfo = Get-CimInstance Win32_OperatingSystem
$TotalMemMb = [int][Math]::Floor($OsInfo.TotalVisibleMemorySize / 1024)
$AvailableMb = [int][Math]::Floor($OsInfo.FreePhysicalMemory / 1024)
$DefaultMinFreeMb = [int][Math]::Floor($TotalMemMb * 0.05)
if ($DefaultMinFreeMb -lt 200) { $DefaultMinFreeMb = 200 }
$MinFreeMb = if ($env:MIN_FREE_MB) { [int]$env:MIN_FREE_MB } else { $DefaultMinFreeMb }
$MinTargetMb = if ($env:MIN_TARGET_MB) { [int]$env:MIN_TARGET_MB } else { 256 }
$ChunkMb = if ($env:CHUNK_MB) { [int]$env:CHUNK_MB } else { 256 }
$SleepBetweenChunks = if ($env:SLEEP_BETWEEN_CHUNKS) { [double]$env:SLEEP_BETWEEN_CHUNKS } else { 0.05 }
$KeepTouchingInterval = if ($env:KEEP_TOUCHING_INTERVAL) { [double]$env:KEEP_TOUCHING_INTERVAL } else { 1.0 }

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    & $LogEventScript $EventsFile $TestRunId $EventType $Details
}

function Resolve-Percent([string]$LevelOrPercentValue) {
    switch ($LevelOrPercentValue) {
        'medium' { return 65 }
        'high' { return 85 }
        'very_high' { return 100 }
        default {
            $PercentText = $LevelOrPercentValue.TrimEnd('%')
            if ($PercentText -match '^[0-9]+$') {
                $PercentNumber = [int]$PercentText
                if ($PercentNumber -ge 1 -and $PercentNumber -le 100) {
                    return $PercentNumber
                }
            }
            throw "Unsupported memory level/percent: $LevelOrPercentValue. Use medium, high, very_high, or 1-100 / 1-100%."
        }
    }
}

$Percent = Resolve-Percent $LevelOrPercent
$RequestedMb = [int][Math]::Floor($AvailableMb * $Percent / 100)
$SafeCeilingMb = $AvailableMb - $MinFreeMb
if ($SafeCeilingMb -lt 0) { $SafeCeilingMb = 0 }
$TargetMb = [Math]::Min($RequestedMb, $SafeCeilingMb)

if ($TargetMb -lt $MinTargetMb) {
    Log-Event 'stress_memory_skip' "reason=target_too_low level_or_percent=$LevelOrPercent percent=$Percent requested_mb=$RequestedMb target_mb=$TargetMb available_mb=$AvailableMb min_free_mb=$MinFreeMb safe_ceiling_mb=$SafeCeilingMb"
    throw "Memory stress target too low for safe execution: $TargetMb MB"
}

Log-Event 'stress_memory_start' "duration=$Duration level_or_percent=$LevelOrPercent percent=$Percent requested_mb=$RequestedMb target_mb=$TargetMb total_mem_mb=$TotalMemMb available_mb=$AvailableMb min_free_mb=$MinFreeMb safe_ceiling_mb=$SafeCeilingMb"

$Blocks = New-Object 'System.Collections.Generic.List[byte[]]'
$PageSize = 4096
$EndTime = (Get-Date).AddSeconds($Duration)
$Success = $false

try {
    while ($Blocks.Count * $ChunkMb -lt $TargetMb -and (Get-Date) -lt $EndTime) {
        $CurrentMb = [Math]::Min($ChunkMb, $TargetMb - ($Blocks.Count * $ChunkMb))
        $Block = New-Object byte[] ($CurrentMb * 1MB)
        for ($i = 0; $i -lt $Block.Length; $i += $PageSize) {
            $Block[$i] = 1
        }
        $Blocks.Add($Block)
        if ($SleepBetweenChunks -gt 0) {
            Start-Sleep -Milliseconds ([int]($SleepBetweenChunks * 1000))
        }
    }

    while ((Get-Date) -lt $EndTime) {
        foreach ($Block in $Blocks) {
            if ($Block.Length -gt 0) {
                $Block[0] = ($Block[0] + 1) % 256
            }
        }
        if ($KeepTouchingInterval -gt 0) {
            Start-Sleep -Milliseconds ([int]($KeepTouchingInterval * 1000))
        }
    }

    $Success = $true
}
finally {
    $Blocks.Clear()
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()

    if ($Success) {
        Log-Event 'stress_memory_stop' "status=completed target_mb=$TargetMb"
    }
    else {
        Log-Event 'stress_memory_stop' "status=failed target_mb=$TargetMb"
    }
}
