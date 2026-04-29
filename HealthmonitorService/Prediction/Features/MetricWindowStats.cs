using HealthMonitorService.Model;

namespace HealthMonitorService.Prediction.Features
{
    public readonly record struct MetricWindowStats(
        MetricSample Latest,
        double CpuMax,
        double CpuAverage,
        double MemoryMax,
        double MemoryAverage,
        double SecondaryMemoryMax,
        double DiskUsageMax,
        double DiskLatencyMax,
        double DiskLatencyAverage,
        double IoWaitMax,
        double PacketLossMax,
        int CpuQueueMax,
        double LoadPerCoreMax,
        double MaxGapSeconds)
    {
        public static MetricWindowStats From(IReadOnlyList<MetricSample> samples)
        {
            // ProcessCount is intentionally not included in the heuristic stats.
            // It is highly environment-dependent and weak as a standalone risk signal.

            return new MetricWindowStats(
                Latest: samples[^1],
                CpuMax: MaxValid(samples, s => s.CpuUsagePercent),
                CpuAverage: AverageValid(samples, s => s.CpuUsagePercent),
                MemoryMax: MaxValid(samples, s => s.MemoryUsagePercent),
                MemoryAverage: AverageValid(samples, s => s.MemoryUsagePercent),
                SecondaryMemoryMax: MaxValid(samples, s => s.SecondaryMemoryUsagePercent),
                DiskUsageMax: MaxValid(samples, s => s.DiskUsagePercent),
                DiskLatencyMax: MaxValid(samples, s => s.DiskLatencyMs),
                DiskLatencyAverage: AverageValid(samples, s => s.DiskLatencyMs),
                IoWaitMax: MaxValid(samples, s => s.IoWaitPercent),
                PacketLossMax: MaxValid(samples, s => s.PacketLossPercent),
                CpuQueueMax: (int)MaxValid(samples, s => s.CpuQueueLength),
                LoadPerCoreMax: MaxValid(samples, s => s.LoadPerCore),
                MaxGapSeconds: GetMaxGapSeconds(samples));
        }

        private static double MaxValid(
        IReadOnlyList<MetricSample> samples,
        Func<MetricSample, double> selector)
        {
            var values = samples
                .Select(selector)
                .Where(IsValidMetric)
                .ToList();

            return values.Count == 0 ? 0 : values.Max();
        }

        private static double AverageValid(
            IReadOnlyList<MetricSample> samples,
            Func<MetricSample, double> selector)
        {
            var values = samples
                .Select(selector)
                .Where(IsValidMetric)
                .ToList();

            return values.Count == 0 ? 0 : values.Average();
        }

        private static bool IsValidMetric(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        }

        private static double GetMaxGapSeconds(IReadOnlyList<MetricSample> samples)
        {
            if (samples.Count < 2)
            {
                return 0;
            }

            double maxGap = 0;

            for (int i = 1; i < samples.Count; i++)
            {
                double gapSeconds = (samples[i].Timestamp - samples[i - 1].Timestamp).TotalSeconds;

                if (gapSeconds > maxGap)
                {
                    maxGap = gapSeconds;
                }
            }

            return maxGap;
        }
    }
}
