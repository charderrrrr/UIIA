namespace UIIA.Models
{
    public class MetricRecord
    {
        public long Timestamp { get; set; }
        public double LatencyMs { get; set; }
        public int StatusCode { get; set; }
        public long ResponseSizeBytes { get; set; }
        public bool IsError { get; set; }
    }
}