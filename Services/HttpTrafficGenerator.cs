using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public class HttpTrafficGenerator : ITrafficGenerator
    {
        private readonly HttpClient _httpClient;
        public string Protocol => "http";

        public HttpTrafficGenerator()
        {
            _httpClient = new HttpClient();
        }

        public async Task<MetricRecord> SendRequestAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(new HttpMethod("GET"), target);

                if (config.Headers != null)
                {
                    foreach (var header in config.Headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(config.BodyFile) && File.Exists(config.BodyFile))
                {
                    var content = await File.ReadAllTextAsync(config.BodyFile);
                    request.Content = new StringContent(content, Encoding.UTF8, "application/json");
                }

                using var cts = new System.Threading.CancellationTokenSource(config.TimeoutMs);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);

                record.StatusCode = (int)response.StatusCode;
                if (response.Content != null)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    record.ResponseSizeBytes = bytes.Length;
                }
                record.IsError = !response.IsSuccessStatusCode;
            }
            catch
            {
                record.IsError = true;
                record.StatusCode = 0;
            }

            stopwatch.Stop();
            record.LatencyMs = stopwatch.ElapsedMilliseconds;
            record.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return record;
        }
    }
}