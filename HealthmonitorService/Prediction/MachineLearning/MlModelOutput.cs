using Microsoft.ML.Data;

namespace HealthMonitorService.Prediction.MachineLearning
{
    public class MlModelOutput
    {
        [ColumnName("PredictedLabel")]
        public uint PredictedLabel { get; set; }

        public float[]? Score { get; set; }
    }
}
