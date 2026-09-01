using EdgePulse.Gateway.Adapters;
using EdgePulse.Gateway.Buffering;
using EdgePulse.Gateway.Forwarding;
using EdgePulse.Gateway.Models;
using System.Threading.Channels;

namespace EdgePulse.Gateway
{
    internal class Worker : BackgroundService
    {
        private readonly IEnumerable<IDeviceAdapter> _adapters;
        private readonly BufferWriter _bufferWriter;
        private readonly IotHubForwarder _forwarder;
        private readonly ChannelReader<Reading> _reader;
        private readonly ChannelWriter<Reading> _writer;

        public Worker(
            IEnumerable<IDeviceAdapter> adapters,
            BufferWriter bufferWriter,
            IotHubForwarder forwarder,
            ChannelReader<Reading> reader,
            ChannelWriter<Reading> writer)
        {
            _adapters = adapters;
            _bufferWriter = bufferWriter;
            _forwarder = forwarder;
            _reader = reader;
            _writer = writer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var producers = _adapters.Select(adapter => RunAdapterAsync(adapter, stoppingToken));
            var pipeline = producers
                .Append(_bufferWriter.RunAsync(_reader, stoppingToken))
                .Append(_forwarder.RunAsync(stoppingToken));

            await Task.WhenAll(pipeline);
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
