using HealthMonitorService.Model;

namespace HealthMonitorService.Collectors
{
    public interface IMetricCollector
    {
        Task<MetricSample> CollectAsync(CancellationToken cancellationToken = default);
    }
}
