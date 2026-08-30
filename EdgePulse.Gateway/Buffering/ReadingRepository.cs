using Dapper;
using EdgePulse.Gateway.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace EdgePulse.Gateway.Buffering
{
    // ForwardedAtUtc is NULL until the Forwarder confirms delivery to IoT Hub —
    // that's the "not forwarded" checkpoint from the wiring diagram.
    internal class ReadingRepository
    {
        private readonly string _connectionString;

        static ReadingRepository()
        {
            // Dapper has no built-in DateTimeOffset <-> TEXT coercion, and SQLite
            // has no native DateTimeOffset column type — without this handler,
            // reading rows back throws on the TimestampUtc/ForwardedAtUtc columns.
            SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        }

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

        // Read side for the Forwarder: rows with ForwardedAtUtc still NULL,
        // oldest first, so a slow forwarder catches up in order rather than
        // cherry-picking the newest readings and starving old ones.
        public async Task<IReadOnlyList<BufferedReading>> GetUnforwardedBatchAsync(int batchSize, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                SELECT Id, DeviceId, MetricName, Value, Unit, TimestampUtc
                FROM Readings
                WHERE ForwardedAtUtc IS NULL
                ORDER BY Id
                LIMIT @BatchSize;
                """;

            var rows = await connection.QueryAsync<BufferedReading>(
                new CommandDefinition(sql, new { BatchSize = batchSize }, cancellationToken: ct));

            return rows.AsList();
        }

        // Only call this after IoT Hub has actually acknowledged the batch —
        // marking rows forwarded before the send is confirmed is how you lose
        // readings on a crash between "sent" and "marked".
        public async Task MarkForwardedAsync(IEnumerable<long> ids, DateTimeOffset forwardedAtUtc, CancellationToken ct)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = """
                UPDATE Readings
                SET ForwardedAtUtc = @ForwardedAtUtc
                WHERE Id IN @Ids;
                """;

            await connection.ExecuteAsync(
                new CommandDefinition(sql, new { ForwardedAtUtc = forwardedAtUtc, Ids = ids }, cancellationToken: ct));
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

    internal class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value) =>
            DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        public override void SetValue(System.Data.IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value;
    }

    internal class BufferWriterOptions
    {
        public string ConnectionString { get; set; } = "Data Source=edgepulse.db";
    }
}
