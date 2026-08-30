using EdgePulse.Gateway.Adapters;
using EdgePulse.Gateway.Buffering;
using EdgePulse.Gateway.Models;
using System.Threading.Channels;

namespace EdgePulse.Gateway
{
    internal class Worker : BackgroundService
    {
        private readonly IEnumerable<IDeviceAdapter> _adapters;
        private readonly BufferWriter _bufferWriter;
        private readonly ChannelReader<Reading> _reader;
        private readonly ChannelWriter<Reading> _writer;

        public Worker(
            IEnumerable<IDeviceAdapter> adapters,
            BufferWriter bufferWriter,
            ChannelReader<Reading> reader,
            ChannelWriter<Reading> writer)
        {
            _adapters = adapters;
            _bufferWriter = bufferWriter;
            _reader = reader;
            _writer = writer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var producers = _adapters.Select(adapter => RunAdapterAsync(adapter, stoppingToken));
            await Task.WhenAll(producers.Append(_bufferWriter.RunAsync(_reader, stoppingToken)));
        }

        private async Task RunAdapterAsync(IDeviceAdapter adapter, CancellationToken stoppingToken)
        {
            try
            {
                await adapter.RunAsync(_writer, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
    }
}
