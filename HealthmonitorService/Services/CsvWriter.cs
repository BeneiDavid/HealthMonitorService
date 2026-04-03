using HealthMonitorService.Model;
using System.Globalization;

namespace HealthMonitorService.Services
{
    public class CsvWriter(string filePath)
    {
        private readonly string _filePath = filePath;
        private const string Header ="timestamp;monitoring_starttime;operating_system;device_ip;machine_name;cpu_usage_percent;cpu_queue_length;load_per_core;memory_usage_percent;secondary_memory_usage_percent;io_wait_percent;disk_usage_percent;disk_latency_ms;process_count;network_available;packet_loss_percent;failure";

        public async Task WriteSampleAsync(MetricSample sample)
        {
            if (!File.Exists(_filePath))
            {
                File.WriteAllText(_filePath, Header + Environment.NewLine);
            }

            string line =
                $"{sample.Timestamp:O};" +
                $"{sample.MonitoringStartTime:O};" +
                $"{sample.OperatingSystem};" +
                $"{sample.DeviceIp};" +
                $"{sample.MachineName};" +
                $"{sample.CpuUsagePercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.CpuQueueLength.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.LoadPerCore.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.MemoryUsagePercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.SecondaryMemoryUsagePercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.IoWaitPercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.DiskUsagePercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.DiskLatencyMs.ToString(CultureInfo.InvariantCulture)};" +
                $"{sample.ProcessCount};" +
                $"{(sample.NetworkAvailable ? 1 : 0)};" +
                $"{sample.PacketLossPercent.ToString(CultureInfo.InvariantCulture)};" +
                $"{(int)sample.Failure}";

            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine);
        }
    }
}
