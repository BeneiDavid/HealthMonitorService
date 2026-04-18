param(
    [string]$TestRunId = 'run_001',
    [int]$Duration = 60,
    [string]$DiskTargetDir = "$env:TEMP\thesis_stress",
    [string]$LevelOrMb = 'high'
)

$ErrorActionPreference = 'Stop'
$EventsFile = if ($env:EVENTS_FILE) { $env:EVENTS_FILE } else { 'events.csv' }
$LogEventScript = if ($env:LOG_EVENT_SCRIPT) { $env:LOG_EVENT_SCRIPT } else { Join-Path $PSScriptRoot 'log_event_windows.ps1' }
$MinFileMb = if ($env:MIN_FILE_MB) { [int]$env:MIN_FILE_MB } else { 64 }
$MinFreeDiskMb = if ($env:MIN_FREE_DISK_MB) { [int]$env:MIN_FREE_DISK_MB } else { 512 }

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    & $LogEventScript $EventsFile $TestRunId $EventType $Details
}

function Resolve-FileMb([string]$Value) {
    switch ($Value) {
        'medium' { return 256 }
        'high' { return 512 }
        'very_high' { return 1024 }
        default {
            if ($Value -match '^[0-9]+$' -and [int]$Value -ge 1) {
                return [int]$Value
            }
            throw "Unsupported disk level/size: $Value. Use medium, high, very_high, or a positive MB value."
        }
    }
}

function Write-RandomFile {
    param([string]$FilePath, [int]$FileMb)

    $Buffer = New-Object byte[] (1MB)
    $RandomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $Stream = [System.IO.File]::Open($FilePath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        for ($i = 0; $i -lt $FileMb; $i++) {
            $RandomGenerator.GetBytes($Buffer)
            $Stream.Write($Buffer, 0, $Buffer.Length)
        }
        $Stream.Flush($true)
    }
    finally {
        $Stream.Dispose()
        $RandomGenerator.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $DiskTargetDir | Out-Null
$RequestedMb = Resolve-FileMb $LevelOrMb
$DriveName = ([System.IO.Path]::GetPathRoot($DiskTargetDir)).TrimEnd('\\')
$DiskInfo = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='${DriveName}'"
$FreeMb = [int][Math]::Floor($DiskInfo.FreeSpace / 1MB)
$SafeMaxMb = $FreeMb - $MinFreeDiskMb
if ($SafeMaxMb -lt 0) { $SafeMaxMb = 0 }

$FileMb = [Math]::Min($RequestedMb, $SafeMaxMb)
if ($FileMb -lt $MinFileMb) {
    Log-Event 'stress_disk_skip' "reason=file_too_small level_or_mb=$LevelOrMb requested_mb=$RequestedMb file_mb=$FileMb free_mb=$FreeMb min_free_disk_mb=$MinFreeDiskMb"
    throw "Disk stress target too low for safe execution: $FileMb MB"
}

Log-Event 'stress_disk_start' "duration=$Duration level_or_mb=$LevelOrMb requested_mb=$RequestedMb file_mb=$FileMb free_mb=$FreeMb min_free_disk_mb=$MinFreeDiskMb"

$EndTime = (Get-Date).AddSeconds($Duration)
$IterationCount = 0
$Success = $false

try {
    while ((Get-Date) -lt $EndTime) {
        $FilePath = Join-Path $DiskTargetDir ("file_{0}.bin" -f $IterationCount)
        Write-RandomFile -FilePath $FilePath -FileMb $FileMb
        Remove-Item -Path $FilePath -Force -ErrorAction SilentlyContinue
        $IterationCount++
    }
    $Success = $true
}
finally {
    if ($Success) {
        Log-Event 'stress_disk_stop' "status=completed file_mb=$FileMb iterations=$IterationCount"
    }
    else {
        Log-Event 'stress_disk_stop' "status=failed file_mb=$FileMb iterations=$IterationCount"
    }
}
