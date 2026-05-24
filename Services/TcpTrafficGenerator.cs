using System;
using System.Diagnostics;
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
    }
}