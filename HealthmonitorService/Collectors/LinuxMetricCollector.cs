using HealthMonitorService.Model;
using System.Globalization;
using System.Runtime.Versioning;

namespace HealthMonitorService.Collectors
{
    [SupportedOSPlatform("linux")]
    public class LinuxMetricCollector(HostContext hostContext) : MetricCollectorBase(hostContext), IMetricCollector
    {

        private long? _previousIdleTicks;
        private long? _previousTotalTicks;
        private long? _previousIoWaitTicks;

        private long? _previousReadOps;
        private long? _previousWriteOps;
        private long? _previousReadTimeMs;
        private long? _previousWriteTimeMs;

        private readonly record struct CpuMetrics(
            double CpuUsagePercent,
            double IoWaitPercent);

        private readonly record struct MemoryMetrics(
            double MemoryUsagePercent,
            double SecondaryMemoryUsagePercent);
        private readonly record struct DiskStats(
            long ReadOps,
            long WriteOps,
            long ReadTimeMs,
            long WriteTimeMs);

        public async Task<MetricSample> CollectAsync(CancellationToken cancellationToken = default)
        {
            CpuMetrics cpuMetrics = GetCpuMetrics();
            MemoryMetrics memoryMetrics = GetMemoryMetrics();

            double loadPerCore = GetLoadPerCore();
            double diskUsagePercent = GetDiskUsagePercent();
            double diskLatencyMs = GetDiskLatencyMs();
            int processCount = GetProcessCount();

            bool networkAvailable = GetNetworkAvailable();

            double packetLossPercent = await GetPacketLossPercentAsync(cancellationToken);
            MetricSample baseSample = CreateBaseSample();

            return baseSample with
            {
                CpuUsagePercent = cpuMetrics.CpuUsagePercent,
                CpuQueueLength = -1, // Windows specific
                LoadPerCore = loadPerCore,
                MemoryUsagePercent = memoryMetrics.MemoryUsagePercent,
                SecondaryMemoryUsagePercent = memoryMetrics.SecondaryMemoryUsagePercent,
                IoWaitPercent = cpuMetrics.IoWaitPercent,     
                DiskUsagePercent = diskUsagePercent,
                DiskLatencyMs = diskLatencyMs,
                ProcessCount = processCount,
                NetworkAvailable = networkAvailable,
                PacketLossPercent = packetLossPercent
            };
        }

        private CpuMetrics GetCpuMetrics()
        {
            (long idleTicks, long totalTicks, long ioWaitTicks) = ReadCpuStats();

            if (_previousIdleTicks is null || _previousTotalTicks is null || _previousIoWaitTicks is null)
            {
                _previousIdleTicks = idleTicks;
                _previousTotalTicks = totalTicks;
                _previousIoWaitTicks = ioWaitTicks;

                return new CpuMetrics(-1, -1);
            }

            long idleDelta = idleTicks - _previousIdleTicks.Value;
            long totalDelta = totalTicks - _previousTotalTicks.Value;
            long ioWaitDelta = ioWaitTicks - _previousIoWaitTicks.Value;

            _previousIdleTicks = idleTicks;
            _previousTotalTicks = totalTicks;
            _previousIoWaitTicks = ioWaitTicks;

            if (totalDelta <= 0)
            {
                return new CpuMetrics(-1, -1);
            }

            double cpuUsagePercent = (1.0 - (double)idleDelta / totalDelta) * 100.0;
            double ioWaitPercent = (double)ioWaitDelta / totalDelta * 100.0;

            return new CpuMetrics(
                Math.Round(cpuUsagePercent, 2),
                Math.Round(ioWaitPercent, 2));
        }

        private static (long idleTicks, long totalTicks, long ioWaitTicks) ReadCpuStats()
        {
            string firstLine = File.ReadLines("/proc/stat").First();
            string[] parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            long user = long.Parse(parts[1], CultureInfo.InvariantCulture);
            long nice = long.Parse(parts[2], CultureInfo.InvariantCulture);
            long system = long.Parse(parts[3], CultureInfo.InvariantCulture);
            long idle = long.Parse(parts[4], CultureInfo.InvariantCulture);
            long iowait = parts.Length > 5 ? long.Parse(parts[5], CultureInfo.InvariantCulture) : 0;
            long irq = parts.Length > 6 ? long.Parse(parts[6], CultureInfo.InvariantCulture) : 0;
            long softirq = parts.Length > 7 ? long.Parse(parts[7], CultureInfo.InvariantCulture) : 0;
            long steal = parts.Length > 8 ? long.Parse(parts[8], CultureInfo.InvariantCulture) : 0;

            long total = user + nice + system + idle + iowait + irq + softirq + steal;

            return (idle + iowait, total, iowait);
        }

