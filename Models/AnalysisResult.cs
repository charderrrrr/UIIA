using System.Collections.Generic;
using ScottPlot;

namespace UIIA.Models
{
    public class AnalysisResult
    {
        public string FailurePointText { get; set; } = string.Empty;
        public string ProtocolComparisonText { get; set; } = string.Empty;
        public string ABTestText { get; set; } = string.Empty;
        public string RateLimitText { get; set; } = string.Empty;
        
        public List<Plot> Plots { get; set; } = new();
        public List<MetricRecord> RawMetrics { get; set; } = new();
        public string TestDirectory { get; set; } = string.Empty;
    }
}