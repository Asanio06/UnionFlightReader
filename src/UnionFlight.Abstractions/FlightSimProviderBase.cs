namespace UnionFlight;

/// <summary>
/// Base class for providers: owns the latest snapshot and the event plumbing.
/// Implementations only need to produce snapshots and report faults.
/// </summary>
public abstract class FlightSimProviderBase : IFlightSimProvider
{
    private FlightSnapshot? _latest;

    public abstract SimulatorKind Kind { get; }

    public abstract string DisplayName { get; }

    public virtual bool IsSupportedOnThisPlatform => true;

    public FlightSnapshot? LatestSnapshot => Volatile.Read(ref _latest);

    public event EventHandler<FlightSnapshot>? SnapshotReceived;

    public event EventHandler<ProviderFaultedEventArgs>? Faulted;

    public abstract Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken);

    public abstract Task StartAsync(CancellationToken cancellationToken);

    public abstract Task StopAsync(CancellationToken cancellationToken);

    protected void Publish(FlightSnapshot snapshot)
    {
        Volatile.Write(ref _latest, snapshot);
        SnapshotReceived?.Invoke(this, snapshot);
    }

    protected void ReportFault(Exception exception) =>
        Faulted?.Invoke(this, new ProviderFaultedEventArgs(exception));

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
