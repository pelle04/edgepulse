using Microsoft.Azure.Devices.Client;

namespace EdgePulse.Gateway.Forwarding
{
    internal interface IIotHubDeviceClient : IDisposable
    {
        Task SendEventAsync(Message message);
        Task CloseAsync();
    }

    internal class IotHubDeviceClientAdapter : IIotHubDeviceClient
    {
        private readonly DeviceClient _inner;

        public IotHubDeviceClientAdapter(DeviceClient inner) => _inner = inner;

        public Task SendEventAsync(Message message) => _inner.SendEventAsync(message);
        public Task CloseAsync() => _inner.CloseAsync();
        public void Dispose() => _inner.Dispose();
    }

    internal interface IDeviceClientFactory
    {
        IIotHubDeviceClient CreateFromConnectionString(string connectionString);
    }

    internal class DeviceClientFactory : IDeviceClientFactory
    {
        public IIotHubDeviceClient CreateFromConnectionString(string connectionString) =>
            new IotHubDeviceClientAdapter(DeviceClient.CreateFromConnectionString(connectionString));
    }
}
