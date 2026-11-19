namespace UnionFlight.XPlane.Tests;

public sealed class XPlaneStateTests
{
    [Fact]
    public void ToSnapshot_Converts_Units()
    {
        var state = new XPlaneState
        {
            LatitudeDeg = 48.8566,
            LongitudeDeg = 2.3522,
            ElevationM = 1000,
            HeightAglM = 500,
            IndicatedAirspeedKt = 120,
            TrueAirspeedMs = 100,
            GroundspeedMs = 50,
            VerticalSpeedFpm = -500,
            HeadingMagDeg = 180,
            HasAircraftData = true
        };

        var snapshot = state.ToSnapshot(SimulatorKind.XPlane, "12.4.0");

        Assert.Equal(48.8566, snapshot.LatitudeDeg);
        Assert.Equal(3280.839895, snapshot.AltitudeFt!.Value, 6);
        Assert.Equal(1640.4199475, snapshot.HeightAglFt!.Value, 6);
        Assert.Equal(120, snapshot.IndicatedAirspeedKt);
        Assert.Equal(194.3844, snapshot.TrueAirspeedKt!.Value, 3);
        Assert.Equal(97.1922, snapshot.GroundspeedKt!.Value, 3);
        Assert.Equal(-500, snapshot.VerticalSpeedFpm);
        Assert.Equal(180, snapshot.HeadingMagDeg);
        Assert.True(snapshot.IsFlightLoaded);
        Assert.Equal("12.4.0", snapshot.SimulatorVersion);
    }

    [Fact]
    public void SetString_Assembles_TailNumber()
    {
        var state = new XPlaneState();
        const string tail = "N12345";

        for (var i = 0; i < tail.Length; i++)
        {
            XPlaneState.SetString(state.TailNumber, i, tail[i]);
        }

        Assert.Equal(tail, state.GetTailNumber());
    }

    [Fact]
    public void GetTitle_Prefers_UiName_And_Falls_Back_To_Description()
    {
        var state = new XPlaneState();
        XPlaneState.SetString(state.Description, "Cessna 172 (XP11)");
        Assert.Equal("Cessna 172 (XP11)", state.GetTitle());

        XPlaneState.SetString(state.UiName, "Cessna 172 (XP12)");
        Assert.Equal("Cessna 172 (XP12)", state.GetTitle());
    }

    [Fact]
    public void Paused_Semantics_Are_Explicit()
    {
        var state = new XPlaneState();
        Assert.Null(state.ToSnapshot(SimulatorKind.XPlane, null).IsPaused);

        state.Paused = 1;
        Assert.True(state.ToSnapshot(SimulatorKind.XPlane, null).IsPaused);

        state.Paused = 0;
        Assert.False(state.ToSnapshot(SimulatorKind.XPlane, null).IsPaused);
    }
}
