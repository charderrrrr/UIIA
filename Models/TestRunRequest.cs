using System.Collections.Generic;

namespace UIIA.Models
{
    public class TestRunRequest
    {
        public string Target { get; set; } = string.Empty;
        public string Protocol { get; set; } = "http";
        public int DurationSec { get; set; } = 60;
        public string Mode { get; set; } = "constant";
        public string AttackMode { get; set; } = "standard";
        public int MinRps { get; set; } = 1;
        public int MaxRps { get; set; } = 100;
        public int Connections { get; set; } = 50;
        public int TimeoutMs { get; set; } = 3000;
        public int PacketSize { get; set; } = 256;
        public int PacketDelayMs { get; set; } = 1000;
        public Dictionary<string, string> Headers { get; set; } = new();
        public string BodyFile { get; set; } = string.Empty;
        public string DatasetFile { get; set; } = string.Empty;
        public string DatasetContent { get; set; } = string.Empty;
        public double TimeScale { get; set; } = 1.0;
    }
}