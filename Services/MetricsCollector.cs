using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public class MetricsCollector
    {
        private readonly Channel<MetricRecord> _channel = Channel.CreateBounded<MetricRecord>(10000);
        private readonly List<MetricRecord> _records = new();

        public ChannelWriter<MetricRecord> Writer => _channel.Writer;

        public async Task RunCollectorAsync()
        {
            await foreach (var record in _channel.Reader.ReadAllAsync())
            {
                lock (_records)
                {
                    _records.Add(record);
                }
            }
        }

        public List<MetricRecord> GetRecords()
        {
            lock (_records)
            {
                return _records.ToList();
            }
        }
    }
}