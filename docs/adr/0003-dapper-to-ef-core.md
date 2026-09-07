# ADR-0003: Migrate `ReadingRepository` from Dapper to EF Core

## Status

Accepted

## Context

`ReadingRepository` originally used Dapper with raw SQL over a `Microsoft.Data.Sqlite`
connection. It hit a real bug during Phase A development: Dapper has no built-in coercion
between SQLite's TEXT-stored dates and `DateTimeOffset`, so reading `TimestampUtc`/
`ForwardedAtUtc` back either threw or silently dropped the offset. The workaround was a
custom `SqlMapper.TypeHandler<DateTimeOffset>` — a few lines of code, but hand-rolled
serialization logic for something that should be a solved problem, and one more thing to get
subtly wrong (e.g. the round-trip format/`DateTimeStyles` had to be picked carefully).

The migration was deliberately deferred during the initial Phase A build (docker-compose and
the test suite were prioritized first) rather than abandoned — see the Phase A status notes.

## Decision

Replace Dapper + raw SQL with EF Core (`Microsoft.EntityFrameworkCore.Sqlite`): a
`GatewayDbContext` with one `DbSet<ReadingEntity>`, `EnsureCreatedAsync` instead of a manual
`CREATE TABLE IF NOT EXISTS`, and LINQ instead of hand-written SQL strings. EF Core's Sqlite
provider has a built-in `DateTimeOffset` converter, so the custom type handler is gone
entirely — no hand-rolled date serialization left in the codebase.

`ReadingRepository`'s public method signatures were kept identical
(`InitializeAsync`/`InsertAsync`/`GetUnforwardedBatchAsync`/`MarkForwardedAsync`), so
`BufferWriter` and `IotHubForwarder` — the two classes that depend on it — needed zero
changes. This confirmed the migration was genuinely localized, as anticipated when it was
deferred.

One design point surfaced *by* the migration, not present in the original Dapper code:
`ReadingRepository` is registered as a singleton and called concurrently by `BufferWriter`
and `IotHubForwarder` (both run inside the same `Task.WhenAll` in `Worker.ExecuteAsync`). The
old Dapper code opened a fresh `SqliteConnection` per method call, so this was never an
issue. A single shared `DbContext` would have been — `DbContext` isn't thread-safe. Solved by
injecting `IDbContextFactory<GatewayDbContext>` instead of a `DbContext` directly, and having
every repository method create its own short-lived context, faithfully reproducing the old
per-call-connection isolation.

## Consequences

- No more hand-rolled `DateTimeOffset` serialization — the bug class that motivated this
  migration can't recur.
- LINQ queries (`Where`/`OrderBy`/`Take`/`Select`) replace hand-written SQL strings — less
  code, and query shape errors are caught at compile time instead of at first execution.
- New dependency: EF Core's change-tracking and query-translation machinery is heavier than
  Dapper's thin micro-ORM layer. For a single-table, low-complexity schema like this one,
  that's an acceptable trade — it would be worth reconsidering if the schema grew
  significantly more complex and the overhead started to matter on constrained edge
  hardware.
- The `IDbContextFactory` pattern (a fresh `DbContext` per call, not an injected singleton
  `DbContext`) is now a hard requirement any future method on this class must follow — an
  easy mistake for someone unfamiliar with the concurrency shape of `Worker`'s pipeline to
  make by "simplifying" this back to a direct `DbContext` injection.
