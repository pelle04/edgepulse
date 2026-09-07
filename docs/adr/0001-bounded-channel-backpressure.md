# ADR-0001: Bounded `Channel<Reading>` with `BoundedChannelFullMode.Wait`

## Status

Accepted

## Context

Every `IDeviceAdapter` (`ModbusAdapter`, `MqttAdapter`) writes `Reading`s into a single
`Channel<Reading>` that `BufferWriter` drains and persists to SQLite (`Program.cs` wires the
channel; `Worker` runs the adapters and `BufferWriter` concurrently over it). The consumer
side can legitimately fall behind the producer side — a slow disk, a locked SQLite file, or
just multiple adapters producing faster than one writer can insert.

The Gateway is meant to run on constrained edge hardware (Raspberry Pi / small industrial
PC), not a cloud VM with elastic memory. An unbounded channel would let readings pile up in
memory with no ceiling if the consumer ever falls behind — on this hardware that's a real
OOM risk, not a theoretical one, and it would happen silently until the process crashed.

## Decision

Use `Channel.CreateBounded<Reading>(new BoundedChannelOptions(500) { FullMode =
BoundedChannelFullMode.Wait })`. When the channel is full, `WriteAsync` on the producer side
(the adapters) awaits until the consumer (`BufferWriter`) makes room, instead of growing
without limit or dropping readings.

`BoundedChannelFullMode.Wait` was chosen over the alternatives:
- `DropWrite` / `DropNewest` / `DropOldest` were rejected — silently discarding a `Reading`
  contradicts the project's offline-first, don't-lose-data design principle. A dropped
  reading is a gap in the historical record with no signal that it happened.
- An unbounded channel was rejected for the memory-ceiling reason above.

## Consequences

- Memory usage for the pipeline is bounded and predictable (at most 500 buffered `Reading`s
  in flight), regardless of how far the consumer falls behind.
- No reading is ever silently dropped due to backpressure — the worst case is delay, not
  loss.
- Backpressure propagates upstream: if `BufferWriter`/SQLite stalls for long enough, the
  adapters themselves block on `WriteAsync`, which in turn delays their next Modbus poll /
  MQTT message handling. This is accepted as correct behavior for this project — the
  channel is only the in-memory hand-off stage between ingestion and durability, the actual
  durability buffer is SQLite itself (see the "offline-first" design principle in
  `README.md`), so a persistently slow consumer is a problem to fix at the SQLite layer, not
  something the channel should paper over by dropping data.
- The capacity (500) is an untuned starting value, not derived from a specific throughput
  target — worth revisiting if a real device fleet's ingestion rate turns out to need a
  different number.
