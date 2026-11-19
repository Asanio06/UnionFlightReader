namespace UnionFlight.TestKit;

/// <summary>Configurable fake provider for monitor/orchestrator tests.</summary>
public sealed class FakeFlightSimProvider : IFlightSimProvider
{
    public FakeFlightSimProvider(SimulatorKind kind, string displayName, bool available = true)
    {
        Kind = kind;
        DisplayName = displayName;
        IsAvailable = available;
    }

    public SimulatorKind Kind { get; }

    public string DisplayName { get; }

    public bool IsSupportedOnThisPlatform { get; set; } = true;

    public bool IsAvailable { get; set; }

    public int ProbeCount { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public int DisposeCount { get; private set; }

    public FlightSnapshot? LatestSnapshot { get; private set; }

    public event EventHandler<FlightSnapshot>? SnapshotReceived;

    public event EventHandler<ProviderFaultedEventArgs>? Faulted;

    public Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        ProbeCount++;
        return Task.FromResult(
            IsAvailable
                ? SimulatorProbeResult.Available("1.0", DisplayName)
                : SimulatorProbeResult.Unavailable("not running"));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    public void Emit(FlightSnapshot snapshot)
    {
        LatestSnapshot = snapshot;
        SnapshotReceived?.Invoke(this, snapshot);
    }

    public void Fail(Exception exception) =>
        Faulted?.Invoke(this, new ProviderFaultedEventArgs(exception));
}
