$monitorDurationSecs = 300
$sampleIntervalSecs = 5
$processToMonitor = "HealthMonitorService"
$samples = @()
$endTime = (Get-Date).AddSeconds($monitorDurationSecs)

# Collect performance data
while ((Get-Date) -lt $endTime)
{
  $sysCpu = (Get-Counter '\Processor(_Total)\% Processor Time').CounterSamples[0].CookedValue
  $p = Get-Process -Name $processToMonitor -ErrorAction SilentlyContinue
  $mem = if ($p) { [Math]::Round($p.WorkingSet64 / 1MB, 2) } else { 0 }

  $samples += [pscustomobject]@{SystemCPU = $sysCpu; MemMB = $mem}
  Start-Sleep $sampleIntervalSecs
}

# Output results
"Avg System CPU: {0:N2}%" -f (($samples | Measure-Object SystemCPU -Average).Average)
"Max System CPU: {0:N2}%" -f (($samples | Measure-Object SystemCPU -Maximum).Maximum)
"Avg '$processToMonitor' RAM: {0:N2} MB" -f (($samples | Measure-Object MemMB -Average).Average)
"Max '$processToMonitor' RAM: {0:N2} MB" -f (($samples | Measure-Object MemMB -Maximum).Maximum)