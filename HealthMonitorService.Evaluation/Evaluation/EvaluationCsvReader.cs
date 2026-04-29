using HealthMonitorService.Model;
using System.Globalization;

namespace HealthMonitorService.Evaluation.Evaluation
{
    public class EvaluationCsvReader(string filePath)
    {
        private readonly string _filePath = filePath;

        public async Task<IReadOnlyList<EvaluationSample>> ReadAllAsync()
        {
            string[] lines = await File.ReadAllLinesAsync(_filePath);
            List<EvaluationSample> samples = [];

            foreach (string line in lines.Skip(1))   // Skip header
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(';');

                if (parts.Length != 18)
                {
                    throw new FormatException($"Invalid column count. Expected 18, got {parts.Length}.");
                }

                MetricSample sample = new()
                {
                    Timestamp = DateTime.Parse(parts[1], null, DateTimeStyles.RoundtripKind),
                    MonitoringStartTime = DateTime.Parse(parts[2], null, DateTimeStyles.RoundtripKind),
                    OperatingSystem = parts[3],
                    DeviceIp = parts[4],                        // In collected runs DeviceIp is replaced with host_id for anonymization
                    MachineName = parts[5],
                    CpuUsagePercent = ParseDouble(parts[6]),
                    CpuQueueLength = ParseOptionalInt(parts[7]),
                    LoadPerCore = ParseOptionalDouble(parts[8]),
                    MemoryUsagePercent = ParseDouble(parts[9]),
                    SecondaryMemoryUsagePercent = ParseDouble(parts[10]),
                    IoWaitPercent = ParseOptionalDouble(parts[11]),
                    DiskUsagePercent = ParseDouble(parts[12]),
                    DiskLatencyMs = ParseDouble(parts[13]),
                    ProcessCount = ParseInt(parts[14]),
                    NetworkAvailable = ParseInt(parts[15]) == 1,
                    PacketLossPercent = ParseDouble(parts[16]),
                    Failure = (FailureLabel)ParseInt(parts[17])
                };

                EvaluationSample evaluationSample = new()
                {
                    RunId = parts[0],
                    Sample = sample
                };

                samples.Add(evaluationSample);
            }

            return samples;
        }

        private static double ParseDouble(string value) => double.Parse(value, CultureInfo.InvariantCulture);

        private static int ParseInt(string value) => int.Parse(value, CultureInfo.InvariantCulture);

        private static double ParseOptionalDouble(string value) => string.IsNullOrWhiteSpace(value) ? -1 : ParseDouble(value);

        private static int ParseOptionalInt(string value) => string.IsNullOrWhiteSpace(value) ? -1 : ParseInt(value);
    }
}