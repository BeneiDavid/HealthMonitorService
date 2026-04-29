namespace HealthMonitorService.Options
{
    public class MonitoringOptions
    {
        public int SampleIntervalSeconds { get; set; } = 5;
        public int PredictionWindowSeconds { get; set; } = 60;
        public PredictionMode PredictionMode { get; set; } = PredictionMode.Heuristic;
        public string? RandomForestModelPath { get; set; }
        public string? LogisticRegressionModelPath { get; set; }
    }
}
