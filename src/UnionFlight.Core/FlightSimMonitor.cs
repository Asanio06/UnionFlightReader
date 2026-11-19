using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnionFlight;

/// <summary>
/// Probes a set of providers in priority order, starts the first available one,
/// and restarts the probe when the active provider faults.
/// </summary>
public sealed class FlightSimMonitor : IAsyncDisposable
{
    private readonly IReadOnlyList<IFlightSimProvider> _providers;
    private readonly ILogger<FlightSimMonitor> _logger;
    private readonly FlightSimMonitorOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IFlightSimProvider? _active;
    private FlightSnapshot? _latest;
    private bool _disposed;

    public FlightSimMonitor(
        IEnumerable<IFlightSimProvider> providers,
        TimeProvider? time = null,
        ILogger<FlightSimMonitor>? logger = null,
        FlightSimMonitorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _providers = providers.Where(p => p.IsSupportedOnThisPlatform).ToArray();
        _ = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<FlightSimMonitor>.Instance;
        _options = options ?? new FlightSimMonitorOptions();
    }

    public IFlightSimProvider? ActiveProvider => Volatile.Read(ref _active);

    public FlightSnapshot? Latest => Volatile.Read(ref _latest);

    public event EventHandler<FlightSnapshot>? SnapshotReceived;

    public event EventHandler<IFlightSimProvider>? ProviderChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_active is not null)
            {
                return;
            }

            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopActiveCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await StopActiveCoreAsync().ConfigureAwait(false);

            foreach (var provider in _providers)
            {
                try
                {
                    await provider.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error while disposing {Provider}.", provider.DisplayName);
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var available = await ProbeAllAsync(cancellationToken).ConfigureAwait(false);
        var provider = available.Count > 0 ? available[0] : null;
        if (provider is null)
        {
            _logger.LogDebug("No supported simulator is available.");
            return;
        }

        _active = provider;
        provider.SnapshotReceived += OnSnapshot;
        provider.Faulted += OnFaulted;

        try
        {
            await provider.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            provider.SnapshotReceived -= OnSnapshot;
            provider.Faulted -= OnFaulted;
            _active = null;
            throw;
        }

        _logger.LogInformation("Using simulator provider {Provider}.", provider.DisplayName);
        ProviderChanged?.Invoke(this, provider);
    }

    private async Task<IReadOnlyList<IFlightSimProvider>> ProbeAllAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ProbeTimeout);

        var tasks = _providers.Select(async provider =>
        {
            try
            {
                var result = await provider.ProbeAsync(timeout.Token).ConfigureAwait(false);
                return (provider: provider, result: result);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return (provider: provider, result: SimulatorProbeResult.Unavailable("Probe timed out."));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Probe failed for {Provider}.", provider.DisplayName);
                return (provider: provider, result: SimulatorProbeResult.Unavailable(ex.Message));
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results
            .Where(result => result.result.IsAvailable)
            .Select(result => result.provider)
            .ToArray();
    }

    private void OnSnapshot(object? sender, FlightSnapshot snapshot)
    {
        Volatile.Write(ref _latest, snapshot);
        SnapshotReceived?.Invoke(this, snapshot);
    }

    private void OnFaulted(object? sender, ProviderFaultedEventArgs args)
    {
        var provider = sender as IFlightSimProvider;
        _logger.LogWarning(
            args.Exception,
            "Provider {Provider} faulted; re-probing.",
            provider?.DisplayName ?? "unknown");

        _ = RestartAfterFaultAsync();
    }

    private async Task RestartAfterFaultAsync()
    {
        try
        {
            await Task.Delay(_options.RestartDelay).ConfigureAwait(false);

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed)
                {
                    return;
                }

                await StopActiveCoreAsync().ConfigureAwait(false);
                await StartCoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart the simulator provider.");
        }
    }

    private async Task StopActiveCoreAsync()
    {
        var active = _active;
        if (active is null)
        {
            return;
        }

        _active = null;
        active.SnapshotReceived -= OnSnapshot;
        active.Faulted -= OnFaulted;

        try
        {
            await active.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while stopping {Provider}.", active.DisplayName);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
