using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIIA.Models;
using UIIA.Reporting;

namespace UIIA.Services
{
    public class TestOrchestrator
    {
        private readonly Analyzer _analyzer;

        public TestOrchestrator(Analyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public async Task<string> ExecuteTestAsync(TestRunRequest request)
        {
            var config = new TestConfig
            {
                Target = request.Target,
                Protocol = request.Protocol,
                DurationSec = request.DurationSec,
                Mode = request.Mode,
                MinRps = request.MinRps,
                MaxRps = request.MaxRps,
                Connections = request.Connections,
                TimeoutMs = request.TimeoutMs,
                Headers = request.Headers,
                BodyFile = request.BodyFile,
                DatasetFile = request.DatasetFile
            };

            ITrafficGenerator generator = config.Protocol switch
            {
                "http" => new HttpTrafficGenerator(),
                "tcp" => new TcpTrafficGenerator(),
                "ws" => new WebSocketTrafficGenerator(),
                _ => throw new ArgumentException("Unsupported protocol")
            };

            var collector = new MetricsCollector();
            _ = Task.Run(collector.RunCollectorAsync);

            if (!string.IsNullOrWhiteSpace(config.DatasetFile) && File.Exists(config.DatasetFile))
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
                var endTime = DateTime.UtcNow.AddSeconds(config.DurationSec);
                var currentRps = (double)config.MinRps;
                var rampStep = (config.MaxRps - config.MinRps) / (double)config.DurationSec;

                while (DateTime.UtcNow < endTime)
                {
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

                    await Task.Delay(1000);
                }
            }

            collector.Writer.Complete();
            await Task.Delay(1000);

            var allMetrics = collector.GetRecords();
            var folderName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "results", folderName);
            var analysis = _analyzer.Analyze(allMetrics, resultsDir);
            var reportGen = new HtmlReportGenerator();
            reportGen.Generate(analysis, config, resultsDir);

            return $"/results/{folderName}/report.html";
        }
    }
}