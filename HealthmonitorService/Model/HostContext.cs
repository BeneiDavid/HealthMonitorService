namespace HealthMonitorService.Model
{
    public record class HostContext
    {
        public string DeviceIP { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public DateTime MonitoringStartTime { get; set; } = DateTime.MinValue;
        
    }
}
