using System.Net.Sockets;

namespace UnionFlight.XPlane.Udp;

/// <summary>
/// X-Plane provider over the UDP RREF protocol.
/// One socket is bound to an ephemeral port; requests are batched into
/// 413-byte datagrams, and the receive loop cancels cleanly on stop.
/// </summary>
public sealed class XPlaneUdpProvider : FlightSimProviderBase
{
    private readonly XPlaneOptions _options;
    private readonly Dictionary<int, Action<XPlaneState, float>> _handlers = new();

    private XPlaneState _state = new();
    private UdpClient? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _publishTask;
    private Task? _refreshTask;

    public XPlaneUdpProvider(XPlaneOptions? options = null) =>
        _options = options ?? new XPlaneOptions();

    public override SimulatorKind Kind => SimulatorKind.XPlane;

    public override string DisplayName => "X-Plane (UDP RREF)";

    public override async Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new UdpClient(AddressFamily.InterNetwork);
            socket.Connect(_options.Host, _options.UdpPort);

            foreach (var packet in XPlaneRrefProtocol.BuildPackets(
                         new[] { new RrefRequest(1, 0, XPlaneDatarefs.Paused) }))
            {
                await socket.SendAsync(packet, cancellationToken).ConfigureAwait(false);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.UdpTimeout);

            var response = await socket.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            Span<RrefUpdate> updates = stackalloc RrefUpdate[4];
            var count = XPlaneRrefProtocol.Parse(response.Buffer, updates);

            return count > 0
                ? SimulatorProbeResult.Available(null, "UDP RREF")
                : SimulatorProbeResult.Unavailable("Unexpected RREF response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SimulatorProbeResult.Unavailable("No UDP response from X-Plane.");
        }
        catch (Exception ex)
        {
            return SimulatorProbeResult.Unavailable(ex.Message);
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_socket is not null)
        {
            return;
        }

        _state = new XPlaneState();
        BuildHandlers();

        var socket = new UdpClient(AddressFamily.InterNetwork);
        socket.Connect(_options.Host, _options.UdpPort);
        _socket = socket;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cts = cts;
        var token = cts.Token;

        await SendSubscriptionsAsync(socket, token).ConfigureAwait(false);

        _receiveTask = Task.Run(() => ReceiveLoopAsync(socket, token), token);
        _publishTask = Task.Run(() => PublishLoopAsync(token), token);
        _refreshTask = Task.Run(() => RefreshLoopAsync(socket, token), token);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = _cts;
        var socket = _socket;
        if (cts is null && socket is null)
        {
            return;
        }

        _cts = null;
        _socket = null;

        try
        {
            cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            socket?.Close();
        }
        catch (ObjectDisposedException)
        {
        }

        var tasks = new[] { _receiveTask, _publishTask, _refreshTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        _receiveTask = null;
        _publishTask = null;
        _refreshTask = null;

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        cts?.Dispose();
        socket?.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task SendSubscriptionsAsync(UdpClient socket, CancellationToken cancellationToken)
    {
        foreach (var packet in XPlaneRrefProtocol.BuildPackets(BuildRequests()))
        {
            await socket.SendAsync(packet, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReceiveLoopAsync(UdpClient socket, CancellationToken cancellationToken)
    {
        var updates = new RrefUpdate[8];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await socket.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                var count = XPlaneRrefProtocol.Parse(response.Buffer, updates);

                for (var i = 0; i < count; i++)
                {
                    if (_handlers.TryGetValue(updates[i].Id, out var handler))
                    {
                        handler(_state, updates[i].Value);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFault(ex);
        }
    }

    private async Task PublishLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.SnapshotInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!_state.HasAircraftData &&
                    _state.Paused is null &&
                    _state.InternalVersion is null)
                {
                    continue;
                }

                Publish(_state.ToSnapshot(SimulatorKind.XPlane, null));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RefreshLoopAsync(UdpClient socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.SubscriptionRefresh, cancellationToken).ConfigureAwait(false);
                await SendSubscriptionsAsync(socket, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ReportFault(ex);
        }
    }

    private void BuildHandlers()
    {
        _handlers.Clear();

        _handlers[1] = (state, value) => state.Paused = (int)value;
        _handlers[2] = (state, value) =>
        {
            state.LatitudeDeg = value;
            state.HasAircraftData = true;
        };
        _handlers[3] = (state, value) =>
        {
            state.LongitudeDeg = value;
            state.HasAircraftData = true;
        };
        _handlers[4] = (state, value) => state.ElevationM = value;
        _handlers[5] = (state, value) => state.HeightAglM = value;
        _handlers[6] = (state, value) => state.IndicatedAirspeedKt = value;
        _handlers[7] = (state, value) => state.TrueAirspeedMs = value;
        _handlers[8] = (state, value) => state.GroundspeedMs = value;
        _handlers[9] = (state, value) => state.VerticalSpeedFpm = value;
        _handlers[10] = (state, value) => state.HeadingMagDeg = value;
        _handlers[11] = (state, value) => state.TotalFlightTimeSec = value;
        _handlers[12] = (state, value) => state.InternalVersion = (int)value;

        for (var i = 0; i < XPlaneState.TailNumberLength; i++)
        {
            var index = i;
            _handlers[1000 + i] = (state, value) => state.SetString(state.TailNumber, index, value);
        }

        for (var i = 0; i < XPlaneState.UiNameLength; i++)
        {
            var index = i;
            _handlers[2000 + i] = (state, value) => state.SetString(state.UiName, index, value);
        }

        for (var i = 0; i < XPlaneState.DescriptionLength; i++)
        {
            var index = i;
            _handlers[3000 + i] = (state, value) => state.SetString(state.Description, index, value);
        }
    }

    private static List<RrefRequest> BuildRequests()
    {
        var requests = new List<RrefRequest>(600)
        {
            new(10, 1, XPlaneDatarefs.Paused),
            new(10, 2, XPlaneDatarefs.Latitude),
            new(10, 3, XPlaneDatarefs.Longitude),
            new(10, 4, XPlaneDatarefs.Elevation),
            new(10, 5, XPlaneDatarefs.HeightAgl),
            new(10, 6, XPlaneDatarefs.IndicatedAirspeed),
            new(10, 7, XPlaneDatarefs.TrueAirspeed),
            new(10, 8, XPlaneDatarefs.Groundspeed),
            new(10, 9, XPlaneDatarefs.VerticalSpeed),
            new(10, 10, XPlaneDatarefs.HeadingMag),
            new(1, 11, XPlaneDatarefs.TotalFlightTime),
            new(1, 12, XPlaneDatarefs.InternalVersion)
        };

        for (var i = 0; i < XPlaneState.TailNumberLength; i++)
        {
            requests.Add(new RrefRequest(1, 1000 + i, $"{XPlaneDatarefs.TailNumber}[{i}]"));
        }

        for (var i = 0; i < XPlaneState.UiNameLength; i++)
        {
            requests.Add(new RrefRequest(1, 2000 + i, $"{XPlaneDatarefs.UiName}[{i}]"));
        }

        for (var i = 0; i < XPlaneState.DescriptionLength; i++)
        {
            requests.Add(new RrefRequest(1, 3000 + i, $"{XPlaneDatarefs.Description}[{i}]"));
        }

        return requests;
    }
}
