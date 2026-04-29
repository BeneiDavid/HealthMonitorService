using HealthMonitorService.Model;

namespace HealthMonitorService.Evaluation.Evaluation
{
    public readonly record struct EvaluationSample
    {
        public string RunId { get; init; }
        public MetricSample Sample { get; init; }
    }
}
