# Architecture Decision Records

This folder contains the ADRs (Architecture Decision Records) for EdgePulse.
Each ADR documents a single significant architectural choice: the context,
the decision, and the consequences.

Format: [Michael Nygard's template](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions).

Naming convention: `NNNN-short-title.md` (e.g. `0001-use-timescaledb.md`).

## Index

- [0001 — Bounded `Channel<Reading>` with `BoundedChannelFullMode.Wait`](0001-bounded-channel-backpressure.md)
- [0002 — Mark a reading forwarded only after IoT Hub acknowledges it](0002-mark-forwarded-after-ack.md)
- [0003 — Migrate `ReadingRepository` from Dapper to EF Core](0003-dapper-to-ef-core.md)
