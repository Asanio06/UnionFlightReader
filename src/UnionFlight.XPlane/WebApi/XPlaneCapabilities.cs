namespace UnionFlight.XPlane.WebApi;

public sealed record XPlaneCapabilities(string Version, IReadOnlyList<string> ApiVersions)
{
    public string PreferredApiVersion =>
        ApiVersions.Contains("v3") ? "v3" :
        ApiVersions.Contains("v2") ? "v2" :
        ApiVersions.Contains("v1") ? "v1" : "v1";
}

public sealed class XPlaneApiException : Exception
{
    public XPlaneApiException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}
