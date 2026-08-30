using EdgePulse.Simulator;
using EdgePulse.Simulator.Fakes;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FakeModbusPlcOptions>(builder.Configuration.GetSection("FakeModbusPlc"));
builder.Services.Configure<FakeMqttSensorOptions>(builder.Configuration.GetSection("FakeMqttSensor"));

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<FakeModbusPlc>();
builder.Services.AddHostedService<FakeMqttSensor>();

var host = builder.Build();
host.Run();
