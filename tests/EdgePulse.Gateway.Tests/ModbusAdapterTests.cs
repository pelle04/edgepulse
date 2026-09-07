using EdgePulse.Gateway.Adapters;

namespace EdgePulse.Gateway.Tests;

// RunAsync itself needs a live TCP/Modbus connection, so it's not unit-tested here —
// these cover the two pure calculations that were extracted out of it (register scaling,
// backoff growth), which is where the actual logic bugs would hide.
public class ModbusAdapterTests
{
    [Theory]
    [InlineData((ushort)220, 10, 22.0)]  // FakeModbusPlc's x10-scaled temperature convention
    [InlineData((ushort)1013, 10, 101.3)]
    [InlineData((ushort)0, 1, 0.0)]
    public void ScaleValue_divides_the_raw_register_by_the_configured_scale_factor(ushort raw, int scaleFactor, double expected)
    {
        var value = ModbusAdapter.ScaleValue(raw, scaleFactor);

        Assert.Equal(expected, value, precision: 3);
    }

    [Fact]
    public void NextBackoff_doubles_the_current_delay()
    {
        var next = ModbusAdapter.NextBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(2), next);
    }

    [Fact]
    public void NextBackoff_never_exceeds_the_configured_maximum()
    {
        var next = ModbusAdapter.NextBackoff(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), next);
    }
}
