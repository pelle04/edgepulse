using EdgePulse.Gateway.Adapters;
using EdgePulse.Gateway.Models;
using System.Threading.Channels;

namespace EdgePulse.Gateway
{
    internal class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEnumerable<IDeviceAdapter> _adapters;
        private readonly ChannelReader<Reading> _reader;
        private readonly ChannelWriter<Reading> _writer;

        public Worker(
            ILogger<Worker> logger,
            IEnumerable<IDeviceAdapter> adapters,
            ChannelReader<Reading> reader,
            ChannelWriter<Reading> writer)
        {
            _logger = logger;
            _adapters = adapters;
            _reader = reader;
            _writer = writer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var producers = _adapters.Select(adapter => RunAdapterAsync(adapter, stoppingToken));
            await Task.WhenAll(producers.Append(ConsumeAsync(stoppingToken)));
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

        // Temporary sink until BufferWriter/SQLite exists — logs what will
        // eventually be persisted, so the pipeline is observable end-to-end today.
        private async Task ConsumeAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var reading in _reader.ReadAllAsync(stoppingToken))
                {
                    _logger.LogInformation(
                        "{DeviceId} {MetricName}={Value}{Unit} @ {TimestampUtc:o}",
                        reading.DeviceId, reading.MetricName, reading.Value, reading.Unit, reading.TimestampUtc);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
    }
}
