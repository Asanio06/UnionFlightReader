namespace UnionFlight;

/// <summary>Result of probing a simulator provider.</summary>
public sealed record SimulatorProbeResult(bool IsAvailable, string? Version, string? Detail)
{
    public static SimulatorProbeResult Unavailable(string detail) => new(false, null, detail);

    public static SimulatorProbeResult Available(string? version, string? detail = null) =>
        new(true, version, detail);
}
