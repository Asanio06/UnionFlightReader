using UnionFlight.TestKit;
using UnionFlight.XPlane.Udp;
using UnionFlight.XPlane.WebApi;

namespace UnionFlight.XPlane.Tests;

/// <summary>
/// End-to-end tests against <see cref="FakeXPlaneServer"/>: real HttpClient,
/// real ClientWebSocket, real UdpClient. No simulator required.
/// </summary>
public sealed class XPlaneEndToEndTests
{
    [Fact]
    public async Task WebApi_Provider_Reads_A_Complete_Snapshot()
    {
        await using var fake = await FakeXPlaneServer.StartAsync(FakeXPlaneState.CreateDefault());

        var options = new XPlaneOptions
        {
            Host = "127.0.0.1",
            WebApiPort = fake.WebApiPort,
            UdpPort = fake.UdpPort,
            SnapshotInterval = TimeSpan.FromMilliseconds(10)
        };

        await using var provider = new XPlaneWebApiProvider(options);
        var snapshot = await WaitForSnapshotAsync(
            provider,
            candidate => candidate.AircraftTitle == "Cessna 172" &&
                         candidate.AircraftTailNumber == "N12345");

        AssertSnapshot(snapshot);
    }

    [Fact]
    public async Task Udp_Provider_Reads_A_Complete_Snapshot()
    {
        await using var fake = await FakeXPlaneServer.StartAsync(FakeXPlaneState.CreateDefault());

        var options = new XPlaneOptions
        {
            Host = "127.0.0.1",
            WebApiPort = fake.WebApiPort,
            UdpPort = fake.UdpPort,
            PreferWebApi = false,
            SnapshotInterval = TimeSpan.FromMilliseconds(10)
        };

        await using var provider = new XPlaneUdpProvider(options);
        var snapshot = await WaitForSnapshotAsync(
            provider,
            candidate => candidate.AircraftTitle == "Cessna 172" &&
                         candidate.AircraftTailNumber == "N12345");

        AssertSnapshot(snapshot);
    }

    [Fact]
    public async Task Composite_Provider_Prefers_WebApi_When_Available()
    {
        await using var fake = await FakeXPlaneServer.StartAsync(FakeXPlaneState.CreateDefault());

        var options = new XPlaneOptions
        {
            Host = "127.0.0.1",
            WebApiPort = fake.WebApiPort,
            UdpPort = fake.UdpPort,
            PreferWebApi = true,
            SnapshotInterval = TimeSpan.FromMilliseconds(10)
        };

        await using var provider = new XPlaneProvider(options);
        var snapshot = await WaitForSnapshotAsync(
            provider,
            candidate => candidate.AircraftTitle == "Cessna 172" &&
                         candidate.AircraftTailNumber == "N12345");

        AssertSnapshot(snapshot);
    }

    [Fact]
    public async Task Udp_Probe_Succeeds_Against_The_Fake_Server()
    {
        await using var fake = await FakeXPlaneServer.StartAsync(FakeXPlaneState.CreateDefault());
        await using var provider = new XPlaneUdpProvider(new XPlaneOptions
        {
            Host = "127.0.0.1",
            UdpPort = fake.UdpPort
        });

        var result = await provider.ProbeAsync(CancellationToken.None);

        Assert.True(result.IsAvailable);
    }

    [Fact]
    public async Task WebApi_Provider_Falls_Back_To_Description_When_UiName_Is_Missing()
    {
        var state = FakeXPlaneState.CreateDefault();
        state.Strings.Remove("sim/aircraft/view/acf_ui_name");

        await using var fake = await FakeXPlaneServer.StartAsync(state);

        var options = new XPlaneOptions
        {
            Host = "127.0.0.1",
            WebApiPort = fake.WebApiPort,
            UdpPort = fake.UdpPort,
            SnapshotInterval = TimeSpan.FromMilliseconds(10)
        };

        await using var provider = new XPlaneWebApiProvider(options);
        var snapshot = await WaitForSnapshotAsync(
            provider,
            candidate => candidate.AircraftTitle == "Cessna 172 (description)");

        Assert.Equal("Cessna 172 (description)", snapshot.AircraftTitle);
    }

    [Fact]
    public async Task Udp_Provider_Falls_Back_To_Description_When_UiName_Is_Missing()
    {
        var state = FakeXPlaneState.CreateDefault();
        state.Strings.Remove("sim/aircraft/view/acf_ui_name");

        await using var fake = await FakeXPlaneServer.StartAsync(state);

        var options = new XPlaneOptions
        {
            Host = "127.0.0.1",
            WebApiPort = fake.WebApiPort,
            UdpPort = fake.UdpPort,
            PreferWebApi = false,
            SnapshotInterval = TimeSpan.FromMilliseconds(10)
        };

        await using var provider = new XPlaneUdpProvider(options);
        var snapshot = await WaitForSnapshotAsync(
            provider,
            candidate => candidate.AircraftTitle == "Cessna 172 (description)");

        Assert.Equal("Cessna 172 (description)", snapshot.AircraftTitle);
    }

    private static async Task<FlightSnapshot> WaitForSnapshotAsync(
        IFlightSimProvider provider,
        Func<FlightSnapshot, bool>? predicate = null)
    {
        var received = new TaskCompletionSource<FlightSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        provider.SnapshotReceived += (_, snapshot) =>
        {
            if (predicate is null || predicate(snapshot))
            {
                received.TrySetResult(snapshot);
            }
        };

        await provider.StartAsync(CancellationToken.None);
        return await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void AssertSnapshot(FlightSnapshot snapshot)
    {
        Assert.Equal(SimulatorKind.XPlane, snapshot.Simulator);
        Assert.Equal("12.4.0", snapshot.SimulatorVersion);
        Assert.Equal("Cessna 172", snapshot.AircraftTitle);
        Assert.Equal("N12345", snapshot.AircraftTailNumber);

        Assert.Equal(48.8566, snapshot.LatitudeDeg!.Value, 4);
        Assert.Equal(2.3522, snapshot.LongitudeDeg!.Value, 4);
        Assert.Equal(3280.839895, snapshot.AltitudeFt!.Value, 6);
        Assert.Equal(1640.4199475, snapshot.HeightAglFt!.Value, 6);
        Assert.Equal(120, snapshot.IndicatedAirspeedKt!.Value, 6);
        Assert.Equal(252.69972, snapshot.TrueAirspeedKt!.Value, 3);
        Assert.Equal(272.13816, snapshot.GroundspeedKt!.Value, 3);
        Assert.Equal(-500, snapshot.VerticalSpeedFpm!.Value, 6);
        Assert.Equal(180, snapshot.HeadingMagDeg!.Value, 6);

        Assert.False(snapshot.IsPaused);
        Assert.True(snapshot.IsFlightLoaded);
    }
}
