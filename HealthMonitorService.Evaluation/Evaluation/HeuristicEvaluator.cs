using HealthMonitorService.Model;
using HealthMonitorService.Options;
using HealthMonitorService.Prediction;
using HealthMonitorService.Prediction.Features;
using HealthMonitorService.Prediction.Heuristics;

namespace HealthMonitorService.Evaluation.Evaluation
{
    public class HeuristicEvaluator
    {
        private readonly HeuristicRiskPredictor _predictor = new();

        public async Task EvaluateFileAsync(string filePath, MonitoringOptions options)
        {
            if (options.SampleIntervalSeconds <= 0 || options.PredictionWindowSeconds <= 0)
            {
                throw new ArgumentException("SampleIntervalSeconds and PredictionWindowSeconds must be greater than zero.");
            }

            EvaluationMetrics totalMetrics = new();
            EvaluationCsvReader reader = new(filePath);
            IReadOnlyList<EvaluationSample> samples = await reader.ReadAllAsync();

            foreach (IGrouping<string, EvaluationSample> runGroup in samples.GroupBy(s => s.RunId))
            {
                EvaluationMetrics runMetrics = EvaluateRun([.. runGroup], options);
                totalMetrics.Merge(runMetrics);
            }

            Console.WriteLine($"Evaluation file: {Path.GetFileName(filePath)}");
            totalMetrics.Print();
        }

        private EvaluationMetrics EvaluateRun(IReadOnlyList<EvaluationSample> runSamples, MonitoringOptions options)
        {
            MetricWindowBuffer buffer = new(TimeSpan.FromSeconds(options.PredictionWindowSeconds));
            int minimumSamples = options.PredictionWindowSeconds / options.SampleIntervalSeconds;

            EvaluationMetrics metrics = new();

            foreach (EvaluationSample evaluationSample in runSamples)
            {
                MetricSample sample = evaluationSample.Sample;

                buffer.Add(sample);

                if (!buffer.IsReady(minimumSamples))
                {
                    continue;
                }

                IReadOnlyList<MetricSample> window = buffer.GetSamples();
                PredictionResult prediction = _predictor.Predict(window);
                RiskLevel actual = EvaluationUtilities.ToRiskLevel(sample.Failure);

                metrics.Add(actual, prediction.Level);
            }

            return metrics;
        }
    }
}
