using EdgePulse.Gateway;
using EdgePulse.Gateway.Adapters;
using EdgePulse.Gateway.Models;
using System.Threading.Channels;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ModbusAdapterOptions>(builder.Configuration.GetSection("ModbusAdapter"));
builder.Services.Configure<MqttAdapterOptions>(builder.Configuration.GetSection("MqttAdapter"));

// Bounded so a stalled downstream consumer applies backpressure to the
// adapters instead of letting memory grow unbounded.
var channel = Channel.CreateBounded<Reading>(new BoundedChannelOptions(500)
{
    FullMode = BoundedChannelFullMode.Wait
});
builder.Services.AddSingleton(channel.Reader);
builder.Services.AddSingleton(channel.Writer);

builder.Services.AddSingleton<IDeviceAdapter, ModbusAdapter>();
builder.Services.AddSingleton<IDeviceAdapter, MqttAdapter>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
