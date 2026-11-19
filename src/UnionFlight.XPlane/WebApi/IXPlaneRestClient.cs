namespace UnionFlight.XPlane.WebApi;

public interface IXPlaneRestClient : IDisposable
{
    Task<XPlaneCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken);

    Task<long?> ResolveDatarefAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a <c>data</c> dataref (base64 byte array) and returns it as a trimmed UTF-8 string.
    /// </summary>
    Task<string?> GetDatarefDataStringAsync(long id, CancellationToken cancellationToken);
}
