#!/bin/bash

monitorDurationSecs=120
sampleIntervalSecs=5
processToMonitor="HealthMonitorService"
endTime=$((SECONDS + monitorDurationSecs))

# Ensure sysstat is installed for mpstat
if ! command -v mpstat &> /dev/null
then
    echo "mpstat could not be found, please install sysstat package."
    exit 1
fi

echo "Timestamp,System_CPU_Usage,HealthMonitorService_RAM_MB"

# Collect performance data
while [ $SECONDS -lt $endTime ]; do
    timestamp=$(date +%s)
    sys_cpu=$(mpstat 1 1 | awk '/Average:/ {print 100 - $NF}')
    
    pid=$(pgrep -f "$processToMonitor" | head -n1)
    if [ -n "$pid" ]; then
        proc_mem=$(ps -p "$pid" -o rss= | awk '{print $1/1024}')
    else
        proc_mem=0
    fi
    
    echo "$timestamp,$sys_cpu,$proc_mem" 
    
    sleep $sampleIntervalSecs
done | awk -F',' -v proc="$processToMonitor" '{
    # Skip header line in awk processing
    if (NR == 1 && $1 == "Timestamp") {
        next
    }
    cpuSum += $2; # $2 is System_CPU_Usage
    memSum += $3; # $3 is HealthMonitorService_RAM_MB
    if($2 > maxCPU) maxCPU = $2;
    if($3 > maxMem) maxMem = $3;
    count++;
}
END {
    if(count > 0) {
        printf "Avg System CPU: %.2f%%\n", cpuSum/count;
        printf "Max System CPU: %.2f%%\n", maxCPU;
        printf "Avg %s RAM: %.2f MB\n", proc, memSum/count;
        printf "Max %s RAM: %.2f MB\n", proc, maxMem;
    } else {
        print "No data collected.";
    }
}'