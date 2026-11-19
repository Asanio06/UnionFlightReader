using System.Text.Json;

namespace UnionFlight.XPlane.WebApi;

/// <summary>
/// X-Plane provider over the local REST + WebSocket Web API (X-Plane 12.1.1+).
/// Static strings are read once over REST; dynamic values are streamed over
/// the WebSocket at the simulator's 10 Hz cadence.
/// </summary>
public sealed class XPlaneWebApiProvider : FlightSimProviderBase
{
    private readonly XPlaneOptions _options;
    private readonly IXPlaneRestClient _rest;
    private readonly Func<IXPlaneWebSocket> _socketFactory;
    private readonly Dictionary<long, Action<XPlaneState, double>> _numericHandlers = new();

    private XPlaneState _state = new();
    private IXPlaneWebSocket? _socket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private Task? _publishTask;
    private string? _simulatorVersion;
    private string _apiVersion = "v3";

    public XPlaneWebApiProvider(
        XPlaneOptions? options = null,
        IXPlaneRestClient? rest = null,
        Func<IXPlaneWebSocket>? socketFactory = null)
    {
        _options = options ?? new XPlaneOptions();
        _rest = rest ?? new XPlaneRestClient(_options);
        _socketFactory = socketFactory ?? (() => new XPlaneWebSocketClient());
    }

    public override SimulatorKind Kind => SimulatorKind.XPlane;

    public override string DisplayName => "X-Plane (Web API)";

    public override async Task<SimulatorProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var capabilities = await _rest.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
            return capabilities is null
                ? SimulatorProbeResult.Unavailable("X-Plane Web API is not available.")
                : SimulatorProbeResult.Available(
                    capabilities.Version,
                    $"Web API {string.Join(",", capabilities.ApiVersions)}");
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
        _numericHandlers.Clear();

        var capabilities = await _rest.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("X-Plane Web API is not available.");

        _simulatorVersion = capabilities.Version;
        _apiVersion = capabilities.PreferredApiVersion;

        await ResolveDatarefsAsync(cancellationToken).ConfigureAwait(false);
        await ReadStaticStringsAsync(cancellationToken).ConfigureAwait(false);

        var socket = _socketFactory();
        _socket = socket;

        var uri = new Uri($"ws://{_options.Host}:{_options.WebApiPort}/api/{_apiVersion}");
        await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
        await SendSubscribeAsync(socket, cancellationToken).ConfigureAwait(false);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cts = cts;
        var token = cts.Token;

        _receiveTask = Task.Run(() => ReceiveLoopAsync(socket, token), token);
        _publishTask = Task.Run(() => PublishLoopAsync(token), token);
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

