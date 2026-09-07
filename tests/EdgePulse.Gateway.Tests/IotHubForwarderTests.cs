namespace EdgePulse.Gateway.Tests;

// Deliberately empty for now. IotHubForwarder builds Microsoft.Azure.Devices.Client's
// DeviceClient directly (the static DeviceClient.CreateFromConnectionString factory,
// called from private GetOrCreateClient) with no seam to substitute a fake — so the two
// behaviors actually worth testing here (retries without crashing when the send fails,
// and only marks a batch forwarded after the send is acknowledged) can't be exercised
// without either a real IoT Hub or a production-code change such as an injectable
// client factory.
//
// IotHubForwarder integrates with an Azure SDK, which per this project's role split is
// Pellegrino's class to write/modify — so this stays a placeholder instead of Claude
// adding that seam unasked. Today it's only verified manually against the failure path
// (logs a warning and retries, doesn't crash, when IoT Hub is unreachable).
public class IotHubForwarderTests
{
}
