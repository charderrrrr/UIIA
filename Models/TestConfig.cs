using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UIIA.Models
{
    public class TestConfig
    {
        [JsonPropertyName("target")]
        public string Target { get; set; } = string.Empty;

        [JsonPropertyName("protocol")]
        public string Protocol { get; set; } = "http";

        [JsonPropertyName("duration_sec")]
        public int DurationSec { get; set; } = 60;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "constant";

        [JsonPropertyName("attack_mode")]
        public string AttackMode { get; set; } = "standard";

        [JsonPropertyName("min_rps")]
        public int MinRps { get; set; } = 1;

        [JsonPropertyName("max_rps")]
        public int MaxRps { get; set; } = 100;

        [JsonPropertyName("connections")]
        public int Connections { get; set; } = 50;

        [JsonPropertyName("timeout_ms")]
        public int TimeoutMs { get; set; } = 3000;

        [JsonPropertyName("packet_size")]
        public int PacketSize { get; set; } = 256;

        [JsonPropertyName("packet_delay_ms")]
        public int PacketDelayMs { get; set; } = 1000;

        [JsonPropertyName("dataset_file")]
        public string DatasetFile { get; set; } = string.Empty;

        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new();

        [JsonPropertyName("body_file")]
        public string BodyFile { get; set; } = string.Empty;
    }
}