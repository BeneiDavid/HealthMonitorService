using HealthMonitorService.Prediction;

namespace HealthMonitorService.Evaluation.Evaluation
{
    public class EvaluationMetrics
    {
        private static readonly string[] Labels = ["Normal", "Elevated", "Critical"];
        private readonly int[,] _confusionMatrix = new int[Labels.Length, Labels.Length];   // Rows: actual, Columns: predicted

        public int Total { get; private set; }

        public void Add(RiskLevel actual, RiskLevel predicted)
        {
            _confusionMatrix[(int)actual, (int)predicted]++;
            Total++;
        }

        public void Merge(EvaluationMetrics other)
        {
            for (int actual = 0; actual < Labels.Length; actual++)
            {
                for (int predicted = 0; predicted < Labels.Length; predicted++)
                {
                    _confusionMatrix[actual, predicted] += other._confusionMatrix[actual, predicted];
                }
            }
            Total += other.Total;
        }

        public void Print()
        {
            Console.WriteLine($"Evaluated windows: {Total}");
            EvaluationUtilities.PrintConfusionMatrix(_confusionMatrix);
            Console.WriteLine();
            EvaluationUtilities.PrintClassMetrics(_confusionMatrix);
        }
    }
}
