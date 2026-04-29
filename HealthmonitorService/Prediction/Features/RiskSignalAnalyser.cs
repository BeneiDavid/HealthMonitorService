namespace HealthMonitorService.Prediction.Features
{
    public class RiskSignalAnalyser
    {
        public readonly record struct RiskSignal(
            string Reason,
            int Score,
            bool IsTriggered);

        public static IReadOnlyList<RiskSignal> GetTriggeredSignals(MetricWindowStats stats)
        {
            RiskSignal[] signals =
            [
                new("critical memory pressure", 4, stats.MemoryMax >= 95),
                new("combined CPU and memory pressure", 3, stats.CpuMax >= 90 && stats.MemoryMax >= 80),
                new("high CPU usage", 2, stats.CpuAverage >= 85 || stats.CpuMax >= 95),
                new("high memory usage", 2, stats.MemoryAverage >= 75 || stats.MemoryMax >= 90),
                new("swap/pagefile pressure", 2, stats.SecondaryMemoryMax >= 30),
                new("critical disk space usage", 4, stats.DiskUsageMax >= 95),
                new("high disk space usage", 2, stats.DiskUsageMax >= 90 && stats.DiskUsageMax < 95),
                new("high disk latency", 2, stats.DiskLatencyAverage >= 20 || stats.DiskLatencyMax >= 50),
                new("high I/O wait", 2, stats.IoWaitMax >= 25),
                new("CPU queue/load pressure", 1, stats.CpuQueueMax >= 10 || stats.LoadPerCoreMax >= 1.5),
                new("packet loss detected", 3, stats.PacketLossMax >= 30),
                new("network unavailable", 4, !stats.Latest.NetworkAvailable),
                new("monitoring gap detected", 4, stats.MaxGapSeconds >= 8)
            ];

            return [.. signals.Where(signal => signal.IsTriggered)];
        }

        public static string BuildExplanation(MetricWindowStats stats)
        {
            IReadOnlyList<RiskSignal> triggeredSignals = GetTriggeredSignals(stats);

            return triggeredSignals.Count == 0
                ? "No relevant risk signals."
                : string.Join(", ", triggeredSignals.Select(signal => signal.Reason));
        }
    }
}
