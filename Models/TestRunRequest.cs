using System.Collections.Generic;

namespace UIIA.Models
{
    public class TestRunRequest
    {
        public string Target { get; set; } = string.Empty;
        public string Protocol { get; set; } = "http";
        public int DurationSec { get; set; } = 60;
        public string Mode { get; set; } = "constant";
        public int MinRps { get; set; } = 1;
        public int MaxRps { get; set; } = 100;
        public int Connections { get; set; } = 50;
        public int TimeoutMs { get; set; } = 3000;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string BodyFile { get; set; } = string.Empty;
        public string DatasetFile { get; set; } = string.Empty;
        public double TimeScale { get; set; } = 1.0;
    }
}