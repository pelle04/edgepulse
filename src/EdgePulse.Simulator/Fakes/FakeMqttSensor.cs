using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using System.Text.Json;

namespace EdgePulse.Simulator.Fakes
{
    // Publishes fake sensor readings to mosquitto over MQTT.
    // Uses the managed client so reconnects are handled for us — the fake
    // acts like a flaky field sensor without hand-rolled retry logic.
    internal class FakeMqttSensor : BackgroundService
    {
        private readonly ILogger<FakeMqttSensor> _logger;
        private readonly FakeMqttSensorOptions _options;
        private readonly Random _random = new();
        private double _value = 45.0; // starting %RH

        public FakeMqttSensor(ILogger<FakeMqttSensor> logger, IOptions<FakeMqttSensorOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var client = new MqttFactory().CreateManagedMqttClient();

            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Host, _options.Port)
                .WithClientId($"fake-sensor-{_options.DeviceId}")
                .WithCleanSession()
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions)
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .Build();

            await client.StartAsync(managedOptions);
            _logger.LogInformation("FakeMqttSensor connecting to {Host}:{Port}, publishing on {Topic}",
                _options.Host, _options.Port, _options.Topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await PublishReadingAsync(client);
                    await Task.Delay(_options.PublishIntervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }

            await client.StopAsync();
        }

        private async Task PublishReadingAsync(IManagedMqttClient client)
        {
            _value = Math.Clamp(_value + _random.Next(-2, 3), 0, 100);

            var payload = new MqttReadingPayload(
                DeviceId: _options.DeviceId,
                Value: _value,
                Unit: "%RH",
                TimestampUtc: DateTimeOffset.UtcNow);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_options.Topic)
                .WithPayload(JsonSerializer.Serialize(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await client.EnqueueAsync(message);
        }
    }

    // Wire contract on MQTT — MqttAdapter will deserialize this shape.
    internal record MqttReadingPayload(string DeviceId, double Value, string Unit, DateTimeOffset TimestampUtc);

    internal class FakeMqttSensorOptions
    {
        public string Host { get; set; } = "mosquitto";
        public int Port { get; set; } = 1883;
        public string Topic { get; set; } = "edgepulse/sensors/humidity-01/reading";
        public string DeviceId { get; set; } = "humidity-01";
        public int PublishIntervalMs { get; set; } = 3000;
    }
}