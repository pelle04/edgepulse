using Dapper;
using EdgePulse.Gateway.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EdgePulse.Gateway.Buffering
{
    // ForwardedAtUtc is NULL until the (not-yet-built) Forwarder confirms
    // delivery to IoT Hub — that's the "not forwarded" checkpoint from the
    // wiring diagram. Only Initialize/Insert exist for now; the read-side
    // (batch-select unforwarded rows, mark forwarded) belongs to that phase.
    internal class ReadingRepository
    {
        private readonly string _connectionString;

        public ReadingRepository(IOptions<BufferWriterOptions> options)
        {
            _connectionString = options.Value.ConnectionString;
        }

        public async Task InitializeAsync(CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                CREATE TABLE IF NOT EXISTS Readings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DeviceId TEXT NOT NULL,
                    MetricName TEXT NOT NULL,
                    Value REAL NOT NULL,
                    Unit TEXT NOT NULL,
                    TimestampUtc TEXT NOT NULL,
                    ForwardedAtUtc TEXT NULL
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
        }

        public async Task InsertAsync(Reading reading, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                INSERT INTO Readings (DeviceId, MetricName, Value, Unit, TimestampUtc)
                VALUES (@DeviceId, @MetricName, @Value, @Unit, @TimestampUtc);
                """;

            await connection.ExecuteAsync(new CommandDefinition(sql, reading, cancellationToken: ct));
        }
    }

    internal class BufferWriterOptions
    {
        public string ConnectionString { get; set; } = "Data Source=edgepulse.db";
    }
}
