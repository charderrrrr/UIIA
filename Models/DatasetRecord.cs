using CsvHelper.Configuration.Attributes;

namespace UIIA.Models
{
    public class DatasetRecord
    {
        [Name("time_ms")]
        public long TimeMs { get; set; }

        [Name("method")]
        public string Method { get; set; } = "GET";

        [Name("endpoint")]
        public string Endpoint { get; set; } = "/";

        [Name("body_file")]
        public string BodyFile { get; set; } = string.Empty;
    }
}