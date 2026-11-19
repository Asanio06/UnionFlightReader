namespace UnionFlight.Abstractions.Tests;

public sealed class FlightSnapshotTests
{
    [Fact]
    public void Snapshot_Preserves_Explicit_Units()
    {
        var snapshot = new FlightSnapshot
        {
            Simulator = SimulatorKind.XPlane,
            TimestampUtc = DateTimeOffset.UnixEpoch,
            AltitudeFt = 35_000,
            GroundspeedKt = 450,
            VerticalSpeedFpm = -1_200
        };

        Assert.Equal(35_000, snapshot.AltitudeFt);
        Assert.Equal(450, snapshot.GroundspeedKt);
        Assert.Equal(-1_200, snapshot.VerticalSpeedFpm);
    }

    [Fact]
    public void Snapshot_Keeps_Unknown_Values_Null()
    {
        var snapshot = new FlightSnapshot
        {
            Simulator = SimulatorKind.Unknown,
            TimestampUtc = DateTimeOffset.UnixEpoch
        };

        Assert.Null(snapshot.AltitudeFt);
        Assert.Null(snapshot.IsPaused);
        Assert.Null(snapshot.AircraftTitle);
        Assert.False(snapshot.IsFlightLoaded);
    }

    [Fact]
    public void Snapshot_Is_Immutable()
    {
        var snapshot = new FlightSnapshot
        {
            Simulator = SimulatorKind.XPlane,
            TimestampUtc = DateTimeOffset.UnixEpoch,
            AltitudeFt = 1_000
        };

        var copy = snapshot with { AltitudeFt = 2_000 };

        Assert.Equal(1_000, snapshot.AltitudeFt);
        Assert.Equal(2_000, copy.AltitudeFt);
    }
}
