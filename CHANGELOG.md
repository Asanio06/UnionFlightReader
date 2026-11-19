# Changelog

## 0.1.0-alpha (unreleased)

- Add `FlightSnapshot` with explicit units and nullable unknown values.
- Add `IFlightSimProvider`, `FlightSimProviderBase`, and `FlightSimMonitor` with probe/failover.
- Add X-Plane Web API provider (REST dataref resolution + WebSocket subscription).
- Add X-Plane UDP RREF provider with batched 413-byte packets, cancellation, and reconnect.
- Add unit tests for the contract, monitor, RREF parser, Web API mapping, and REST client.
- Target .NET 10 LTS.
- Switch the license to MIT.
