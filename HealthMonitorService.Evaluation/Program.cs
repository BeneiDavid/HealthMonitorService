using HealthMonitorService.Evaluation.Evaluation;
using HealthMonitorService.Evaluation.Evaluation.Plots;
using HealthMonitorService.Evaluation.Training;
using HealthMonitorService.Options;
using HealthMonitorService.Prediction;
using HealthMonitorService.Prediction.Heuristics;
using HealthMonitorService.Prediction.MachineLearning;

if (args.Length == 0)
{
    PrintUsageInfo();
    return;
}

string mode = args[0].ToLowerInvariant();

if (mode == "heuristic")
{
    if (args.Length != 2)
    {
        PrintUsageInfo();
        return;
    }

    string filePath = args[1];

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"File not found: {filePath}");
        return;
    }

    MonitoringOptions options = new()
    {
        SampleIntervalSeconds = 5,
        PredictionWindowSeconds = 60
    };

    HeuristicEvaluator evaluator = new();

    await evaluator.EvaluateFileAsync(filePath, options);
}
else if (mode == "ml")
{
    if (args.Length != 5)
    {
        PrintUsageInfo();
        return;
    }

    string trainingCsvPath = args[1];
    string testCsvPath = args[2];
    string randomForestModelPath = args[3];
    string logisticRegressionModelPath = args[4];

    if (!File.Exists(trainingCsvPath))
    {
        Console.WriteLine($"Training file not found: {trainingCsvPath}");
        return;
    }

    if (!File.Exists(testCsvPath))
    {
        Console.WriteLine($"Test file not found: {testCsvPath}");
        return;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(randomForestModelPath)!);
    Directory.CreateDirectory(Path.GetDirectoryName(logisticRegressionModelPath)!);

    MachineLearningTrainer trainer = new();

    await trainer.TrainAsync(trainingCsvPath, testCsvPath, randomForestModelPath, logisticRegressionModelPath);
}
else if (mode == "leadtime")
{
    if (args.Length < 3)
    {
        PrintUsageInfo();
        return;
    }

    string predictorType = args[1].ToLowerInvariant();
    string filePath = args[2];

    if (!File.Exists(filePath))
    {
        Console.WriteLine($"File not found: {filePath}");
        return;
    }

    MonitoringOptions options = new()
    {
        SampleIntervalSeconds = 5,
        PredictionWindowSeconds = 60
    };

    var (predictor, model) = predictorType switch
    {
        "heuristic" => ((IRiskPredictor)new HeuristicRiskPredictor(), "Heuristic"),

        "rf" when args.Length == 4 && File.Exists(args[3]) => (new TrainedMachineLearningModel(args[3], "Random Forest"), "Random Forest"),

        "lr" when args.Length == 4 && File.Exists(args[3]) => (new TrainedMachineLearningModel(args[3], "Logistic Regression"), "Logistic Regression"),

        "rf" or "lr" => throw new InvalidOperationException("Model file is missing."),

        _ => throw new InvalidOperationException("Unknown lead-time predictor.")
    };

    Console.WriteLine($"--- Model: {model} ---\n");
    LeadTimeEvaluator evaluator = new(predictor);
    await evaluator.EvaluateFileAsync(filePath, options);
}
else if(mode == "plot")
{
    if (args.Length < 4)
    {
        PrintUsageInfo();
        return;
    }

    await new FeatureImportancePlotter().CreatePlotsAsync(args[1], args[2], args[3]);
}
else
{
    PrintUsageInfo();
    return;
}

static void PrintUsageInfo()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("Heuristic: dotnet run --project HealthMonitorService.Evaluation -- heuristic \"C:\\path\\to\\evaluation.csv\"");
    Console.WriteLine("Machine Learning: dotnet run --project HealthMonitorService.Evaluation -- ml \"C:\\path\\to\\train.csv\" \"C:\\path\\to\\test.csv\" \"C:\\models\\random_forest.zip\" \"C:\\models\\logistic_regression.zip\"");
    Console.WriteLine("Lead time:");
    Console.WriteLine("  dotnet run --project HealthMonitorService.Evaluation -- leadtime heuristic <csvPath>");
    Console.WriteLine("  dotnet run --project HealthMonitorService.Evaluation -- leadtime rf <csvPath> <modelPath>");
    Console.WriteLine("  dotnet run --project HealthMonitorService.Evaluation -- leadtime lr <csvPath> <modelPath>");
    Console.WriteLine("Plots:");
    Console.WriteLine("  dotnet run --project HealthMonitorService.Evaluation -- plot \"C:\\path\\to\\test.csv\" \"C:\\models\\random_forest.zip\" \"C:\\models\\logistic_regression.zip\"");
}

