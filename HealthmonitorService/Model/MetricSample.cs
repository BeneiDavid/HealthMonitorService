namespace HealthMonitorService.Model
{
    public readonly record struct MetricSample
    {
        // --- Metadata ---
        public string RunId { get; init; }    // Unique identifier for the test run, used to group samples from the same run together
        public DateTime Timestamp { get; init; }
        public DateTime MonitoringStartTime { get; init; }   // Starting time of the monitoring
        public string OperatingSystem { get; init; }    // e.g. "Windows_11_x64", "Debian_12_x64", "Debian_12_arm64"
        public string DeviceIp { get; init; }
        public string MachineName { get; init; }

        // --- CPU ---
        public double CpuUsagePercent { get; init; }                                                            
        public int CpuQueueLength { get; init; }    // Windows specific, should be -1 on Linux  
        public double LoadPerCore { get; init; }    // Linux specific, should be -1 on Windows

        // --- Memory ---
        public double MemoryUsagePercent { get; init; }
        public double SecondaryMemoryUsagePercent { get; init; }   // Represents the usage of virtual memory (swap on Linux, page file on Windows) as a percentage of total virtual memory


        // --- IO / Disk ---
        public double IoWaitPercent { get; init; }      // Linux specific, should be -1 on Windows
        public double DiskUsagePercent { get; init; }
        public double DiskLatencyMs { get; init; }       // Average latency of disk operations in milliseconds

        // --- System ---
        public int ProcessCount { get; init; }

        // --- Network ---
        public bool NetworkAvailable { get; init; }
        public double PacketLossPercent { get; init; }   // Percentage of packets lost during transmission, -1 if not collected

        // --- Label ---
        public FailureLabel Failure { get; init; }   // -1 unlabeled, 0 normal, 1 near failure, 2 failure
    }
}
