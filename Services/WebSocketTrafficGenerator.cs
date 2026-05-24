using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public class WebSocketTrafficGenerator : ITrafficGenerator
    {
        public string Protocol => "ws";

        public async Task<MetricRecord> SendRequestAsync(string target, TestConfig config)
        {
            var record = new MetricRecord();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var ws = new ClientWebSocket();
                var cts = new CancellationTokenSource(config.TimeoutMs);
                
                await ws.ConnectAsync(new Uri(target), cts.Token);

                var sendBuffer = Encoding.UTF8.GetBytes("ping");
                await ws.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, cts.Token);

                var receiveBuffer = new byte[1024];
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                
                record.ResponseSizeBytes = result.Count;
                record.StatusCode = 200;
                record.IsError = false;

                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
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