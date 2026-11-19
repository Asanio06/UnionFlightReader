namespace UnionFlight;

/// <summary>
/// Immutable telemetry snapshot for the user aircraft.
/// Units are explicit in the property names; no implicit conversions.
/// </summary>
public sealed record FlightSnapshot
{
    public required SimulatorKind Simulator { get; init; }

    public string? SimulatorVersion { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>True when a flight is loaded (not the main menu or a loading screen).</summary>
    public bool IsFlightLoaded { get; init; }

    /// <summary>Null when the simulator does not expose pause state.</summary>
    public bool? IsPaused { get; init; }

    public string? AircraftTitle { get; init; }

    public string? AircraftTailNumber { get; init; }

    public double? LatitudeDeg { get; init; }

    public double? LongitudeDeg { get; init; }

    public double? AltitudeFt { get; init; }

    public double? HeightAglFt { get; init; }

    public double? IndicatedAirspeedKt { get; init; }

    public double? TrueAirspeedKt { get; init; }

    public double? GroundspeedKt { get; init; }

    public double? VerticalSpeedFpm { get; init; }

    public double? HeadingMagDeg { get; init; }

    public bool? OnGround { get; init; }
}
