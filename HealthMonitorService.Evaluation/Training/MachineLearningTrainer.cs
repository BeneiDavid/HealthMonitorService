using HealthMonitorService.Evaluation.Evaluation;
using HealthMonitorService.Model;
using HealthMonitorService.Prediction;
using HealthMonitorService.Prediction.MachineLearning;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;

namespace HealthMonitorService.Evaluation.Training
{
    public class MachineLearningTrainer
    {
        private readonly record struct ClassWeights(float Normal, float Elevated, float Critical);

        private readonly MLContext _mlContext = new(seed: 42);

        public async Task TrainAsync(string trainingCsvPath, string testCsvPath, string randomForestOutputPath, string logisticRegressionOutputPath)
        {
            var trainingSamples = await new EvaluationCsvReader(trainingCsvPath).ReadAllAsync();
            var testSamples = await new EvaluationCsvReader(testCsvPath).ReadAllAsync();

            // Critical failures are rare, so weights were added to tune the models
            var randomForestWeights = new ClassWeights(Normal: 1, Elevated: 2, Critical: 50);
            var logisticRegressionWeights = new ClassWeights(Normal: 1, Elevated: 20, Critical: 90);

            var randomForestRows = BuildModelRows(trainingSamples, randomForestWeights);
            var logisticRegressionRows = BuildModelRows(trainingSamples, logisticRegressionWeights);
            var testRows = BuildModelRows(testSamples, new ClassWeights(1, 1, 1));

            if (randomForestRows.Count == 0 || logisticRegressionRows.Count == 0 || testRows.Count == 0)
            {
                throw new InvalidOperationException("No usable training or test rows were produced.");
            }

            PrintDistribution(randomForestRows, "Training Set");
            PrintDistribution(testRows, "Test Set");

            var testData = _mlContext.Data.LoadFromEnumerable(testRows);

            TrainAndSaveModel(
                trainData: _mlContext.Data.LoadFromEnumerable(randomForestRows),
                testData: testData,
                trainer: CreateRandomForestTrainer(),
                outputPath: randomForestOutputPath,
                modelName: "Random Forest",
                normalizeFeatures: false);

            TrainAndSaveModel(
                trainData: _mlContext.Data.LoadFromEnumerable(logisticRegressionRows),
                testData: testData,
                trainer: CreateLogisticRegressionTrainer(),
                outputPath: logisticRegressionOutputPath,
                modelName: "Logistic Regression",
                normalizeFeatures: true);
        }

        private OneVersusAllTrainer CreateRandomForestTrainer()
        {
            return _mlContext.MulticlassClassification.Trainers.OneVersusAll(
                _mlContext.BinaryClassification.Trainers.FastForest(
                labelColumnName: "LabelKey",
                featureColumnName: nameof(MlModelInput.Features),
                exampleWeightColumnName: nameof(MlModelInput.Weight),
                numberOfLeaves: 20,
                numberOfTrees: 350,
                minimumExampleCountPerLeaf: 10),
                labelColumnName: "LabelKey");
        }

        private LbfgsMaximumEntropyMulticlassTrainer CreateLogisticRegressionTrainer()
        {
            var options = new LbfgsMaximumEntropyMulticlassTrainer.Options
            {
                LabelColumnName = "LabelKey",
                FeatureColumnName = nameof(MlModelInput.Features),
                ExampleWeightColumnName = nameof(MlModelInput.Weight)
            };

            return _mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(options);
        }

        private void TrainAndSaveModel(IDataView trainData, IDataView testData, IEstimator<ITransformer> trainer, string outputPath, string modelName, bool normalizeFeatures)
        {
            IEstimator<ITransformer> pipeline = _mlContext.Transforms.Conversion.MapValueToKey("LabelKey", nameof(MlModelInput.RawLabel));

            if (normalizeFeatures)
            {
                pipeline = pipeline.Append(_mlContext.Transforms.NormalizeMinMax(nameof(MlModelInput.Features)));
            }

            var fullPipeline = pipeline
                .Append(trainer)
                .Append(_mlContext.Transforms.Conversion
                .MapKeyToValue(outputColumnName: "PredictedRiskLevel", inputColumnName: "PredictedLabel"));

            var model = fullPipeline.Fit(trainData);
            var predictions = model.Transform(testData);

            var metrics = _mlContext.MulticlassClassification.Evaluate(predictions, labelColumnName: "LabelKey", predictedLabelColumnName: "PredictedLabel");

            Console.WriteLine($"--- Evaluation result for {modelName} ---");
            Console.WriteLine($"Macro-Accuracy: {metrics.MacroAccuracy:F3}");
            Console.WriteLine($"Micro-Accuracy: {metrics.MicroAccuracy:F3}\n");

            var confusionMatrix = ToArray(metrics.ConfusionMatrix);

            EvaluationUtilities.PrintConfusionMatrix(confusionMatrix);
            EvaluationUtilities.PrintClassMetrics(confusionMatrix);

            _mlContext.Model.Save(model, trainData.Schema, outputPath);
            Console.WriteLine($"Model {modelName} saved to: {outputPath}");
        }

        private static List<MlModelInput> BuildModelRows(IReadOnlyList<EvaluationSample> samples, ClassWeights weights)
        {
            var rows = new List<MlModelInput>();

            var labeledSamples = samples.Where(sample => sample.Sample.Failure != FailureLabel.Unlabeled);

            foreach (var run in labeledSamples.GroupBy(sample => sample.RunId))
            {
                var orderedSamples = run
                    .Select(sample => sample.Sample)
                    .OrderBy(sample => sample.Timestamp)
                    .ToList();

                for (int index = MlModelInput.ExpectedSampleCount - 1; index < orderedSamples.Count; index++)
                {
                    int windowStart = index - MlModelInput.ExpectedSampleCount + 1;
                    var input = MlModelInput.From(orderedSamples.GetRange(windowStart, MlModelInput.ExpectedSampleCount));

                    input.RawLabel = (float)orderedSamples[index].Failure;
                    input.Weight = GetWeight(input.RawLabel, weights);

                    rows.Add(input);
                }
            }

            return rows;
        }

        private static float GetWeight(float label, ClassWeights weights)
        {
            return label switch
            {
                2f => weights.Critical,
                1f => weights.Elevated,
                _ => weights.Normal
            };
        }

        private static int[,] ToArray(ConfusionMatrix confusionMatrix)
        {
            var matrix = new int[3, 3];

            for (int actual = 0; actual < 3; actual++)
            {
                for (int predicted = 0; predicted < 3; predicted++)
                {
                    matrix[actual, predicted] = (int)confusionMatrix.Counts[actual][predicted];
                }
            }

            return matrix;
        }

        private static void PrintDistribution(List<MlModelInput> rows, string title)
        {
            EvaluationUtilities.PrintClassDistribution(rows.GroupBy(row => (RiskLevel)(int)row.RawLabel)
                                                            .Select(group => (group.Key, group.Count())), title);
        }
    }
}