        private static MemoryMetrics GetMemoryMetrics()
        {
            Dictionary<string, long> memInfo = File.ReadLines("/proc/meminfo")
                .Select(line => line.Split(':', 2))
                .ToDictionary(
                    parts => parts[0].Trim(),
                    parts => ParseKbValue(parts[1]));

            long memTotal = memInfo.GetValueOrDefault("MemTotal", 0);
            long memAvailable = memInfo.GetValueOrDefault("MemAvailable", 0);
            long swapTotal = memInfo.GetValueOrDefault("SwapTotal", 0);
            long swapFree = memInfo.GetValueOrDefault("SwapFree", 0);

            if (memTotal <= 0)
            {
                return new MemoryMetrics(-1, -1);
            }

            double memoryUsagePercent = (double)(memTotal - memAvailable) / memTotal * 100.0;

            double secondaryMemoryUsagePercent = swapTotal > 0
                ? (double)(swapTotal - swapFree) / swapTotal * 100.0
                : 0;

            return new MemoryMetrics(
                Math.Round(memoryUsagePercent, 2),
                Math.Round(secondaryMemoryUsagePercent, 2));
        }

        private static long ParseKbValue(string value)
        {
            string numericPart = value.Replace("kB", "", StringComparison.OrdinalIgnoreCase).Trim();
            return long.Parse(numericPart, CultureInfo.InvariantCulture);
        }

        private static double GetLoadPerCore()
        {
            string firstValue = File.ReadAllText("/proc/loadavg")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

            double loadAverage = double.Parse(firstValue, CultureInfo.InvariantCulture);
            int coreCount = Environment.ProcessorCount;

            if (coreCount <= 0)
            {
                return -1;
            }

            return Math.Round(loadAverage / coreCount, 2);
        }

        private static double GetDiskUsagePercent()
        {
            var drive = new DriveInfo("/");

            if (!drive.IsReady || drive.TotalSize <= 0)
                return -1;

            double usage = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0;
            return Math.Round(usage, 2);
        }
        private double GetDiskLatencyMs()
        {
            DiskStats current = ReadRootDiskStats();

            if (_previousReadOps is null ||
                _previousWriteOps is null ||
                _previousReadTimeMs is null ||
                _previousWriteTimeMs is null)
            {
                _previousReadOps = current.ReadOps;
                _previousWriteOps = current.WriteOps;
                _previousReadTimeMs = current.ReadTimeMs;
                _previousWriteTimeMs = current.WriteTimeMs;

                return -1;
            }

            long readOpsDelta = current.ReadOps - _previousReadOps.Value;
            long writeOpsDelta = current.WriteOps - _previousWriteOps.Value;
            long readTimeDelta = current.ReadTimeMs - _previousReadTimeMs.Value;
            long writeTimeDelta = current.WriteTimeMs - _previousWriteTimeMs.Value;

            _previousReadOps = current.ReadOps;
            _previousWriteOps = current.WriteOps;
            _previousReadTimeMs = current.ReadTimeMs;
            _previousWriteTimeMs = current.WriteTimeMs;

            long totalOpsDelta = readOpsDelta + writeOpsDelta;
            long totalTimeDelta = readTimeDelta + writeTimeDelta;

            if (totalOpsDelta <= 0)
            {
                return 0;
            }

            return Math.Round((double)totalTimeDelta / totalOpsDelta, 2);
        }

        private static DiskStats ReadRootDiskStats()
        {
            string rootDeviceName = GetRootDeviceName();

            foreach (string line in File.ReadLines("/proc/diskstats"))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 14)
                {
                    continue;
                }

                string deviceName = parts[2];

                if (!string.Equals(deviceName, rootDeviceName, StringComparison.Ordinal))
                {
                    continue;
                }

                long readsCompleted = long.Parse(parts[3], CultureInfo.InvariantCulture);
                long timeReadingMs = long.Parse(parts[6], CultureInfo.InvariantCulture);
                long writesCompleted = long.Parse(parts[7], CultureInfo.InvariantCulture);
                long timeWritingMs = long.Parse(parts[10], CultureInfo.InvariantCulture);

                return new DiskStats(
                    readsCompleted,
                    writesCompleted,
                    timeReadingMs,
                    timeWritingMs);
            }

            return new DiskStats(-1, -1, -1, -1);
        }

        private static string GetRootDeviceName()
        {
            foreach (string line in File.ReadLines("/proc/self/mountinfo"))
            {
                string[] parts = line.Split(" - ", StringSplitOptions.None);

                if (parts.Length != 2)
                {
                    continue;
                }

                string[] mountInfoParts = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string[] fsParts = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (mountInfoParts.Length < 5 || fsParts.Length < 2)
                {
                    continue;
                }

                string mountPoint = mountInfoParts[4];
                string source = fsParts[1];

                if (mountPoint != "/")
                {
                    continue;
                }

                string deviceName = Path.GetFileName(source);

                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    return deviceName;
                }
            }

            throw new InvalidOperationException("Could not determine root disk device from /proc/self/mountinfo.");
        }

        private static int GetProcessCount()
        {
            return Directory.EnumerateDirectories("/proc")
                .Count(path => int.TryParse(Path.GetFileName(path), out _));
        }
    }
}