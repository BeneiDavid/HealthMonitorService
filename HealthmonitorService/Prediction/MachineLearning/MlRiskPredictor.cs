using HealthMonitorService.Model;

namespace HealthMonitorService.Prediction.MachineLearning;

public class MlRiskPredictor(IMachineLearningModel model) : IRiskPredictor
{
    public PredictionResult Predict(IReadOnlyList<MetricSample> window)
    {
        if (window.Count == 0)
        {
            return new PredictionResult(
                RiskLevel.Normal,
                0,
                model.SourceName,
                "No samples available.");
        }

        return model.Predict(window);
    }
}