using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UIIA.Models;
using UIIA.Reporting;
using System.Text;

namespace UIIA.Services
{
    public class TestOrchestrator
    {
        private readonly Analyzer _analyzer;
        private static readonly SemaphoreSlim _testLock = new SemaphoreSlim(1, 1);
        private static readonly HttpClient _sharedHttpClient;

        public bool IsTestRunning => _testLock.CurrentCount == 0;

        static TestOrchestrator()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 3,
                UseCookies = false
            };
            _sharedHttpClient = new HttpClient(handler);
        }

        public TestOrchestrator(Analyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public async Task<string> ExecuteTestAsync(TestRunRequest request)
        {
            if (!await _testLock.WaitAsync(TimeSpan.Zero))
            {
                throw new InvalidOperationException("Тест уже выполняется. Дождитесь завершения текущего теста.");
            }

            try
            {
                return await RunTestAsync(request);
            }
            finally
            {
                _testLock.Release();
            }
        }

        private async Task<string> RunTestAsync(TestRunRequest request)
        {
            var config = new TestConfig
            {
                Target = request.Target,
                Protocol = request.Protocol,
                DurationSec = request.DurationSec,
                Mode = request.Mode,
                AttackMode = request.AttackMode,
                MinRps = request.MinRps,
                MaxRps = request.MaxRps,
                Connections = request.Connections,
                TimeoutMs = request.TimeoutMs,
                PacketSize = request.PacketSize,
                PacketDelayMs = request.PacketDelayMs,
                Headers = request.Headers,
                BodyFile = request.BodyFile,
                DatasetFile = request.DatasetFile
            };

            if (config.MaxRps > 10_000)
            {
                throw new ArgumentException("Максимальный RPS не может превышать 10 000.");
            }

            ITrafficGenerator generator = config.Protocol switch
            {
                "http" => new HttpTrafficGenerator(_sharedHttpClient),
                "tcp" => new TcpTrafficGenerator(),
                "ws" => new WebSocketTrafficGenerator(),
                _ => throw new ArgumentException("Unsupported protocol")
            };

            var collector = new MetricsCollector();
            var collectorTask = Task.Run(() => collector.RunCollectorAsync());

            try
            {
                if (!string.IsNullOrWhiteSpace(request.DatasetContent))
                {
                    var tempDatasetPath = Path.Combine(Path.GetTempPath(), $"uiia_dataset_{Guid.NewGuid()}.csv");
                    await File.WriteAllTextAsync(tempDatasetPath, request.DatasetContent, Encoding.UTF8);
                    config.DatasetFile = tempDatasetPath;

                    try
                    {
                        var player = new DatasetPlayer();
                        var dataset = player.Load(config.DatasetFile);
                        var metrics = await player.PlayAsync(dataset, generator, config.Target, config, request.TimeScale);
                        foreach (var metric in metrics)
                        {
                            await collector.Writer.WriteAsync(metric);
                        }
                    }
                    finally
                    {
                        try { File.Delete(tempDatasetPath); } catch { }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(config.DatasetFile) && File.Exists(config.DatasetFile))
                {
                    var player = new DatasetPlayer();
                    var dataset = player.Load(config.DatasetFile);
                    var metrics = await player.PlayAsync(dataset, generator, config.Target, config, request.TimeScale);
                    foreach (var metric in metrics)
                    {
                        await collector.Writer.WriteAsync(metric);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(config.DatasetFile))
                    {
                        System.Diagnostics.Debug.WriteLine($"Датасет не найден: {config.DatasetFile}. Используется синтетическая нагрузка.");
                    }

                    var endTime = DateTime.UtcNow.AddSeconds(config.DurationSec);
                    var currentRps = (double)config.MinRps;
                    var rampStep = (config.MaxRps - config.MinRps) / (double)config.DurationSec;

                    while (DateTime.UtcNow < endTime)
                    {
                        var iterationStart = DateTime.UtcNow;
                        var tasks = Enumerable.Range(0, (int)currentRps).Select(async _ =>
                        {
                            var metric = await generator.SendRequestAsync(config.Target, config);
                            await collector.Writer.WriteAsync(metric);
                        });

                        await Task.WhenAll(tasks);

                        if (config.Mode == "linear_ramp")
                        {
                            currentRps += rampStep;
                        }

                        var elapsed = (DateTime.UtcNow - iterationStart).TotalMilliseconds;
                        var delay = Math.Max(0, 1000 - elapsed);
                        await Task.Delay((int)delay);
                    }
                }
            }
            finally
            {
                if (generator is TcpTrafficGenerator tcpGen)
                {
                    tcpGen.CancelSlowlorisConnections();
                }
            }

            collector.Writer.Complete();
            await collectorTask;

            var allMetrics = collector.GetRecords();
            var folderName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff");
            var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "results", folderName);
            var analysis = _analyzer.Analyze(allMetrics, resultsDir);
            var reportGen = new HtmlReportGenerator();
            reportGen.Generate(analysis, config, resultsDir);

            return $"/results/{folderName}/report.html";
        }
    }
}