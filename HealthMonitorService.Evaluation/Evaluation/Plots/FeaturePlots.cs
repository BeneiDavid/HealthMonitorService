using HealthMonitorService.Model;
using HealthMonitorService.Prediction.MachineLearning;
using Microsoft.ML;
using ScottPlot;

namespace HealthMonitorService.Evaluation.Evaluation.Plots
{
    public class FeatureImportancePlotter
    {
        private readonly MLContext _mlContext = new(seed: 42);
                    
        private static readonly string[] MetricNames =
        [
            "CPU usage","CPU queue length","Load per core","Memory usage",
            "Secondary memory usage","I/O wait","Disk usage","Disk latency",
            "Process count","Network available","Packet loss"
        ];

        public async Task CreatePlotsAsync(string testCsvPath, string rfModelPath, string lrModelPath)
        {
            var outDir = Directory.CreateDirectory("HealthMonitorService.Evaluation/Evaluation/Plots");
            var samples = await new EvaluationCsvReader(testCsvPath).ReadAllAsync();
            var rows = BuildModelRows(samples);

            CreatePermutationImportancePlot(rfModelPath, rows, "Random Forest Permutation Importance", Path.Combine(outDir.FullName, "RandomForest.png"));
            CreatePermutationImportancePlot(lrModelPath, rows, "Logistic Regression Permutation Importance", Path.Combine(outDir.FullName, "LogisticRegression.png"));
        }
        /// <summary>
        /// Evaluates the model normally, then shuffles one metric across the windows to check metric dependency
        /// </summary>
        private void CreatePermutationImportancePlot(string modelPath, List<MlModelInput> rows, string title, string outputPath)
        {
            var model = _mlContext.Model.Load(modelPath, out _);
            double baseline = EvaluateMacroAccuracy(model, rows);
            var importances = new double[MlModelInput.MetricsPerSample];

            for (int i = 0; i < importances.Length; i++)
            {
                var permuted = CloneRows(rows);
                PermuteMetric(permuted, i);
                double permutedScore = EvaluateMacroAccuracy(model, permuted);
                importances[i] = Math.Max(0, baseline - permutedScore);
            }

            SaveHorizontalBarChart(MetricNames, importances, title, "Macro-accuracy drop", outputPath);
        }

        private double EvaluateMacroAccuracy(ITransformer model, List<MlModelInput> rows)
        {
            var data = _mlContext.Data.LoadFromEnumerable(rows);
            var preds = model.Transform(data);
            var metrics = _mlContext.MulticlassClassification.Evaluate(preds, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            return metrics.MacroAccuracy;
        }

        private static List<MlModelInput> CloneRows(List<MlModelInput> rows) => 
            [.. rows.Select(r => new MlModelInput { RawLabel = r.RawLabel, Weight = r.Weight, Features = [.. r.Features] })];

        private static void PermuteMetric(List<MlModelInput> rows, int metricIndex)
        {
            var rnd = new Random(42);
            var shuffledIndices = Enumerable.Range(0, rows.Count).OrderBy(_ => rnd.Next()).ToArray();
            var originalFeatures = rows.Select(r => (float[])r.Features.Clone()).ToList();

            for (int i = 0; i < rows.Count; i++)
            {
                int sourceIdx = shuffledIndices[i];
                for (int pos = 0; pos < MlModelInput.ExpectedSampleCount; pos++)
                {
                    int featIdx = pos * MlModelInput.MetricsPerSample + metricIndex;
                    rows[i].Features[featIdx] = originalFeatures[sourceIdx][featIdx];
                }
            }
        }

        private static List<MlModelInput> BuildModelRows(IReadOnlyList<EvaluationSample> samples)
        {
            var rows = new List<MlModelInput>();

            foreach (var runGroup in samples.Where(s => s.Sample.Failure != FailureLabel.Unlabeled).GroupBy(s => s.RunId))
            {
                var ordered = runGroup.Select(s => s.Sample).OrderBy(s => s.Timestamp).ToList();
                
                for (int i = MlModelInput.ExpectedSampleCount - 1; i < ordered.Count; i++)
                {
                    int start = i - MlModelInput.ExpectedSampleCount + 1;
                    var input = MlModelInput.From(ordered.GetRange(start, MlModelInput.ExpectedSampleCount));
                    input.RawLabel = (float)ordered[i].Failure;
                    input.Weight = 1;
                    rows.Add(input);
                }
            }

            return rows;
        }

        private static void SaveHorizontalBarChart(string[] labels, double[] values, string title, string xLabel, string outputPath)
        {
            var pairs = labels.Select((l, i) => (Label: l, Value: values[i])).OrderBy(p => p.Value).ToArray();
            var sortedLabels = pairs.Select(p => p.Label).ToArray();
            var sortedValues = pairs.Select(p => p.Value).ToArray();

            var plt = new Plot();
            double[] positions = [.. Enumerable.Range(0, sortedValues.Length).Select(i => (double)i)];

            var bars = sortedValues.Select((value, i) => 
                new Bar { Position = i, Value = value, FillColor = Colors.SteelBlue, Orientation = Orientation.Horizontal}).ToList();

            plt.Add.Bars(bars);
            plt.Axes.Left.SetTicks(positions, sortedLabels);
            plt.Axes.Bottom.Label.Text = xLabel;
            plt.Axes.Title.Label.Text = title;
            plt.Axes.SetLimits(left: 0);
            plt.Grid.IsVisible = true;

            plt.SavePng(outputPath, 1000, 650);

            Console.WriteLine($"Saved plot: {outputPath}");
        }
    }
}