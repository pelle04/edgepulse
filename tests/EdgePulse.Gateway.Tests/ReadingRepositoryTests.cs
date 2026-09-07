using EdgePulse.Gateway.Buffering;
using EdgePulse.Gateway.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EdgePulse.Gateway.Tests;

public class ReadingRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ReadingRepository _repository;

    public ReadingRepositoryTests()
    {
        // A real temp SQLite file, not a mock — the behavior we're testing (the
        // DateTimeOffsetHandler round-trip) only exists at the real ADO.NET/Dapper
        // boundary, so mocking the repository away would just assert against itself.
        _dbPath = Path.Combine(Path.GetTempPath(), $"edgepulse-test-{Guid.NewGuid():N}.db");
        var options = Options.Create(new BufferWriterOptions
        {
            ConnectionString = $"Data Source={_dbPath}"
        });
        _repository = new ReadingRepository(options);
    }

    [Fact]
    public async Task InsertAsync_then_GetUnforwardedBatchAsync_returns_the_same_reading()
    {
        // Arrange
        await _repository.InitializeAsync(CancellationToken.None);
        var reading = new Reading("plc-01", "temperature", 21.7, "C", DateTimeOffset.UtcNow);

        // Act
        await _repository.InsertAsync(reading, CancellationToken.None);
        var batch = await _repository.GetUnforwardedBatchAsync(batchSize: 10, CancellationToken.None);

        // Assert
        var stored = Assert.Single(batch);
        Assert.Equal(reading.DeviceId, stored.DeviceId);
        Assert.Equal(reading.MetricName, stored.MetricName);
        Assert.Equal(reading.Value, stored.Value);
        Assert.Equal(reading.Unit, stored.Unit);

        // This assertion is the one that actually exercises the DateTimeOffsetHandler
        // workaround in ReadingRepository — without it, this either throws when Dapper
        // reads the TimestampUtc column back, or silently loses the offset.
        Assert.Equal(reading.TimestampUtc, stored.TimestampUtc);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools its native connections by default, so the
        // file can still be locked here even though every `using var connection`
        // in ReadingRepository has already gone out of scope. Clearing the pool
        // releases the underlying handle before we try to delete the file.
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
