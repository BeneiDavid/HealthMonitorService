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
$LogEventScript = Join-Path $PSScriptRoot 'log_event_windows.ps1'
$DiskTargetDir = Join-Path $env:TEMP 'thesis_stress'

$CpuCores = [Environment]::ProcessorCount
$CpuMedium = [Math]::Max([int][Math]::Floor($CpuCores / 2), 1)
$CpuHigh = [Math]::Max($CpuCores - 1, 1)
$CpuVeryHigh = $CpuCores

New-Item -ItemType Directory -Force -Path $DiskTargetDir | Out-Null

function Log-Event {
    param([string]$EventType, [string]$Details = '')
    & $LogEventScript $EventsFile $RunId $EventType $Details
}

function Validate-LevelOrNone([string]$LevelName, [string]$LevelValue) {
    switch ($LevelValue) {
        'none' {}
        'medium' {}
        'high' {}
        'very_high' {}
        default { throw "Unknown $LevelName level: $LevelValue. Use none, medium, high, or very_high." }
    }
}

function Resolve-CpuWorkers([string]$CpuLevel) {
    switch ($CpuLevel) {
        'medium' { return $CpuMedium }
        'high' { return $CpuHigh }
        'very_high' { return $CpuVeryHigh }
        'none' { return $null }
        default { throw "Unknown CPU level: $CpuLevel. Use none, medium, high, or very_high." }
    }
}

function Start-Stressor([string]$ScriptPath, [object[]]$ArgumentList) {
    $QuotedScriptPath = '"' + $ScriptPath + '"'
    $QuotedArgumentList = @()

    foreach ($ArgumentValue in $ArgumentList) {
        if ($null -ne $ArgumentValue) {
            $QuotedArgumentList += '"' + ([string]$ArgumentValue).Replace('"', '\"') + '"'
        }
    }

    $ProcessArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $QuotedScriptPath) + $QuotedArgumentList
    Start-Process -FilePath 'powershell.exe' -ArgumentList $ProcessArguments -PassThru -WindowStyle Hidden
}

$script:ChildProcesses = @()

function Cleanup-Children {
    foreach ($ChildProcess in $script:ChildProcesses) {
        try {
            if ($null -ne $ChildProcess -and -not $ChildProcess.HasExited) {
                Stop-Process -Id $ChildProcess.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch {}
    }
    $script:ChildProcesses = @()
}

trap {
    Cleanup-Children
    Write-Host "Scheduler error at line $($_.InvocationInfo.ScriptLineNumber): $($_.Exception.Message)"
    throw $_
}

function Run-Mixed([string]$CpuLevel, [string]$MemLevel, [string]$DiskLevel, [int]$Duration) {
    Validate-LevelOrNone 'cpu' $CpuLevel
    Validate-LevelOrNone 'memory' $MemLevel
    Validate-LevelOrNone 'disk' $DiskLevel

    $script:ChildProcesses = @()
    $Details = "duration=${Duration}s"
    $StartedOther = $false
    $MemoryDuration = $Duration
    $CpuWorkers = $null

    if ($CpuLevel -ne 'none') {
        $CpuWorkers = Resolve-CpuWorkers $CpuLevel
        $Details += " cpu=$CpuLevel workers=$CpuWorkers total_cores=$CpuCores"
    }

    if ($DiskLevel -ne 'none') {
        $Details += " disk=$DiskLevel target_dir=$DiskTargetDir"
    }

    if ($MemLevel -ne 'none') {
        if ($CpuLevel -ne 'none' -or $DiskLevel -ne 'none') {
            if ($Duration -le 5) {
                throw 'Duration must be greater than 5 seconds when memory is delayed after CPU/disk.'
            }
            $MemoryDuration = $Duration - 5
        }
        $Details += " memory=$MemLevel memory_duration=${MemoryDuration}s"
    }

    if ($CpuLevel -eq 'none' -and $MemLevel -eq 'none' -and $DiskLevel -eq 'none') {
        throw 'Mixed scenario must enable at least one resource'
    }

    Log-Event 'scenario_start' $Details

    if ($CpuLevel -ne 'none') {
        $script:ChildProcesses += Start-Stressor $CpuScript @($RunId, $Duration, $CpuWorkers)
        $StartedOther = $true
    }

    if ($DiskLevel -ne 'none') {
        $script:ChildProcesses += Start-Stressor $DiskScript @($RunId, $Duration, $DiskTargetDir, $DiskLevel)
        $StartedOther = $true
    }

    if ($MemLevel -ne 'none') {
        if ($StartedOther) {
            Start-Sleep -Seconds 5
        }
        $script:ChildProcesses += Start-Stressor $MemScript @($RunId, $MemoryDuration, $MemLevel)
    }

    $Failed = $false
    foreach ($ChildProcess in $script:ChildProcesses) {
        $null = $ChildProcess.WaitForExit()
        if ($ChildProcess.ExitCode -ne 0) {
            $Failed = $true
            Write-Warning "Child process failed: PID=$($ChildProcess.Id) ExitCode=$($ChildProcess.ExitCode)"
        }
    }
    $script:ChildProcesses = @()

    if ($Failed) {
        Log-Event 'scenario_stop' "status=failed duration=${Duration}s cpu=$CpuLevel memory=$MemLevel disk=$DiskLevel"
        throw 'One or more mixed stress processes failed'
    }

    Log-Event 'scenario_stop' "status=completed duration=${Duration}s cpu=$CpuLevel memory=$MemLevel disk=$DiskLevel"
}

Log-Event 'mixed_block_start' "schedule=$ScheduleFile cpu_cores=$CpuCores cpu_medium_workers=$CpuMedium cpu_high_workers=$CpuHigh cpu_very_high_workers=$CpuVeryHigh disk_target_dir=$DiskTargetDir"

$ScheduleRows = Import-Csv -Path $ScheduleFile -Delimiter ','

foreach ($ScheduleRow in $ScheduleRows) {
    if ([string]::IsNullOrWhiteSpace($ScheduleRow.gap_seconds)) { continue }

    $GapSeconds = [int]$ScheduleRow.gap_seconds.Trim()
    $CpuLevel = $ScheduleRow.cpu_level.Trim()
    $MemLevel = $ScheduleRow.mem_level.Trim()
    $DiskLevel = $ScheduleRow.disk_level.Trim()
    $DurationSeconds = [int]$ScheduleRow.duration_seconds.Trim()

    Start-Sleep -Seconds $GapSeconds
    Run-Mixed $CpuLevel $MemLevel $DiskLevel $DurationSeconds
    Start-Sleep -Seconds $CooldownSeconds
}

Log-Event 'mixed_block_stop' "schedule=$ScheduleFile completed"
