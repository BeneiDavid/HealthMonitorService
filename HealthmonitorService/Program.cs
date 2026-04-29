using HealthMonitorService.Collectors;
using HealthMonitorService.Options;
using HealthMonitorService.Prediction;
using HealthMonitorService.Prediction.Heuristics;
using HealthMonitorService.Prediction.MachineLearning;
using HealthMonitorService.Services;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection("Monitoring"));

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

builder.Services.AddSingleton<IRiskPredictor>(provider =>
{
    MonitoringOptions options = provider.GetRequiredService<IOptions<MonitoringOptions>>().Value;

    if (options.SampleIntervalSeconds <= 0 || options.PredictionWindowSeconds <= 0)
    {
        throw new InvalidOperationException("SampleIntervalSeconds and PredictionWindowSeconds must be greater than zero.");
    }

    return options.PredictionMode switch
    {
        PredictionMode.Heuristic => new HeuristicRiskPredictor(),

        PredictionMode.RandomForest => new TrainedMachineLearningModel(RequirePath(options.RandomForestModelPath, "RandomForestModelPath is required"), "Random Forest"),

        PredictionMode.LogisticRegression => new TrainedMachineLearningModel(RequirePath(options.LogisticRegressionModelPath, "LogisticRegressionModelPath is required"), "Logistic Regression"),

        _ => throw new InvalidOperationException("Unknown prediction mode.")
    };
});

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDirectory);

var path = Path.Combine(dataDirectory, "metrics.csv");
builder.Services.AddSingleton<CsvWriter>(_ => new CsvWriter(path));
builder.Services.AddHostedService<DataCollectionService>();

var host = builder.Build();
await host.RunAsync();


static string RequirePath(string? path, string message)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        throw new InvalidOperationException(message);
    }

    return path;
}