using HealthMonitorService.Collectors;
using HealthMonitorService.Model;
using HealthMonitorService.Options;
using Microsoft.Extensions.Options;

namespace HealthMonitorService.Services
{
    public class DataCollectionService(ILogger<DataCollectionService> logger, IMetricCollector metricCollector, CsvWriter csvWriter, IOptions<MonitoringOptions> monitoringOptions, HostContext hostContext) : BackgroundService
    {
        private readonly ILogger<DataCollectionService> _logger = logger;
        private readonly IMetricCollector _metricCollector = metricCollector;
        private readonly CsvWriter _csvWriter = csvWriter;
        private readonly MonitoringOptions _monitoringOptions = monitoringOptions.Value;
        private readonly HostContext _hostContext = hostContext;
        private int _totalSamplesWritten = 0;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                 "DataCollectionService started. MachineName: {MachineName}, DeviceIP: {DeviceIP}, OperatingSystem: {OperatingSystem}.",
                 _hostContext.MachineName,
                 _hostContext.DeviceIP,
                 _hostContext.OperatingSystem);

            bool firstSample = true;
            DateTime lastSummaryLogUtc = DateTime.UtcNow;
            int writtenSamplesSinceLastLog = 0;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        MetricSample sample = await _metricCollector.CollectAsync(stoppingToken);

                        // Skip the first sample because some metrics (for example CPU usage)
                        // may require an initial baseline measurement to produce valid values.
                        if (!firstSample)
                        {
                            await _csvWriter.WriteSampleAsync(sample);
                            writtenSamplesSinceLastLog++;
                            _totalSamplesWritten++;
                        }

                        firstSample = false;

                        var now = DateTime.UtcNow;
                        if ((now - lastSummaryLogUtc).TotalSeconds >= 60)
                        {
                            _logger.LogInformation(
                                "Monitoring summary: {SampleCount} samples written in the last 60 seconds.",
                                writtenSamplesSinceLastLog);

                            writtenSamplesSinceLastLog = 0;
                            lastSummaryLogUtc = now;
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during metric collection.");
                    }

                    await Task.Delay(
                        TimeSpan.FromSeconds(_monitoringOptions.SampleIntervalSeconds), 
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }

            _logger.LogInformation(
                "Data collection terminated. Total samples written: {TotalSamples}.",
                _totalSamplesWritten);
        }
    }  
}
