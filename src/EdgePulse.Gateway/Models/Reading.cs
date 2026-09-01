namespace EdgePulse.Gateway.Models
{
    internal record Reading(
        string DeviceId,
        string MetricName,
        double Value,
        string Unit,
        DateTimeOffset TimestampUtc);
}
