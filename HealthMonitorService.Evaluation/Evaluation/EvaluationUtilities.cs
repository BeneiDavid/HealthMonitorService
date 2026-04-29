using HealthMonitorService.Model;
using HealthMonitorService.Prediction;

namespace HealthMonitorService.Evaluation.Evaluation
{
    public static class EvaluationUtilities
    {
        public static readonly string[] Labels =
        [
            "Normal",
            "Elevated",
            "Critical"
        ];

        public static RiskLevel ToRiskLevel(FailureLabel label)
        {
            return label switch
            {
                FailureLabel.Normal => RiskLevel.Normal,
                FailureLabel.NearFailure => RiskLevel.ElevatedRisk,
                FailureLabel.Failure => RiskLevel.CriticalRisk,
                FailureLabel.Unlabeled => throw new InvalidOperationException("Evaluation data contains an unlabeled sample."),
                _ => throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown label")
            };
        }

        public static void PrintClassDistribution(IEnumerable<(RiskLevel Label, int Count)> distribution, string name)
        {
            Console.WriteLine($"--- {name} class distribution ---");

            foreach ((RiskLevel label, int count) in distribution.OrderBy(x => x.Label))
            {
                Console.WriteLine($"{label,-15}: {count} samples");
            }

            Console.WriteLine();
        }
        public static void PrintConfusionMatrix(int[,] matrix)
        {
            Console.WriteLine("Confusion matrix:");
            Console.Write($"{"Actual \\ Predicted",-22}");

            for (int i = 0; i < Labels.Length; i++)
            {
                Console.Write($"{Labels[i],10}");
            }

            Console.WriteLine();

            for (int actual = 0; actual < Labels.Length; actual++)
            {
                Console.Write($"{Labels[actual],-22}");

                for (int predicted = 0; predicted < Labels.Length; predicted++)
                {
                    Console.Write($"{matrix[actual, predicted],10}");
                }

                Console.WriteLine();
            }

            Console.WriteLine();
        }

        public static void PrintClassMetrics(int[,] matrix)
        {
            for (int label = 0; label < Labels.Length; label++)
            {
                int truePositive = matrix[label, label];
                int falsePositive = GetColumnSum(matrix, label) - truePositive;
                int falseNegative = GetRowSum(matrix, label) - truePositive;

                double precision = DivideSafely(truePositive, truePositive + falsePositive);
                double recall = DivideSafely(truePositive, truePositive + falseNegative);
                double f1 = CalculateF1(precision, recall);

                Console.WriteLine($"{Labels[label],-10} Precision={precision:F3}, Recall={recall:F3}, F1={f1:F3}");
            }
            Console.WriteLine();
        }

        private static int GetRowSum(int[,] matrix, int row) => Enumerable.Range(0, Labels.Length).Sum(col => matrix[row, col]);

        private static int GetColumnSum(int[,] matrix, int col) => Enumerable.Range(0, Labels.Length).Sum(row => matrix[row, col]);

        private static double CalculateF1(double precision, double recall) => DivideSafely(2 * precision * recall, precision + recall);

        private static double DivideSafely(double numerator, double denominator) => denominator == 0 ? 0 : numerator / denominator;
    }
}