using EdgePulse.Gateway.Buffering;
using Microsoft.Azure.Devices.Client;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace EdgePulse.Gateway.Forwarding
{
    // Pulls unforwarded rows from SQLite and pushes them to IoT Hub,
    // Rows are only marked forwarded after IoT Hub has acknowledged the send
    // if the send throws, the rows stay unforwarded
    // and get picked up again on the next loop iteration instead of being silently lost.
    internal class IotHubForwarder
    {
        private readonly ILogger<IotHubForwarder> _logger;
        private readonly ReadingRepository _repository;
        private readonly IotHubForwarderOptions _options;
        private readonly Dictionary<string, DeviceClient> _clients = new();

        public IotHubForwarder(ILogger<IotHubForwarder> logger, ReadingRepository repository, IOptions<IotHubForwarderOptions> options)
        {
            _logger = logger;
            _repository = repository;
            _options = options.Value;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var batch = await _repository.GetUnforwardedBatchAsync(_options.BatchSize, stoppingToken);

                    foreach (var group in batch.GroupBy(r => r.DeviceId))
                    {
                        await ForwardGroupAsync(group.Key, group.ToList(), stoppingToken);
                    }

                    await Task.Delay(_options.PollIntervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
            finally
            {
                await CloseClientsAsync();
            }
        }

        private async Task ForwardGroupAsync(string deviceId, List<BufferedReading> readings, CancellationToken ct)
        {
            if (!_options.ConnectionStrings.TryGetValue(deviceId, out var connectionString))
            {
                _logger.LogWarning("No IoT Hub connection string configured for {DeviceId}, skipping", deviceId);
                return;
            }

            try
            {
                var client = GetOrCreateClient(deviceId, connectionString);

                var json = JsonSerializer.Serialize(readings);
                using var message = new Message(Encoding.UTF8.GetBytes(json));
                await client.SendEventAsync(message);

                await _repository.MarkForwardedAsync(readings.Select(r => r.Id), DateTimeOffset.UtcNow, ct);

                _logger.LogInformation("Forwarded {Count} readings for {DeviceId}", readings.Count, deviceId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to forward {Count} readings for {DeviceId}, will retry next cycle",
                    readings.Count, deviceId);
            }
        }

        private DeviceClient GetOrCreateClient(string deviceId, string connectionString)
        {
            if (!_clients.TryGetValue(deviceId, out var client))
            {
                client = DeviceClient.CreateFromConnectionString(connectionString);
                _clients[deviceId] = client;
            }

            return client;
        }

        private async Task CloseClientsAsync()
        {
            foreach (var client in _clients.Values)
            {
                try
                {
                    await client.CloseAsync();
                }
                catch
                {
                    // best-effort on shutdown
                }

                client.Dispose();
            }

            _clients.Clear();
        }

        internal class IotHubForwarderOptions
        {
            public int BatchSize { get; set; } = 50;
            public int PollIntervalMs { get; set; } = 5000;

            // key = DeviceId (es. "plc-01"), value = device's connection string on IoT Hub
            public Dictionary<string, string> ConnectionStrings { get; set; } = new();
        }
    }
}
