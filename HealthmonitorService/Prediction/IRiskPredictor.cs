using HealthMonitorService.Model;

namespace HealthMonitorService.Prediction;

public interface IRiskPredictor
{
    PredictionResult Predict(IReadOnlyList<MetricSample> window);
}