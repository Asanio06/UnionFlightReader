namespace UnionFlight;

/// <summary>
/// Convenience facade over <see cref="FlightSimMonitor"/>.
/// The caller supplies the providers; platform-specific meta-packages add
/// SimConnect providers on Windows.
/// </summary>
public sealed class UnionFlightClient : IAsyncDisposable
{
    private readonly FlightSimMonitor _monitor;

    public UnionFlightClient(IEnumerable<IFlightSimProvider> providers)
        : this(new FlightSimMonitor(providers))
    {
    }

    public UnionFlightClient(FlightSimMonitor monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
    }

    public FlightSnapshot? Latest => _monitor.Latest;

    public IFlightSimProvider? ActiveProvider => _monitor.ActiveProvider;

    public event EventHandler<FlightSnapshot>? SnapshotReceived
    {
        add => _monitor.SnapshotReceived += value;
        remove => _monitor.SnapshotReceived -= value;
    }

    public event EventHandler<IFlightSimProvider>? ProviderChanged
    {
        add => _monitor.ProviderChanged += value;
        remove => _monitor.ProviderChanged -= value;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _monitor.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _monitor.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => _monitor.DisposeAsync();
}
