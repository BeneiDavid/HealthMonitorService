using HealthMonitorService.Model;
using HealthMonitorService.Prediction.Features;
using Microsoft.ML;

namespace HealthMonitorService.Prediction.MachineLearning
{
    public class TrainedMachineLearningModel : IRiskPredictor
    {
        private readonly PredictionEngine<MlModelInput, MlModelOutput> _predictionEngine;

        public string SourceName { get; }

        public TrainedMachineLearningModel(string modelPath, string sourceName)
        {
            SourceName = sourceName;

            MLContext mlContext = new();
            var model = mlContext.Model.Load(modelPath, out _);
            _predictionEngine = mlContext.Model.CreatePredictionEngine<MlModelInput, MlModelOutput>(model);
        }

        public PredictionResult Predict(IReadOnlyList<MetricSample> window)
        {
            MlModelInput input = MlModelInput.From(window);
            MlModelOutput output = _predictionEngine.Predict(input);

            RiskLevel level = MapLabelToRiskLevel(output.PredictedLabel);
            double confidence = GetPredictionConfidence(output.Score, level);
            
            string scoreText = output.Score is null ? "no scores" : string.Join(", ", output.Score.Select(s => s.ToString("F4")));

            MetricWindowStats stats = MetricWindowStats.From(window);
            string explanation = RiskSignalAnalyser.BuildExplanation(stats);

            return new PredictionResult(level, confidence, SourceName, explanation);
        }

        private static RiskLevel MapLabelToRiskLevel(uint label)
        {
            // ML.NET key labels are 1-based after MapValueToKey: 1 = Normal, 2 = ElevatedRisk, 3 = CriticalRisk
            return label switch
            {
                1 => RiskLevel.Normal,
                2 => RiskLevel.ElevatedRisk,
                3 => RiskLevel.CriticalRisk,
                _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown label")
            };
        }

        private static double GetPredictionConfidence(float[]? scores, RiskLevel level)
        {
            if (scores is null || scores.Length == 0)
            {
                return 0;
            }

            // Score array is zero-based in class order: 0 = Normal, 1 = ElevatedRisk, 2 = CriticalRisk
            int scoreIndex = level switch
            {
                RiskLevel.Normal => 0,
                RiskLevel.ElevatedRisk => 1,
                RiskLevel.CriticalRisk => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown level")
            };

            if (scoreIndex < 0 || scoreIndex >= scores.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Predicted risk level does not match ML score output.");
            }

            return Math.Clamp(scores[scoreIndex], 0, 1);
        }
    }
}
