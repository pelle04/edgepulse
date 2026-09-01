using EdgePulse.Gateway.Models;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace EdgePulse.Gateway.Buffering
{
    // Every reading lands here before anything else happens — the durability
    // checkpoint from the wiring diagram. A dead network or a crashed process
    // never loses a reading, it just delays it until this table catches up.
    internal class BufferWriter
    {
        private readonly ILogger<BufferWriter> _logger;
        private readonly ReadingRepository _repository;

        public BufferWriter(ILogger<BufferWriter> logger, ReadingRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task RunAsync(ChannelReader<Reading> input, CancellationToken stoppingToken)
        {
            await _repository.InitializeAsync(stoppingToken);

            try
            {
                await foreach (var reading in input.ReadAllAsync(stoppingToken))
                {
                    await _repository.InsertAsync(reading, stoppingToken);
                    _logger.LogInformation(
                        "Buffered {DeviceId} {MetricName}={Value}{Unit} @ {TimestampUtc:o}",
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
