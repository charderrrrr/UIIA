using System;
using System.IO;
using System.Text;
using System.Text.Json;
using UIIA.Models;

namespace UIIA.Reporting
{
    public class HtmlReportGenerator
    {
        public void Generate(AnalysisResult analysis, TestConfig config, string outputPath)
        {
            var html = new StringBuilder();

            html.Append("<!DOCTYPE html>");
            html.Append("<html lang=\"ru\">");
            html.Append("<head>");
            html.Append("<meta charset=\"utf-8\">");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.Append("<title>Отчёт нагрузочного тестирования</title>");
            html.Append("<style>");
            html.Append(GetStyles());
            html.Append("</style>");
            html.Append("</head>");
            html.Append("<body>");

            html.Append("<div class=\"container\">");
            html.Append("<h1>UIIA UIIA REPORT</h1>");

            html.Append("<div class=\"section\">");
            html.Append("<h2>Параметры теста</h2>");
            html.Append("<div class=\"params-grid\">");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Цель</span><span class=\"param-value\">{EscapeHtml(config.Target)}</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Протокол</span><span class=\"param-value\">{EscapeHtml(config.Protocol.ToUpper())}</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Длительность</span><span class=\"param-value\">{config.DurationSec} сек</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Режим</span><span class=\"param-value\">{GetModeName(config.Mode)}</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Мин. RPS</span><span class=\"param-value\">{config.MinRps}</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Макс. RPS</span><span class=\"param-value\">{config.MaxRps}</span></div>");
            html.Append($"<div class=\"param\"><span class=\"param-label\">Таймаут</span><span class=\"param-value\">{config.TimeoutMs} мс</span></div>");
            html.Append("</div>");
            html.Append("</div>");

            html.Append("<div class=\"section\">");
            html.Append("<h2>Анализ</h2>");

            if (!string.IsNullOrEmpty(analysis.FailurePointText))
            {
                html.Append("<div class=\"insight\">");
                html.Append("<h3>Точка отказа</h3>");
                html.Append($"<p>{EscapeHtml(analysis.FailurePointText)}</p>");
                html.Append("</div>");
            }

            if (!string.IsNullOrEmpty(analysis.RateLimitText))
            {
                html.Append("<div class=\"insight\">");
                html.Append("<h3>Рекомендуемый лимит RPS</h3>");
                html.Append($"<p>{EscapeHtml(analysis.RateLimitText)}</p>");
                html.Append("</div>");
            }

            if (!string.IsNullOrEmpty(analysis.ProtocolComparisonText))
            {
                html.Append("<div class=\"insight\">");
                html.Append("<h3>Сравнение протоколов</h3>");
                html.Append($"<p>{EscapeHtml(analysis.ProtocolComparisonText)}</p>");
                html.Append("</div>");
            }

            if (!string.IsNullOrEmpty(analysis.ABTestText))
            {
                html.Append("<div class=\"insight\">");
                html.Append("<h3>A/B сравнение</h3>");
                html.Append($"<p>{EscapeHtml(analysis.ABTestText)}</p>");
                html.Append("</div>");
            }
            html.Append("</div>");

            html.Append("<div class=\"section\">");
            html.Append("<h2>Графики</h2>");
            html.Append("<div class=\"plots\">");
            foreach (var plot in analysis.Plots)
            {
                var bytes = plot.GetImageBytes(800, 600, ScottPlot.ImageFormat.Png);
                var base64 = Convert.ToBase64String(bytes);
                html.Append($"<img class=\"plot-img\" src=\"data:image/png;base64,{base64}\" alt=\"График\">");
            }
            html.Append("</div>");
            html.Append("</div>");

            html.Append("<div class=\"section\">");
            html.Append("<h2>Метрики (первые 100 записей)</h2>");
            html.Append("<div class=\"table-wrapper\">");
            html.Append("<table>");
            html.Append("<thead><tr><th>Время</th><th>Задержка (мс)</th><th>Статус</th><th>Ошибка</th></tr></thead>");
            html.Append("<tbody>");
            foreach (var m in analysis.RawMetrics)
            {
                var timeStr = DateTimeOffset.FromUnixTimeMilliseconds(m.Timestamp).ToLocalTime().ToString("HH:mm:ss.fff");
                html.Append("<tr>");
                html.Append($"<td>{timeStr}</td>");
                html.Append($"<td>{m.LatencyMs:F1}</td>");
                html.Append($"<td>{m.StatusCode}</td>");
                html.Append($"<td>{(m.IsError ? "Да" : "Нет")}</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody>");
            html.Append("</table>");
            html.Append("</div>");
            html.Append("</div>");

            html.Append("</div>");
            html.Append("</body>");
            html.Append("</html>");

            Directory.CreateDirectory(outputPath);
            File.WriteAllText(Path.Combine(outputPath, "report.html"), html.ToString());

            var json = JsonSerializer.Serialize(analysis.RawMetrics, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputPath, "raw_logs.json"), json);
        }

