param(
    [Parameter(Mandatory=$true)][string]$EventsFile,
    [Parameter(Mandatory=$true)][string]$TestRunId,
    [Parameter(Mandatory=$true)][string]$EventType,
    [string]$Details = ''
)

$ErrorActionPreference = 'Stop'
$Timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
Add-Content -Path $EventsFile -Value "$Timestamp,$TestRunId,$EventType,$Details"
