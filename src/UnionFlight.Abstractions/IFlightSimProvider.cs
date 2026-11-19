namespace UnionFlight;

/// <summary>
/// A telemetry provider for one simulator family/version.
/// Implementations own their transport, parsing and unit mapping.
/// </summary>
public interface IFlightSimProvider : IAsyncDisposable
{
    SimulatorKind Kind { get; }

    string DisplayName { get; }

    bool IsSupportedOnThisPlatform { get; }

    /// <summary>
    /// Cheap, side-effect-free availability probe. Safe to call repeatedly.
    /// </summary>
    Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken);

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    FlightSnapshot? LatestSnapshot { get; }

    event EventHandler<FlightSnapshot>? SnapshotReceived;

    event EventHandler<ProviderFaultedEventArgs>? Faulted;
}
