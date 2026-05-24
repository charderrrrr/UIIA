using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using UIIA.Models;

namespace UIIA.Services
{
    public class Analyzer
    {
        private const int CriticalLatencyMs = 5000;
        private const int MaxLatencyMs = 200;
        private const double MinRSquared = 0.3;

        public AnalysisResult Analyze(List<MetricRecord> metrics, string testDirectory)
        {
            var windowedData = CalculateWindowedData(metrics, 5);
            var rpsValues = windowedData.Select(w => w.Rps).ToList();
            var latencies = windowedData.Select(w => w.LatencyP50).ToList();

            var errorRate = metrics.Count > 0 ? (double)metrics.Count(m => m.IsError) / metrics.Count : 1.0;

            var regression = new LinearRegression();
            if (rpsValues.Count >= 2)
            {
                regression.Fit(rpsValues, latencies);
            }

            var result = new AnalysisResult
            {
                RawMetrics = metrics.Take(100).ToList(),
                TestDirectory = testDirectory,
                Plots = new List<Plot>()
            };

            if (errorRate > 0.9)
            {
                result.FailurePointText = "Более 90% запросов завершились ошибкой. Сервер недоступен или не отвечает на запросы. Точка отказа не может быть определена.";
                result.RateLimitText = "Сервер не обработал ни одного запроса успешно. Проверьте доступность цели и повторите тест.";
            }
            else if (rpsValues.Count < 2)
            {
                result.FailurePointText = "Недостаточно данных для анализа. Увеличьте длительность теста.";
                result.RateLimitText = "Недостаточно данных для расчёта лимитов.";
            }
            else if (regression.RSquared < MinRSquared)
            {
                result.FailurePointText = $"Модель регрессии слабая (R² = {regression.RSquared:F2}). Точка отказа не может быть надёжно определена.";
                if (regression.Slope > 0)
                {
                    var rps = (CriticalLatencyMs - regression.Intercept) / regression.Slope;
                    if (rps > 0)
                    {
                        result.FailurePointText += $" Приблизительная оценка: {rps:F0} RPS.";
                        result.RateLimitText = $"Приблизительный безопасный лимит: {(MaxLatencyMs - regression.Intercept) / regression.Slope:F0} RPS (низкая достоверность).";
                    }
                    else
                    {
                        result.RateLimitText = "Не удалось определить безопасный лимит из-за низкого качества модели.";
                    }
                }
                else
                {
                    result.RateLimitText = "Задержка не растёт с нагрузкой — сервер, вероятно, не достиг предела производительности в данном тесте.";
                }
            }
            else
            {
                var failureRps = (CriticalLatencyMs - regression.Intercept) / regression.Slope;
                if (failureRps > 0)
                {
                    result.FailurePointText = $"Сервер достигнет критической латентности {CriticalLatencyMs} мс при {failureRps:F0} RPS.";
                    var maxObservedRps = rpsValues.Max();
                    var headroom = (failureRps - maxObservedRps) / failureRps * 100;
                    result.FailurePointText += $" Максимальная наблюдавшаяся нагрузка: {maxObservedRps:F0} RPS (запас {headroom:F0}%).";
                }
                else
                {
                    result.FailurePointText = $"При текущем тренде сервер не достигнет критической латентности {CriticalLatencyMs} мс в обозримом диапазоне нагрузок.";
                }

                var safeRps = (MaxLatencyMs - regression.Intercept) / regression.Slope;
                if (safeRps > 0)
                {
                    result.RateLimitText = $"Для поддержания латентности ниже {MaxLatencyMs} мс рекомендуем ограничить RPS на уровне {safeRps:F0}. Достоверность модели: R² = {regression.RSquared:F2}.";
                }
                else
                {
                    result.RateLimitText = $"При любой нагрузке в пределах теста латентность остаётся ниже {MaxLatencyMs} мс. Сервер справляется с текущей нагрузкой.";
                }
            }

            result.Plots.Add(GenerateRpsTimePlot(windowedData));
            result.Plots.Add(GenerateLatencyTimePlot(windowedData));
            
            if (rpsValues.Count >= 2)
            {
                result.Plots.Add(GenerateRegressionPlot(rpsValues, latencies, regression));
            }

            return result;
        }

        private List<(double Time, double Rps, double LatencyP50)> CalculateWindowedData(List<MetricRecord> metrics, int windowSeconds)
        {
            if (!metrics.Any()) return new();

            var windowed = new List<(double, double, double)>();
            var startTime = metrics.Min(m => m.Timestamp);
            var endTime = metrics.Max(m => m.Timestamp);

            for (var current = startTime; current < endTime; current += windowSeconds * 1000)
            {
                var windowEnd = current + windowSeconds * 1000;
                var windowMetrics = metrics.Where(m => m.Timestamp >= current && m.Timestamp < windowEnd).ToList();

                if (windowMetrics.Any())
                {
                    var rps = windowMetrics.Count / (double)windowSeconds;
                    var sortedLatency = windowMetrics.Select(m => m.LatencyMs).OrderBy(l => l).ToList();
                    var p50 = sortedLatency[(int)(sortedLatency.Count * 0.5)];

                    windowed.Add((current, rps, p50));
                }
            }

            return windowed;
        }

        private Plot GenerateRpsTimePlot(List<(double Time, double Rps, double)> data)
        {
            var plot = new Plot();
            if (data.Any())
            {
                var times = data.Select(d => (double)(d.Time - data.First().Time) / 1000).ToArray();
                var rps = data.Select(d => d.Rps).ToArray();
                var scatter = plot.Add.Scatter(times, rps);
                scatter.Color = ScottPlot.Color.FromHex("#d16161");
                plot.Title("RPS от времени");
                plot.XLabel("Время (сек)");
                plot.YLabel("RPS");
            }
            return plot;
        }

        private Plot GenerateLatencyTimePlot(List<(double Time, double, double Latency)> data)
        {
            var plot = new Plot();
            if (data.Any())
            {
                var times = data.Select(d => (double)(d.Time - data.First().Time) / 1000).ToArray();
                var lat = data.Select(d => d.Latency).ToArray();
                var scatter = plot.Add.Scatter(times, lat);
                scatter.Color = ScottPlot.Color.FromHex("#d16161");
                plot.Title("P50 задержка от времени");
                plot.XLabel("Время (сек)");
                plot.YLabel("Задержка (мс)");
            }
            return plot;
        }

        private Plot GenerateRegressionPlot(List<double> rps, List<double> latency, LinearRegression regression)
        {
            var plot = new Plot();
            if (rps.Any())
            {
                var scatter = plot.Add.Scatter(rps.ToArray(), latency.ToArray());
                scatter.Color = ScottPlot.Color.FromHex("#d16161");
                var lineY = rps.Select(x => regression.Predict(x)).ToArray();
                var line = plot.Add.Scatter(rps.ToArray(), lineY);
                line.Color = ScottPlot.Color.FromHex("#ff7675");
                plot.Title($"Регрессия (R² = {regression.RSquared:F2})");
                plot.XLabel("RPS");
                plot.YLabel("Задержка (мс)");
            }
            return plot;
        }
    }
}