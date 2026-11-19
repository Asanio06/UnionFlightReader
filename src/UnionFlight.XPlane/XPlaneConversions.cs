namespace UnionFlight.XPlane;

internal static class XPlaneConversions
{
    public const double MetersToFeet = 3.280839895;
    public const double MetersPerSecondToKnots = 1.943844;

    public static double? MetersToFeetValue(double? value) => value * MetersToFeet;

    public static double? MetersPerSecondToKnotsValue(double? value) =>
        value * MetersPerSecondToKnots;
}
