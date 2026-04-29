namespace HealthMonitorService.Prediction;

public readonly record struct PredictionResult(
    RiskLevel Level,
    double RiskScore,
    string Source,
    string Explanation
);