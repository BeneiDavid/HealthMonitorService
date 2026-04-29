using HealthMonitorService.Model;
using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace HealthMonitorService.Collectors
{
    [SupportedOSPlatform("windows")]
    public sealed class WindowsMetricCollector : MetricCollectorBase, IMetricCollector, IDisposable
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _availableMemoryCounter;
        private readonly PerformanceCounter _cpuQueueLengthCounter;
        private readonly PerformanceCounter _pagingFileUsageCounter;
        private readonly PerformanceCounter _diskReadLatencyCounter;
        private readonly PerformanceCounter _diskWriteLatencyCounter;
        private readonly PerformanceCounter _processCountCounter;
        private readonly ulong _totalPhysicalMemoryBytes;

        public WindowsMetricCollector(HostContext hostContext) : base(hostContext)
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available Bytes");
            _cpuQueueLengthCounter = new PerformanceCounter("System", "Processor Queue Length");
            _pagingFileUsageCounter = new PerformanceCounter("Paging File", "% Usage", "_Total");
            _diskReadLatencyCounter = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Read", "_Total");
            _diskWriteLatencyCounter = new PerformanceCounter("PhysicalDisk", "Avg. Disk sec/Write", "_Total");
            _processCountCounter = new PerformanceCounter("System", "Processes");
            
            _totalPhysicalMemoryBytes = GetTotalPhysicalMemoryBytes();

            // Warm-up for counters that need an initial sample.
            _cpuCounter.NextValue();
            _cpuQueueLengthCounter.NextValue();
            _pagingFileUsageCounter.NextValue();
            _diskReadLatencyCounter.NextValue();
            _diskWriteLatencyCounter.NextValue();
            _processCountCounter.NextValue();
        }

        private static ulong GetTotalPhysicalMemoryBytes()
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
            {
                return Convert.ToUInt64(obj["TotalPhysicalMemory"]);
            }

            return 0;
        }

        public async Task<MetricSample> CollectAsync(CancellationToken cancellationToken = default)
        {
            double cpuUsagePercent = Math.Round(_cpuCounter.NextValue(), 2);
            int cpuQueueLength = Convert.ToInt32(Math.Round(_cpuQueueLengthCounter.NextValue(), 0));
            int processCount = Convert.ToInt32(Math.Round(_processCountCounter.NextValue(), 0));
            double availableMemoryBytes = _availableMemoryCounter.NextValue();
            double memoryUsagePercent = _totalPhysicalMemoryBytes > 0 ? Math.Round((_totalPhysicalMemoryBytes - availableMemoryBytes) / _totalPhysicalMemoryBytes * 100.0, 2) : -1;
            double secondaryMemoryUsagePercent = Math.Round(_pagingFileUsageCounter.NextValue(), 2);
            var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            double diskUsagePercent = -1;

            if (systemDrive.IsReady && systemDrive.TotalSize > 0)
            {
                diskUsagePercent = Math.Round((double)(systemDrive.TotalSize - systemDrive.AvailableFreeSpace) / systemDrive.TotalSize * 100.0, 2);
            }

            double diskReadLatencyMs = Math.Round(_diskReadLatencyCounter.NextValue() * 1000.0, 2);
            double diskWriteLatencyMs = Math.Round(_diskWriteLatencyCounter.NextValue() * 1000.0, 2);
            double diskLatencyMs = Math.Round((diskReadLatencyMs + diskWriteLatencyMs) / 2.0, 2);
            bool networkAvailable = GetNetworkAvailable();
            double packetLossPercent = await GetPacketLossPercentAsync(cancellationToken);

            MetricSample baseSample = CreateBaseSample();

            return baseSample with
            {
                CpuUsagePercent = cpuUsagePercent,
                CpuQueueLength = cpuQueueLength,  
                LoadPerCore = -1,   // Only on Linux
                MemoryUsagePercent = memoryUsagePercent,
                SecondaryMemoryUsagePercent = secondaryMemoryUsagePercent,
                IoWaitPercent = -1, // Only on Linux
                DiskUsagePercent = diskUsagePercent,
                DiskLatencyMs = diskLatencyMs,
                ProcessCount = processCount,
                NetworkAvailable = networkAvailable,
                PacketLossPercent = packetLossPercent,
            };
        }

        public void Dispose()
        {
            _cpuCounter.Dispose();
            _availableMemoryCounter.Dispose();
            _cpuQueueLengthCounter.Dispose();
            _pagingFileUsageCounter.Dispose();
            _diskReadLatencyCounter.Dispose();
            _diskWriteLatencyCounter.Dispose();
            _processCountCounter.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
