using HealthMonitorService.Model;
using Microsoft.ML.Data;

namespace HealthMonitorService.Prediction.MachineLearning
{
    public class MlModelInput()
    {
        // Must match the window size used during ML training: 60 seconds window / 5 second sample interval = 12 samples
        public const int ExpectedSampleCount = 12;
        public const int MetricsPerSample = 11;
        public const int FeatureCount = ExpectedSampleCount * MetricsPerSample;

        [ColumnName("RawLabel")]
        public float RawLabel { get; set; }

        [ColumnName("Weight")]
        public float Weight { get; set; }

        [VectorType(FeatureCount)]
        public float[] Features { get; set; } = new float[FeatureCount];

        public static MlModelInput From(IReadOnlyList<MetricSample> window)
        {
            float[] features = new float[FeatureCount];

            List<MetricSample> samples = [.. window.TakeLast(ExpectedSampleCount)];

            int startIndex = ExpectedSampleCount - samples.Count;

            for (int i = 0; i < samples.Count; i++)
            {
                int sampleIndex = startIndex + i;
                int offset = sampleIndex * MetricsPerSample;

                MetricSample sample = samples[i];

                features[offset + 0] = (float)sample.CpuUsagePercent;
                features[offset + 1] = sample.CpuQueueLength;
                features[offset + 2] = (float)sample.LoadPerCore;
                features[offset + 3] = (float)sample.MemoryUsagePercent;
                features[offset + 4] = (float)sample.SecondaryMemoryUsagePercent;
                features[offset + 5] = (float)sample.IoWaitPercent;
                features[offset + 6] = (float)sample.DiskUsagePercent;
                features[offset + 7] = (float)sample.DiskLatencyMs;
                features[offset + 8] = sample.ProcessCount;
                features[offset + 9] = sample.NetworkAvailable ? 1f : 0f;
                features[offset + 10] = (float)sample.PacketLossPercent;
            }

            return new MlModelInput
            {
                Features = features
            };
        }
    }
}