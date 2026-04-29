using HealthMonitorService.Model;
using HealthMonitorService.Options;
using HealthMonitorService.Prediction;
using HealthMonitorService.Prediction.Features;

namespace HealthMonitorService.Evaluation.Evaluation
{
    /// <summary>
    /// Measures how early can the model identify elevated risks before a confirmed failure
    /// </summary>
    public class LeadTimeEvaluator(IRiskPredictor predictor)
    {
        public async Task EvaluateFileAsync(string filePath, MonitoringOptions options)
        {
            var samples = await new EvaluationCsvReader(filePath).ReadAllAsync();

            List<double> leadTimes = [];
            int missed = 0;

            foreach (var run in samples.GroupBy(s => s.RunId))
            {
                double? leadTime = EvaluateRun([.. run.OrderBy(s => s.Sample.Timestamp)], options);

                if (leadTime is null)
                {
                    continue;
                }
                else if (leadTime.Value < 0)
                {
                    missed++;
                }
                else
                {
                    leadTimes.Add(leadTime.Value);
                }  
            }

            Console.WriteLine($"Lead-time evaluation of dataset: {Path.GetFileName(filePath)}");
            Console.WriteLine($"Confirmed failures: {leadTimes.Count + missed}");
            Console.WriteLine($"Detected before failure: {leadTimes.Count}");
            Console.WriteLine($"Missed failures: {missed}");

            if (leadTimes.Count > 0)
            {
                Console.WriteLine($"Average lead time: {leadTimes.Average():F1} seconds");
                Console.WriteLine($"Min lead time: {leadTimes.Min():F1} seconds");
                Console.WriteLine($"Max lead time: {leadTimes.Max():F1} seconds");
            }
        }

        private double? EvaluateRun(IReadOnlyList<EvaluationSample> runSamples, MonitoringOptions options)
        {
            int failureIndex = -1;

            for (int i = 0; i < runSamples.Count; i++)
            {
                if (runSamples[i].Sample.Failure == FailureLabel.Failure)
                {
                    failureIndex = i;
                    break;
                }
            }

            if (failureIndex < 0)
            {
                return null;
            }

            DateTime failureTime = runSamples[failureIndex].Sample.Timestamp;
            DateTime episodeStart = FindFailureEpisodeStart(runSamples, failureIndex);

            MetricWindowBuffer buffer = new(TimeSpan.FromSeconds(options.PredictionWindowSeconds));

            int minimumSamples = options.PredictionWindowSeconds / options.SampleIntervalSeconds;

            foreach (EvaluationSample evaluationSample in runSamples)
            {
                MetricSample sample = evaluationSample.Sample;

                if (sample.Timestamp >= failureTime)
                    break;

                buffer.Add(sample);

                if (!buffer.IsReady(minimumSamples))
                    continue;

                // Ignore older warnings from earlier elevated periods, only measuring lead time for the final failure (marked as failure in dataset)
                if (sample.Timestamp < episodeStart)
                    continue;

                PredictionResult prediction = predictor.Predict(buffer.GetSamples());

                if (prediction.Level != RiskLevel.Normal)
                {
                    Console.WriteLine("---- FIRST WARNING ----");
                    Console.WriteLine($"Warning timestamp: {sample.Timestamp:O}");
                    Console.WriteLine($"Failure timestamp: {failureTime:O}");
                    Console.WriteLine($"Lead time: {(failureTime - sample.Timestamp).TotalSeconds:F1}s");
                    Console.WriteLine();

                    return (failureTime - sample.Timestamp).TotalSeconds;
                }
            }

            return -1;
        }

        private static DateTime FindFailureEpisodeStart(IReadOnlyList<EvaluationSample> runSamples, int failureIndex)
        {
            int index = failureIndex;

            while (index > 0 && runSamples[index - 1].Sample.Failure != FailureLabel.Normal && runSamples[index - 1].Sample.Failure != FailureLabel.Unlabeled)
            {
                index--;
            }

            return runSamples[index].Sample.Timestamp;
        }
    }
}