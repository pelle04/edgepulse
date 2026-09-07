# ADR-0002: Mark a reading forwarded only after IoT Hub acknowledges it

## Status

Accepted

## Context

`IotHubForwarder.RunAsync` polls `ReadingRepository.GetUnforwardedBatchAsync` for rows where
`ForwardedAtUtc IS NULL`, groups them by device, and sends each group to Azure IoT Hub via
`DeviceClient.SendEventAsync`. Somewhere between "read the batch" and "the row is marked
forwarded" the process could crash, lose network, or IoT Hub could reject/fail the send —
and whatever ordering is chosen for "send" vs. "mark forwarded" determines what happens to
that batch when it does.

## Decision

`ForwardGroupAsync` only calls `_repository.MarkForwardedAsync(...)` **after**
`client.SendEventAsync(message)` has completed successfully. If `SendEventAsync` throws, the
`catch` block logs a warning and returns without marking anything — the rows stay
`ForwardedAtUtc IS NULL` and are picked up again on the forwarder's next poll cycle
(`PollIntervalMs`).

This means the send is only ever confirmed forwarded after a real acknowledgment from IoT
Hub, never optimistically before or during the send.

## Consequences

- **No reading is silently lost on a failed or interrupted send** — a crash, a dropped
  connection, or a hub-side error all leave the batch unforwarded, so it's retried
  automatically rather than needing manual recovery.
- **This is at-least-once delivery, not exactly-once.** If the send succeeds on IoT Hub's
  side but the process crashes (or the connection drops) before `MarkForwardedAsync`
  commits, the same batch is sent again on the next cycle. Downstream consumers of IoT Hub
  data must tolerate duplicate messages (e.g. by deduplicating on `DeviceId` +
  `TimestampUtc`, which together are effectively unique per reading) — this was a deliberate
  trade-off, not an oversight: building exactly-once delivery (e.g. via idempotency tokens
  and a two-phase commit-style protocol) was judged not worth the complexity for a
  monitoring/telemetry system where an occasional duplicate is harmless but a lost reading
  is not.
- A batch that fails repeatedly (e.g. a permanently invalid connection string for a device)
  retries forever at `PollIntervalMs` — there is currently no dead-letter/max-retry
  mechanism, so a persistently broken device's readings accumulate unforwarded in SQLite
  indefinitely rather than being flagged. Worth revisiting once real devices are connected;
  not addressed by this ADR.
