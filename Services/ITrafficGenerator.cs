using System.Threading.Tasks;
using UIIA.Models;

namespace UIIA.Services
{
    public interface ITrafficGenerator
    {
        string Protocol { get; }
        Task<MetricRecord> SendRequestAsync(string target, TestConfig config);
    }
}