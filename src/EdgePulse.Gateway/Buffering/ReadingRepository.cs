using EdgePulse.Gateway.Models;
using Microsoft.EntityFrameworkCore;

namespace EdgePulse.Gateway.Buffering
{
    // ForwardedAtUtc is NULL until the Forwarder confirms delivery to IoT Hub —
    // that's the "not forwarded" checkpoint from the wiring diagram.
    //
    // ReadingRepository is registered as a singleton and called concurrently from both
    // BufferWriter and IotHubForwarder (see Worker.ExecuteAsync), so it can't hold a single
    // shared DbContext — DbContext isn't thread-safe. Instead it asks the factory for a
    // fresh, short-lived context per call, mirroring the old Dapper code's "new
    // SqliteConnection per method call" pattern.
    internal class ReadingRepository
    {
        private readonly IDbContextFactory<GatewayDbContext> _dbContextFactory;

        public ReadingRepository(IDbContextFactory<GatewayDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
            await db.Database.EnsureCreatedAsync(ct);
        }

        public async Task InsertAsync(Reading reading, CancellationToken ct)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            db.Readings.Add(new ReadingEntity
            {
                DeviceId = reading.DeviceId,
                MetricName = reading.MetricName,
                Value = reading.Value,
                Unit = reading.Unit,
                TimestampUtc = reading.TimestampUtc
            });

            await db.SaveChangesAsync(ct);
        }

        // Read side for the Forwarder: rows with ForwardedAtUtc still NULL,
        // oldest first, so a slow forwarder catches up in order rather than
        // cherry-picking the newest readings and starving old ones.
        public async Task<IReadOnlyList<BufferedReading>> GetUnforwardedBatchAsync(int batchSize, CancellationToken ct)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            return await db.Readings
                .Where(r => r.ForwardedAtUtc == null)
                .OrderBy(r => r.Id)
                .Take(batchSize)
                .Select(r => new BufferedReading
                {
                    Id = r.Id,
                    DeviceId = r.DeviceId,
                    MetricName = r.MetricName,
                    Value = r.Value,
                    Unit = r.Unit,
                    TimestampUtc = r.TimestampUtc
                })
                .ToListAsync(ct);
        }

        // Only call this after IoT Hub has actually acknowledged the batch —
        // marking rows forwarded before the send is confirmed is how you lose
        // readings on a crash between "sent" and "marked".
        public async Task MarkForwardedAsync(IEnumerable<long> ids, DateTimeOffset forwardedAtUtc, CancellationToken ct)
        {
            var idSet = ids as ICollection<long> ?? ids.ToList();

            await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

            var rows = await db.Readings
                .Where(r => idSet.Contains(r.Id))
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                row.ForwardedAtUtc = forwardedAtUtc;
            }

            await db.SaveChangesAsync(ct);
        }
    }

    internal record BufferedReading
    {
        public long Id { get; init; }
        public string DeviceId { get; init; } = string.Empty;
        public string MetricName { get; init; } = string.Empty;
        public double Value { get; init; }
        public string Unit { get; init; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; init; }
    }

    internal class BufferWriterOptions
    {
        public string ConnectionString { get; set; } = "Data Source=edgepulse.db";
    }
}
