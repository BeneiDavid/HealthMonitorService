using HealthMonitorService.Collectors;
using HealthMonitorService.Options;
using HealthMonitorService.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection("Monitoring"));

// TODO: Add runcontext later


var hostContext = HostContextFactory.Create();
builder.Services.AddSingleton(hostContext);


if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IMetricCollector, WindowsMetricCollector>();
}
else if (OperatingSystem.IsLinux())
{
    builder.Services.AddSingleton<IMetricCollector, LinuxMetricCollector>();
}
else
{
    Log.Fatal("Unsupported operating system. Exiting.");
    return;
}

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDirectory);

var path = Path.Combine(dataDirectory, "metrics.csv");
builder.Services.AddSingleton<CsvWriter>(_ => new CsvWriter(path));
builder.Services.AddHostedService<DataCollectionService>();

var host = builder.Build();
await host.RunAsync();
