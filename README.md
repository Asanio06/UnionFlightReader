# UnionFlight

One .NET API for flight simulator telemetry: **X-Plane 11/12**, **MSFS 2020/2024**, and **Prepar3D v4/v5/v6**.

The library exposes an immutable `FlightSnapshot` with explicit units and a provider per simulator family. The orchestrator probes providers, starts the available one, and re-probes after a fault.

## Status

| Simulator | Transport | Status |
|---|---|---|
| X-Plane 12.1.1+ | Local Web API (REST + WebSocket) | Implemented, end-to-end tested |
| X-Plane 11 / LAN | UDP RREF (batched) | Implemented, end-to-end tested |
| MSFS 2020 | SimConnect host process | Planned |
| MSFS 2024 | SimConnect host process | Planned |
| P3D v4/v5/v6 | SimConnect host process per version | Planned |

- **Read-only in v1**: no dataref/SimVar writes, commands, or outgoing events.
- **.NET 10 LTS** only.
- **MIT** licensed.

The working roadmap lives in `.hermes/plans/2026-11-19_145448-unionflight-reprise.md`.

## Quick start

```csharp
using UnionFlight;
using UnionFlight.XPlane;

await using var client = new UnionFlightClient(new IFlightSimProvider[]
{
    new XPlaneProvider()
});

client.SnapshotReceived += (_, snapshot) =>
{
    Console.WriteLine(
        $"{snapshot.Simulator} | {snapshot.AircraftTitle} | " +
        $"{snapshot.AltitudeFt:F0} ft | {snapshot.GroundspeedKt:F0} kt | " +
        $"paused={snapshot.IsPaused}");
};

await client.StartAsync();

if (client.ActiveProvider is null)
{
    Console.WriteLine("No simulator detected.");
    return;
}

await Task.Delay(Timeout.Infinite);
```

## Snapshot contract

Units are in the property names; unsupported values stay `null`.

| Property | Unit |
|---|---|
| `LatitudeDeg`, `LongitudeDeg` | degrees |
| `AltitudeFt`, `HeightAglFt` | feet |
| `IndicatedAirspeedKt`, `TrueAirspeedKt`, `GroundspeedKt` | knots |
| `VerticalSpeedFpm` | feet per minute |
| `HeadingMagDeg` | degrees (magnetic) |
| `IsFlightLoaded` | true when a flight is loaded |
| `IsPaused` | null when unknown, otherwise paused/unpaused |

`IsFlightLoaded`, `IsPaused`, and the connection state are independent. A paused flight is still loaded.

## Architecture

```
UnionFlight.Abstractions   contracts, FlightSnapshot, provider base
UnionFlight.Core           FlightSimMonitor, probe/failover, facade
UnionFlight.XPlane         Web API provider + UDP RREF fallback
UnionFlight.SimConnect     Windows client + one host process per SimConnect version (planned)
```

SimConnect client DLLs are version-locked (MSFS 2020 vs 2024, P3D v4/v5/v6). The planned design runs one host process per version and streams NDJSON snapshots to the client, so two versions never share a process.

## Run without a simulator

The repository ships a fake X-Plane server (Web API + UDP RREF) so the library and the sample can be exercised on any machine:

```bash
# Terminal 1
dotnet run --project tools/UnionFlight.FakeSim

# Terminal 2
dotnet run --project samples/UnionFlight.Sample.Console
```

The sample should print `Connected to X-Plane.` and a Cessna 172 snapshot.

## Build and test

```bash
dotnet build UnionFlight.sln
dotnet test UnionFlight.sln
dotnet run --project samples/UnionFlight.Sample.Console
```

The suite has 31 tests. X-Plane tests run without a simulator: the end-to-end tests start `FakeXPlaneServer` (Kestrel + WebSocket + UDP) and use the real `HttpClient`, `ClientWebSocket`, and `UdpClient`. Live tests against a real simulator are marked `Integration` and require the corresponding simulator.

## License

MIT. See [LICENSE](LICENSE).
