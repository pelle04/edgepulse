using EdgePulse.Gateway.Models;
using System.Threading.Channels;

namespace EdgePulse.Gateway.Adapters
{
    internal interface IDeviceAdapter
    {
        string Name { get; }

        Task RunAsync(ChannelWriter<Reading> output, CancellationToken stoppingToken);
    }
}
