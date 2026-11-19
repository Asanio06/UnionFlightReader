using UnionFlight.XPlane.Udp;
using UnionFlight.XPlane.WebApi;

namespace UnionFlight.XPlane;

/// <summary>
/// X-Plane provider that prefers the Web API (X-Plane 12.1.1+) and falls back
/// to UDP RREF for X-Plane 11 and LAN setups.
/// </summary>
public sealed class XPlaneProvider : FlightSimProviderBase
{
    private readonly XPlaneOptions _options;
    private readonly XPlaneWebApiProvider _webApi;
    private readonly XPlaneUdpProvider _udp;
    private IFlightSimProvider? _active;

    public XPlaneProvider(XPlaneOptions? options = null)
    {
        _options = options ?? new XPlaneOptions();
        _webApi = new XPlaneWebApiProvider(_options);
        _udp = new XPlaneUdpProvider(_options);
    }

    public override SimulatorKind Kind => SimulatorKind.XPlane;

    public override string DisplayName => "X-Plane";

    public override async Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var first = _options.PreferWebApi ? (IFlightSimProvider)_webApi : _udp;
        var second = _options.PreferWebApi ? (IFlightSimProvider)_udp : _webApi;

        var firstResult = await first.ProbeAsync(cancellationToken).ConfigureAwait(false);
        return firstResult.IsAvailable
            ? firstResult
            : await second.ProbeAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var first = _options.PreferWebApi ? (IFlightSimProvider)_webApi : _udp;
        var second = _options.PreferWebApi ? (IFlightSimProvider)_udp : _webApi;

        var firstProbe = await first.ProbeAsync(cancellationToken).ConfigureAwait(false);
        _active = firstProbe.IsAvailable ? first : second;

        _active.SnapshotReceived += OnSnapshot;
        _active.Faulted += OnFaulted;

        await _active.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var active = _active;
        if (active is null)
        {
            return;
        }

        _active = null;
        active.SnapshotReceived -= OnSnapshot;
        active.Faulted -= OnFaulted;
        await active.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await _webApi.DisposeAsync().ConfigureAwait(false);
        await _udp.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private void OnSnapshot(object? sender, FlightSnapshot snapshot) => Publish(snapshot);

    private void OnFaulted(object? sender, ProviderFaultedEventArgs args) =>
        ReportFault(args.Exception);
}
