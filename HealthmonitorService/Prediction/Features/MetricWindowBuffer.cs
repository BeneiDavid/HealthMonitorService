using HealthMonitorService.Model;

namespace HealthMonitorService.Prediction.Features
{
    public class MetricWindowBuffer(TimeSpan windowSize)
    {
        private readonly Queue<MetricSample> _samples = new();
        private readonly TimeSpan _windowSize = windowSize;

        public void Add(MetricSample sample)
        {
            _samples.Enqueue(sample);

            while (_samples.Count > 0 && sample.Timestamp - _samples.Peek().Timestamp > _windowSize)
            {
                _samples.Dequeue();
            }
        }

        public IReadOnlyList<MetricSample> GetSamples()
        {
            return [.. _samples];
        }

        public bool IsReady(int minimumSamples)
        {
            return _samples.Count >= minimumSamples;
        }
    }
}