        if (socket is not null)
        {
            try
            {
                await socket.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        var tasks = new[] { _receiveTask, _publishTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();

        _receiveTask = null;
        _publishTask = null;

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
    }

    public override async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _rest.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ResolveDatarefsAsync(CancellationToken cancellationToken)
    {
        var definitions = new (string Name, Action<XPlaneState, double> Handler)[]
        {
            (XPlaneDatarefs.Paused, (state, value) => state.Paused = (int)value),
            (XPlaneDatarefs.Latitude, (state, value) =>
            {
                state.LatitudeDeg = value;
                state.HasAircraftData = true;
            }),
            (XPlaneDatarefs.Longitude, (state, value) =>
            {
                state.LongitudeDeg = value;
                state.HasAircraftData = true;
            }),
            (XPlaneDatarefs.Elevation, (state, value) => state.ElevationM = value),
            (XPlaneDatarefs.HeightAgl, (state, value) => state.HeightAglM = value),
            (XPlaneDatarefs.IndicatedAirspeed, (state, value) => state.IndicatedAirspeedKt = value),
            (XPlaneDatarefs.TrueAirspeed, (state, value) => state.TrueAirspeedMs = value),
            (XPlaneDatarefs.Groundspeed, (state, value) => state.GroundspeedMs = value),
            (XPlaneDatarefs.VerticalSpeed, (state, value) => state.VerticalSpeedFpm = value),
            (XPlaneDatarefs.HeadingMag, (state, value) => state.HeadingMagDeg = value),
            (XPlaneDatarefs.TotalFlightTime, (state, value) => state.TotalFlightTimeSec = value),
            (XPlaneDatarefs.InternalVersion, (state, value) => state.InternalVersion = (int)value)
        };

        foreach (var (name, handler) in definitions)
        {
            var id = await _rest.ResolveDatarefAsync(name, cancellationToken).ConfigureAwait(false);
            if (id is not null)
            {
                _numericHandlers[id.Value] = handler;
            }
        }
    }

    private async Task ReadStaticStringsAsync(CancellationToken cancellationToken)
    {
        var tailId = await _rest.ResolveDatarefAsync(XPlaneDatarefs.TailNumber, cancellationToken)
            .ConfigureAwait(false);
        if (tailId is not null)
        {
            var tail = await _rest.GetDatarefDataStringAsync(tailId.Value, cancellationToken)
                .ConfigureAwait(false);
            _state.SetString(_state.TailNumber, tail);
        }

        var uiNameId = await _rest.ResolveDatarefAsync(XPlaneDatarefs.UiName, cancellationToken)
            .ConfigureAwait(false);
        if (uiNameId is not null)
        {
            var uiName = await _rest.GetDatarefDataStringAsync(uiNameId.Value, cancellationToken)
                .ConfigureAwait(false);
            _state.SetString(_state.UiName, uiName);
        }

        var descriptionId = await _rest.ResolveDatarefAsync(XPlaneDatarefs.Description, cancellationToken)
            .ConfigureAwait(false);
        if (descriptionId is not null)
        {
            var description = await _rest.GetDatarefDataStringAsync(descriptionId.Value, cancellationToken)
                .ConfigureAwait(false);
            _state.SetString(_state.Description, description);
        }
    }

    private async Task SendSubscribeAsync(IXPlaneWebSocket socket, CancellationToken cancellationToken)
    {
        var ids = _numericHandlers.Keys.OrderBy(id => id).ToArray();
        var request = new
        {
            req_id = 1,
            type = "dataref_subscribe_values",
            @params = new
            {
                datarefs = ids.Select(id => new { id }).ToArray()
            }
        };

        await socket.SendTextAsync(JsonSerializer.Serialize(request), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(IXPlaneWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var text = await socket.ReceiveTextAsync(cancellationToken).ConfigureAwait(false);
                if (text is null)
                {
                    ReportFault(new IOException("X-Plane WebSocket closed."));
                    return;
                }

                using var document = JsonDocument.Parse(text);
                HandleMessage(document.RootElement);
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

    private void HandleMessage(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeProperty))
        {
            return;
        }

        switch (typeProperty.GetString())
        {
            case "dataref_update_values":
                if (!root.TryGetProperty("data", out var data) ||
                    data.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                foreach (var property in data.EnumerateObject())
                {
                    if (!long.TryParse(property.Name.Trim(), out var id) ||
                        property.Value.ValueKind != JsonValueKind.Number)
                    {
                        continue;
                    }

                    if (_numericHandlers.TryGetValue(id, out var handler))
                    {
                        handler(_state, property.Value.GetDouble());
                    }
                }

                break;

            case "result":
                if (root.TryGetProperty("success", out var success) &&
                    success.ValueKind == JsonValueKind.False)
                {
                    var code = root.TryGetProperty("error_code", out var codeProperty)
                        ? codeProperty.GetString()
                        : "unknown";
                    var message = root.TryGetProperty("error_message", out var messageProperty)
                        ? messageProperty.GetString()
                        : "X-Plane Web API error";

                    throw new XPlaneApiException(
                        code ?? "unknown",
                        message ?? "X-Plane Web API error");
                }

                break;
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

                Publish(_state.ToSnapshot(SimulatorKind.XPlane, _simulatorVersion));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
