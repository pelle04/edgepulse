using EdgePulse.Gateway.Buffering;
using EdgePulse.Gateway.Forwarding;
using EdgePulse.Gateway.Models;
using Microsoft.Azure.Devices.Client;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EdgePulse.Gateway.Tests;

// The repository side uses a real temp-SQLite ReadingRepository, same as
// ReadingRepositoryTests — only the actual external system (Azure IoT Hub) is faked,
// via the IIotHubDeviceClient/IDeviceClientFactory seam.
public class IotHubForwarderTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReadingRepository _repository;

    public IotHubForwarderTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"edgepulse-test-{Guid.NewGuid():N}.db");
        var dbContextFactory = new TestDbContextFactory($"Data Source={_dbPath}");
        _repository = new ReadingRepository(dbContextFactory);
    }

    [Fact]
    public async Task ForwardGroupAsync_marks_the_batch_forwarded_only_after_a_successful_send()
    {
        await _repository.InitializeAsync(CancellationToken.None);
        await _repository.InsertAsync(
            new Reading("plc-01", "temperature", 21.7, "C", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var fakeClient = new FakeIotHubDeviceClient();
        var forwarder = CreateForwarder(new FakeDeviceClientFactory(fakeClient));

        await RunOneForwardCycleAsync(forwarder);

        Assert.Single(fakeClient.SentMessages);

        var remaining = await _repository.GetUnforwardedBatchAsync(10, CancellationToken.None);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task ForwardGroupAsync_leaves_the_batch_unforwarded_when_the_send_fails()
    {
        await _repository.InitializeAsync(CancellationToken.None);
        await _repository.InsertAsync(
            new Reading("plc-01", "temperature", 21.7, "C", DateTimeOffset.UtcNow),
            CancellationToken.None);

        var fakeClient = new FakeIotHubDeviceClient { ThrowOnSend = true };
        var forwarder = CreateForwarder(new FakeDeviceClientFactory(fakeClient));

        // The real assertion is what this does NOT throw: RunAsync must survive a failed
        // send instead of crashing the Worker's Task.WhenAll pipeline.
        await RunOneForwardCycleAsync(forwarder);

        var remaining = await _repository.GetUnforwardedBatchAsync(10, CancellationToken.None);
        Assert.Single(remaining);
    }

    private IotHubForwarder CreateForwarder(IDeviceClientFactory deviceClientFactory)
    {
        var options = Options.Create(new IotHubForwarder.IotHubForwarderOptions
        {
            BatchSize = 50,
            PollIntervalMs = 5000, // long enough that the test's cancellation lands mid-delay, after exactly one cycle
            ConnectionStrings = new Dictionary<string, string> { ["plc-01"] = "fake-connection-string" }
        });

        return new IotHubForwarder(NullLogger<IotHubForwarder>.Instance, _repository, options, deviceClientFactory);
    }

    // RunAsync loops until cancelled; give it one poll cycle then cancel during the delay.
    private static async Task RunOneForwardCycleAsync(IotHubForwarder forwarder)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await forwarder.RunAsync(cts.Token);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    private sealed class FakeIotHubDeviceClient : IIotHubDeviceClient
    {
        public bool ThrowOnSend { get; set; }
        public List<Message> SentMessages { get; } = new();

        public Task SendEventAsync(Message message)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("simulated IoT Hub send failure");
            }

            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task CloseAsync() => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeDeviceClientFactory : IDeviceClientFactory
    {
        private readonly IIotHubDeviceClient _client;

        public FakeDeviceClientFactory(IIotHubDeviceClient client) => _client = client;

        public IIotHubDeviceClient CreateFromConnectionString(string connectionString) => _client;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<GatewayDbContext>
    {
        private readonly DbContextOptions<GatewayDbContext> _options;

        public TestDbContextFactory(string connectionString) =>
            _options = new DbContextOptionsBuilder<GatewayDbContext>().UseSqlite(connectionString).Options;

        public GatewayDbContext CreateDbContext() => new(_options);
    }
}
