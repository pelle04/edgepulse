using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NModbus;
using NModbus.Data;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
namespace EdgePulse.Simulator.Fakes
{
    // Hosts a Modbus TCP slave and drifts its holding registers over time,
    // so ModbusAdapter has something that behaves like a live PLC to poll.
    internal class FakeModbusPlc : BackgroundService
    {
        private readonly ILogger<FakeModbusPlc> _logger;
        private readonly FakeModbusPlcOptions _options;
        private readonly Random _random = new();

        public FakeModbusPlc(ILogger<FakeModbusPlc> logger, IOptions<FakeModbusPlcOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ModbusFactory();
            var listener = new TcpListener(IPAddress.Any, _options.Port);
            listener.Start();

            var network = factory.CreateSlaveNetwork(listener);
            var dataStore = new DefaultSlaveDataStore();
            network.AddSlave(factory.CreateSlave(_options.SlaveId, dataStore));

            // Registers are ushort — no float/negative support — so values are
            // scaled x10. ModbusAdapter must divide by 10 when it reads these back.
            dataStore.HoldingRegisters.WritePoints(ModbusRegisters.TemperatureC, new ushort[] { 220 }); // 22.0 C
            dataStore.HoldingRegisters.WritePoints(ModbusRegisters.PressureHpa, new ushort[] { 1013 }); // 101.3 kPa

            var listenTask = network.ListenAsync(stoppingToken);
            _logger.LogInformation("FakeModbusPlc listening on TCP :{Port} (slave {SlaveId})",
                _options.Port, _options.SlaveId);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(_options.UpdateIntervalMs, stoppingToken);
                    Drift(dataStore, ModbusRegisters.TemperatureC, step: 2);
                    Drift(dataStore, ModbusRegisters.PressureHpa, step: 3);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }

            listener.Stop();
            await listenTask;
        }

        private void Drift(DefaultSlaveDataStore store, ushort address, int step)
        {
            var current = store.HoldingRegisters.ReadPoints(address, 1)[0];
            var delta = _random.Next(-step, step + 1);
            var next = (ushort)Math.Max(0, current + delta);
            store.HoldingRegisters.WritePoints(address, new ushort[] { next });
        }
    }

    internal static class ModbusRegisters
    {
        public const ushort TemperatureC = 0; // x10 scaled
        public const ushort PressureHpa = 1;  // x10 scaled
    }

    internal class FakeModbusPlcOptions
    {
        public int Port { get; set; } = 502;
        public byte SlaveId { get; set; } = 1;
        public int UpdateIntervalMs { get; set; } = 2000;
    }
}

