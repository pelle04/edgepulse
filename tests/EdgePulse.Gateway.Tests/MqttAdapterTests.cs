using EdgePulse.Gateway.Adapters;
using System;

public class MqttAdapterTests
{
    [Theory]
    [InlineData("%RH","humidity")]
    [InlineData("C", "temperature")]
    [InlineData("","unknown")]
    public void GivenUnit_then_ReturnCorrectMeasure(string unit,string expectedMetricName)
    {
        var res = MqttAdapter.InferMetricName(unit);

        Assert.Equal(expectedMetricName, res);
    }
}
