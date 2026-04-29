using HealthMonitorService.Model;
using HealthMonitorService.Prediction.Features;
namespace HealthMonitorService.Prediction.Heuristics
{
    public class HeuristicRiskPredictor : IRiskPredictor
    {
        private readonly record struct RiskRule(string Reason, int Score, bool IsTriggered);

        private const string SourceName = "Heuristic";
        private const int ElevatedRiskScore = 3;
        private const int CriticalRiskScore = 8;
        private const int MaxExpectedScore = 12;

        public PredictionResult Predict(IReadOnlyList<MetricSample> window)
        {
            if (window.Count == 0)
            {
                return new PredictionResult(
                    RiskLevel.Normal,
                    0,
                    SourceName,
                    "No samples available.");
            }

            MetricWindowStats stats = MetricWindowStats.From(window);
            return AssessRisk(stats);
        }

        private static PredictionResult AssessRisk(MetricWindowStats stats)
        {
            var triggeredRules = RiskSignalAnalyser.GetTriggeredSignals(stats);

            int totalScore = triggeredRules.Sum(rule => rule.Score);
            int moderateOrHigherSignals = triggeredRules.Count(rule => rule.Score >= 2);
            bool isPressureCurrent =
                stats.Latest.CpuUsagePercent >= 85 ||
                stats.Latest.MemoryUsagePercent >= 85 ||
                stats.Latest.SecondaryMemoryUsagePercent >= 30 ||
                stats.Latest.DiskLatencyMs >= 50 ||
                stats.Latest.IoWaitPercent >= 25 ||
                !stats.Latest.NetworkAvailable ||
                stats.Latest.PacketLossPercent >= 30;

            bool isCritical = totalScore >= CriticalRiskScore && moderateOrHigherSignals >= 2 && isPressureCurrent;

            RiskLevel level = isCritical switch
            {
                true => RiskLevel.CriticalRisk,
                false when totalScore >= ElevatedRiskScore => RiskLevel.ElevatedRisk,
                _ => RiskLevel.Normal
            };

            double riskScore = Math.Min((double)totalScore / MaxExpectedScore, 1.0);
            string explanation = triggeredRules.Count == 0 ? "No relevant risk signals." : string.Join(", ", triggeredRules.Select(rule => rule.Reason));

            return new PredictionResult(level, riskScore, SourceName, explanation);
        }
    }
}