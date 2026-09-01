using EdgePulse.Gateway.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Packets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace EdgePulse.Gateway.Adapters
{
    internal class MqttAdapter : IDeviceAdapter
    {
        private readonly ILogger<MqttAdapter> _logger;
        private readonly MqttAdapterOptions _options;

        public string Name => "Mqtt";

        public MqttAdapter(ILogger<MqttAdapter> logger, IOptions<MqttAdapterOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task RunAsync(ChannelWriter<Reading> output, CancellationToken stoppingToken)
        {
            using var client = new MqttFactory().CreateManagedMqttClient();

            client.ApplicationMessageReceivedAsync += e => HandleMessageAsync(e, output, stoppingToken);

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Host, _options.Port)
                .WithClientId("edgepulse-gateway")
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .Build();

            await client.StartAsync(managedOptions);

            await client.SubscribeAsync(new List<MqttTopicFilter>
            {
                new MqttTopicFilterBuilder().WithTopic(_options.TopicFilter).Build()
            });

            _logger.LogInformation("{Name} subscribed to {TopicFilter} on {Host}:{Port}",
                Name, _options.TopicFilter, _options.Host, _options.Port);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }

            await client.StopAsync();
        }

        private async Task HandleMessageAsync(
            MqttApplicationMessageReceivedEventArgs e,
            ChannelWriter<Reading> output,
            CancellationToken stoppingToken)
        {
            MqttSensorPayload? payload;
            try
            {
                var json = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
                payload = JsonSerializer.Deserialize<MqttSensorPayload>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "{Name} discarding malformed payload on {Topic}", Name, e.ApplicationMessage.Topic);
                return;
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.DeviceId))
            {
                _logger.LogWarning("{Name} discarding payload missing DeviceId on {Topic}", Name, e.ApplicationMessage.Topic);
                return;
            }

            var reading = new Reading(
                DeviceId: payload.DeviceId,
                MetricName: InferMetricName(payload.Unit),
                Value: payload.Value,
                Unit: payload.Unit,
                TimestampUtc: payload.TimestampUtc);

            await output.WriteAsync(reading, stoppingToken);
        }

        private static string InferMetricName(string unit) => unit switch
        {
            "%RH" => "humidity",
            "C" => "temperature",
            _ => "unknown"
        };
    }

    // Wire contract read from MQTT — mirrors FakeMqttSensor's MqttReadingPayload,
    // but deliberately a separate type: the JSON shape is the contract, not the C# type.
    internal record MqttSensorPayload(string DeviceId, double Value, string Unit, DateTimeOffset TimestampUtc);

    internal class MqttAdapterOptions
    {
        public string Host { get; set; } = "mosquitto";
        public int Port { get; set; } = 1883;
        public string TopicFilter { get; set; } = "edgepulse/sensors/+/reading";
    }
}
