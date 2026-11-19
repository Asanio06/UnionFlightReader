using UnionFlight.XPlane.WebApi;

namespace UnionFlight.XPlane.Tests;

public sealed class XPlaneWebApiProviderTests
{
    [Fact]
    public async Task StartAsync_Maps_WebSocket_Updates_To_Snapshot()
    {
        var rest = new FakeRest();

        var ids = new Dictionary<string, long>
        {
            [XPlaneDatarefs.Paused] = 1,
            [XPlaneDatarefs.Latitude] = 2,
            [XPlaneDatarefs.Longitude] = 3,
            [XPlaneDatarefs.Elevation] = 4,
            [XPlaneDatarefs.HeightAgl] = 5,
            [XPlaneDatarefs.IndicatedAirspeed] = 6,
            [XPlaneDatarefs.TrueAirspeed] = 7,
            [XPlaneDatarefs.Groundspeed] = 8,
            [XPlaneDatarefs.VerticalSpeed] = 9,
            [XPlaneDatarefs.HeadingMag] = 10,
            [XPlaneDatarefs.TotalFlightTime] = 11,
            [XPlaneDatarefs.InternalVersion] = 12,
            [XPlaneDatarefs.TailNumber] = 20,
            [XPlaneDatarefs.UiName] = 21,
            [XPlaneDatarefs.Description] = 22
        };

        foreach (var (name, id) in ids)
        {
            rest.Ids[name] = id;
        }

        rest.Data[20] = "N12345";
        rest.Data[21] = "Cessna 172";

        var socket = new FakeSocket(
            """
            {"type":"dataref_update_values","data":{"1":0,"2":48.8566,"3":2.3522,"4":1000,"5":500,"6":120,"7":130,"8":140,"9":-500,"10":180,"11":42,"12":120905}}
            """);

        await using var provider = new XPlaneWebApiProvider(
            new XPlaneOptions { SnapshotInterval = TimeSpan.FromMilliseconds(10) },
            rest,
            () => socket);

        var received = new TaskCompletionSource<FlightSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider.SnapshotReceived += (_, snapshot) => received.TrySetResult(snapshot);

        await provider.StartAsync(CancellationToken.None);
        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(received.Task, completed);

        var snapshot = await received.Task;
        Assert.Equal(SimulatorKind.XPlane, snapshot.Simulator);
        Assert.Equal("12.4.0", snapshot.SimulatorVersion);
        Assert.Equal("Cessna 172", snapshot.AircraftTitle);
        Assert.Equal("N12345", snapshot.AircraftTailNumber);
        Assert.Equal(48.8566, snapshot.LatitudeDeg);
        Assert.Equal(2.3522, snapshot.LongitudeDeg);
        Assert.Equal(3280.839895, snapshot.AltitudeFt!.Value, 6);
        Assert.Equal(1640.4199475, snapshot.HeightAglFt!.Value, 6);
        Assert.Equal(120, snapshot.IndicatedAirspeedKt);
        Assert.Equal(252.69972, snapshot.TrueAirspeedKt!.Value, 3);
        Assert.Equal(272.13816, snapshot.GroundspeedKt!.Value, 3);
        Assert.Equal(-500, snapshot.VerticalSpeedFpm);
        Assert.Equal(180, snapshot.HeadingMagDeg);
        Assert.False(snapshot.IsPaused);
        Assert.True(snapshot.IsFlightLoaded);

        Assert.Equal(new Uri("ws://127.0.0.1:8086/api/v3"), socket.ConnectedUri);
        Assert.Contains("dataref_subscribe_values", socket.SubscribeJson);
        Assert.Contains("\"id\":2", socket.SubscribeJson);
    }

    [Fact]
    public async Task Probe_Returns_Unavailable_When_Capabilities_Are_Missing()
    {
        var rest = new FakeRest { Capabilities = null };
        await using var provider = new XPlaneWebApiProvider(
            new XPlaneOptions(),
            rest,
            () => new FakeSocket());

        var result = await provider.ProbeAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
    }

    private sealed class FakeRest : IXPlaneRestClient
    {
        public Dictionary<string, long> Ids { get; } = new();

        public Dictionary<long, string?> Data { get; } = new();

        public XPlaneCapabilities? Capabilities { get; set; } =
            new("12.4.0", new[] { "v1", "v2", "v3" });

        public Task<XPlaneCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Capabilities);

        public Task<long?> ResolveDatarefAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Ids.TryGetValue(name, out var id) ? id : (long?)null);

        public Task<string?> GetDatarefDataStringAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Data.TryGetValue(id, out var value) ? value : null);

        public void Dispose()
        {
        }
    }

    private sealed class FakeSocket : IXPlaneWebSocket
    {
        private readonly Queue<string> _messages;

        public FakeSocket(params string[] messages) => _messages = new Queue<string>(messages);

        public Uri? ConnectedUri { get; private set; }

        public string? SubscribeJson { get; private set; }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            ConnectedUri = uri;
            return Task.CompletedTask;
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            SubscribeJson = text;
            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            if (_messages.Count > 0)
            {
                return _messages.Dequeue();
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
