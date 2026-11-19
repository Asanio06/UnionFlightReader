using UnionFlight.TestKit;

namespace UnionFlight.Core.Tests;

public sealed class FlightSimMonitorTests
{
    private static FlightSimMonitor CreateMonitor(
        params IFlightSimProvider[] providers) =>
        new(
            providers,
            options: new FlightSimMonitorOptions
            {
                ProbeTimeout = TimeSpan.FromMilliseconds(500),
                RestartDelay = TimeSpan.FromMilliseconds(10)
            });

    private static FlightSnapshot Snapshot(string title = "Test aircraft") =>
        new()
        {
            Simulator = SimulatorKind.XPlane,
            TimestampUtc = DateTimeOffset.UnixEpoch,
            AircraftTitle = title
        };

    [Fact]
    public async Task StartAsync_Picks_First_Available_Provider()
    {
        var unavailable = new FakeFlightSimProvider(SimulatorKind.Msfs2024, "MSFS", available: false);
        var available = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane");

        await using var monitor = CreateMonitor(unavailable, available);
        await monitor.StartAsync(CancellationToken.None);

        Assert.Same(available, monitor.ActiveProvider);
        Assert.Equal(0, unavailable.StartCount);
        Assert.Equal(1, available.StartCount);
    }

    [Fact]
    public async Task StartAsync_When_No_Provider_Is_Available_Leaves_Active_Null()
    {
        var provider = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane", available: false);

        await using var monitor = CreateMonitor(provider);
        await monitor.StartAsync(CancellationToken.None);

        Assert.Null(monitor.ActiveProvider);
        Assert.Null(monitor.Latest);
    }

    [Fact]
    public async Task StartAsync_Forwards_Snapshots()
    {
        var provider = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane");

        await using var monitor = CreateMonitor(provider);
        FlightSnapshot? received = null;
        monitor.SnapshotReceived += (_, snapshot) => received = snapshot;

        await monitor.StartAsync(CancellationToken.None);
        provider.Emit(Snapshot("Cessna"));

        Assert.Equal("Cessna", received?.AircraftTitle);
        Assert.Equal("Cessna", monitor.Latest?.AircraftTitle);
    }

    [Fact]
    public async Task Fault_ReProbes_And_Selects_Another_Provider()
    {
        var first = new FakeFlightSimProvider(SimulatorKind.Msfs2024, "MSFS");
        var second = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane");

        await using var monitor = CreateMonitor(first, second);
        await monitor.StartAsync(CancellationToken.None);
        Assert.Same(first, monitor.ActiveProvider);

        first.IsAvailable = false;
        first.Fail(new InvalidOperationException("simulator closed"));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (monitor.ActiveProvider != second && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Same(second, monitor.ActiveProvider);
        Assert.Equal(1, second.StartCount);
    }

    [Fact]
    public async Task StopAsync_Stops_The_Active_Provider()
    {
        var provider = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane");

        await using var monitor = CreateMonitor(provider);
        await monitor.StartAsync(CancellationToken.None);
        await monitor.StopAsync(CancellationToken.None);

        Assert.Null(monitor.ActiveProvider);
        Assert.Equal(1, provider.StopCount);
    }

    [Fact]
    public async Task DisposeAsync_Disposes_All_Providers()
    {
        var provider = new FakeFlightSimProvider(SimulatorKind.XPlane, "X-Plane");
        var monitor = CreateMonitor(provider);

        await monitor.StartAsync(CancellationToken.None);
        await monitor.DisposeAsync();

        Assert.Equal(1, provider.DisposeCount);
    }
}
