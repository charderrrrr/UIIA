using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public class DatasetPlayer
    {
        public List<DatasetRecord> Load(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null
            });
            
            return csv.GetRecords<DatasetRecord>().ToList();
        }

        public async Task<List<MetricRecord>> PlayAsync(List<DatasetRecord> dataset, ITrafficGenerator generator, string baseUrl, TestConfig config, double timeScale)
        {
            var results = new List<MetricRecord>();
            
            var events = dataset.OrderBy(d => d.TimeMs).ToList();
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var baseTimestamp = events.First().TimeMs;

            foreach (var entry in events)
            {
                var relativeTime = entry.TimeMs - baseTimestamp;
                var targetRealTime = startTime + (long)(relativeTime / timeScale);
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var delay = targetRealTime - currentTime;

                if (delay > 0)
                {
                    await Task.Delay((int)delay);
                }

                var endpoint = baseUrl.TrimEnd('/') + entry.Endpoint;
                var metric = await generator.SendRequestAsync(endpoint, config);
                results.Add(metric);
            }

            return results;
        }
    }
}