        private string GetStyles()
        {
            return @"
                @font-face {
                    font-family: 'Medium';
                    src: url('/font/Benzin-Bold.ttf') format('truetype');
                }

                @font-face {
                    font-family: 'Regular';
                    src: url('/font/Benzin-Regular.ttf') format('truetype');
                }

                * {
                    margin: 0;
                    padding: 0;
                    box-sizing: border-box;
                }

                body {
                    font-family: 'Regular', 'Segoe UI', system-ui, sans-serif;
                    background: rgb(20, 7, 7);
                    color: rgb(235, 180, 180);
                    min-height: 100vh;
                    padding: 40px 20px;
                }

                .container {
                    max-width: 900px;
                    margin: 0 auto;
                }

                h1 {
                    font-family: 'Medium', 'Segoe UI', system-ui, sans-serif;
                    font-size: 28px;
                    font-weight: 700;
                    margin-bottom: 32px;
                    color: rgb(240, 200, 200);
                    text-align: center;
                }

                .section {
                    background: rgba(209, 97, 97, 0.05);
                    border: 1px solid rgba(209, 97, 97, 0.15);
                    border-radius: 12px;
                    padding: 28px;
                    margin-bottom: 20px;
                }

                .section h2 {
                    font-family: 'Medium', 'Segoe UI', system-ui, sans-serif;
                    font-size: 18px;
                    font-weight: 600;
                    margin-bottom: 20px;
                    color: rgb(240, 200, 200);
                }

                .params-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
                    gap: 16px;
                }

                .param {
                    display: flex;
                    flex-direction: column;
                    gap: 4px;
                }

                .param-label {
                    font-family: 'Medium', 'Segoe UI', system-ui, sans-serif;
                    font-size: 12px;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    color: rgb(180, 130, 130);
                }

                .param-value {
                    font-size: 16px;
                    color: rgb(235, 180, 180);
                }

                .insight {
                    background: rgba(209, 97, 97, 0.03);
                    border: 1px solid rgba(209, 97, 97, 0.1);
                    border-radius: 8px;
                    padding: 16px;
                    margin-bottom: 12px;
                }

                .insight:last-child {
                    margin-bottom: 0;
                }

                .insight h3 {
                    font-family: 'Medium', 'Segoe UI', system-ui, sans-serif;
                    font-size: 14px;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                    color: rgb(200, 150, 150);
                    margin-bottom: 8px;
                }

                .insight p {
                    font-size: 15px;
                    line-height: 1.6;
                    color: rgb(235, 180, 180);
                }

                .plots {
                    display: flex;
                    flex-direction: column;
                    gap: 20px;
                }

                .plot-img {
                    width: 100%;
                    border-radius: 8px;
                    border: 1px solid rgba(209, 97, 97, 0.15);
                }

                .table-wrapper {
                    overflow-x: auto;
                }

                table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 14px;
                }

                thead th {
                    font-family: 'Medium', 'Segoe UI', system-ui, sans-serif;
                    text-align: left;
                    padding: 12px 16px;
                    border-bottom: 1px solid rgba(209, 97, 97, 0.3);
                    color: rgb(200, 150, 150);
                    text-transform: uppercase;
                    font-size: 12px;
                    letter-spacing: 0.5px;
                }

                tbody td {
                    padding: 10px 16px;
                    border-bottom: 1px solid rgba(209, 97, 97, 0.08);
                    color: rgb(235, 180, 180);
                }

                tbody tr:hover {
                    background: rgba(209, 97, 97, 0.03);
                }
            ";
        }

        private string EscapeHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
        }

        private string GetModeName(string mode)
        {
            return mode switch
            {
                "constant" => "Постоянный RPS",
                "linear_ramp" => "Линейный рост",
                _ => mode
            };
        }
    }
}