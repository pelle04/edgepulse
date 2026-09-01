using EdgePulse.Gateway.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;
using System.Net.Sockets;
using System.Threading.Channels;

namespace EdgePulse.Gateway.Adapters
{
    internal class ModbusAdapter : IDeviceAdapter
    {
        private readonly ILogger<ModbusAdapter> _logger;
        private readonly ModbusAdapterOptions _options;

        public string Name => $"Modbus:{_options.DeviceId}";

        public ModbusAdapter(ILogger<ModbusAdapter> logger, IOptions<ModbusAdapterOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task RunAsync(ChannelWriter<Reading> output, CancellationToken stoppingToken)
        {
            var backoff = TimeSpan.FromSeconds(1);
            var maxBackoff = TimeSpan.FromSeconds(30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(_options.Host, _options.Port, stoppingToken);

                    var factory = new ModbusFactory();
                    using var master = factory.CreateMaster(tcpClient);

                    _logger.LogInformation("{Name} connected to {Host}:{Port}", Name, _options.Host, _options.Port);
                    backoff = TimeSpan.FromSeconds(1); // reset after a clean connect

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        foreach (var register in _options.RegisterMap)
                        {
                            var raw = await master.ReadHoldingRegistersAsync(_options.SlaveId, register.Address, 1);

                            var reading = new Reading(
                                DeviceId: _options.DeviceId,
                                MetricName: register.MetricName,
                                Value: raw[0] / (double)register.ScaleFactor,
                                Unit: register.Unit,
                                TimestampUtc: DateTimeOffset.UtcNow);

                            await output.WriteAsync(reading, stoppingToken);
                        }

                        await Task.Delay(_options.PollIntervalMs, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected on shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Name} failed, retrying in {Backoff}", Name, backoff);

                    try { await Task.Delay(backoff, stoppingToken); }
                    catch (OperationCanceledException) { break; }

                    backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, maxBackoff.TotalSeconds));
                }
            }
        }
    }

    internal class RegisterMapping
    {
        public ushort Address { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int ScaleFactor { get; set; } = 1;
    }

    internal class ModbusAdapterOptions
    {
        public string Host { get; set; } = "simulator";
        public int Port { get; set; } = 502;
        public byte SlaveId { get; set; } = 1;
        public string DeviceId { get; set; } = "plc-01";
        public int PollIntervalMs { get; set; } = 2000;
        public List<RegisterMapping> RegisterMap { get; set; } = new();
    }
}
