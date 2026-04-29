using HealthMonitorService.Model;

namespace HealthMonitorService.Prediction.MachineLearning
{
    public interface IMachineLearningModel
    {
        public string SourceName { get; }

        public PredictionResult Predict(IReadOnlyList<MetricSample> window);
    }
}
