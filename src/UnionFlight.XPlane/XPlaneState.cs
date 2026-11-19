namespace UnionFlight.XPlane;

/// <summary>
/// Mutable accumulator for X-Plane values. The transport threads write it;
/// the publisher reads it to build an immutable <see cref="FlightSnapshot"/>.
/// </summary>
internal sealed class XPlaneState
{
    public const int TailNumberLength = 40;
    public const int UiNameLength = 250;
    public const int DescriptionLength = 260;

    public double? LatitudeDeg;
    public double? LongitudeDeg;
    public double? ElevationM;
    public double? HeightAglM;
    public double? IndicatedAirspeedKt;
    public double? TrueAirspeedMs;
    public double? GroundspeedMs;
    public double? VerticalSpeedFpm;
    public double? HeadingMagDeg;
    public int? Paused;
    public double? TotalFlightTimeSec;
    public int? InternalVersion;
    public bool HasAircraftData;

    public readonly char[] TailNumber = new char[TailNumberLength];
    public readonly char[] UiName = new char[UiNameLength];
    public readonly char[] Description = new char[DescriptionLength];

    public bool TailNumberReady;
    public bool UiNameReady;
    public bool DescriptionReady;

    public string? GetTailNumber() => TailNumberReady ? ReadString(TailNumber) : null;

    public string? GetTitle()
    {
        var uiName = UiNameReady ? ReadString(UiName) : null;
        if (uiName is not null)
        {
            return uiName;
        }

        return DescriptionReady ? ReadString(Description) : null;
    }

    public void SetString(char[] buffer, int index, float value)
    {
        if ((uint)index >= (uint)buffer.Length)
        {
            return;
        }

        if (index == 0)
        {
            Array.Clear(buffer);
        }

        var code = (int)value;
        if (code > 0 && code <= char.MaxValue)
        {
            buffer[index] = (char)code;
        }

        // A NUL terminates the string; the last index also marks completion
        // when the simulator pads the whole buffer. This tolerates a lost
        // trailing packet.
        if (code == 0 || index == buffer.Length - 1)
        {
            MarkReady(buffer);
        }
    }

    public void SetString(char[] buffer, string? value)
    {
        Array.Clear(buffer);
        if (!string.IsNullOrEmpty(value))
        {
            value.CopyTo(0, buffer, 0, Math.Min(value.Length, buffer.Length));
        }

        MarkReady(buffer);
    }

    private void MarkReady(char[] buffer)
    {
        if (ReferenceEquals(buffer, TailNumber))
        {
            TailNumberReady = true;
        }
        else if (ReferenceEquals(buffer, UiName))
        {
            UiNameReady = true;
        }
        else if (ReferenceEquals(buffer, Description))
        {
            DescriptionReady = true;
        }
    }

    public FlightSnapshot ToSnapshot(SimulatorKind kind, string? version)
    {
        return new FlightSnapshot
        {
            Simulator = kind,
            SimulatorVersion = version ?? FormatVersion(InternalVersion),
            TimestampUtc = DateTimeOffset.UtcNow,
            IsFlightLoaded = HasAircraftData || (TotalFlightTimeSec ?? 0) > 0,
            IsPaused = Paused is null ? null : Paused != 0,
            AircraftTitle = GetTitle(),
            AircraftTailNumber = GetTailNumber(),
            LatitudeDeg = LatitudeDeg,
            LongitudeDeg = LongitudeDeg,
            AltitudeFt = XPlaneConversions.MetersToFeetValue(ElevationM),
            HeightAglFt = XPlaneConversions.MetersToFeetValue(HeightAglM),
            IndicatedAirspeedKt = IndicatedAirspeedKt,
            TrueAirspeedKt = XPlaneConversions.MetersPerSecondToKnotsValue(TrueAirspeedMs),
            GroundspeedKt = XPlaneConversions.MetersPerSecondToKnotsValue(GroundspeedMs),
            VerticalSpeedFpm = VerticalSpeedFpm,
            HeadingMagDeg = HeadingMagDeg,
            OnGround = null
        };
    }

    private static string? ReadString(char[] buffer)
    {
        var length = Array.IndexOf(buffer, '\0');
        if (length < 0)
        {
            length = buffer.Length;
        }

        if (length == 0)
        {
            return null;
        }

        var value = new string(buffer, 0, length).Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? FormatVersion(int? internalVersion)
    {
        if (internalVersion is null or <= 0)
        {
            return null;
        }

        var value = internalVersion.Value;
        return $"{value / 10000}.{(value / 100) % 100}.{value % 100}";
    }
}
