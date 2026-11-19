using System.Net;
using System.Text;
using System.Text.Json;

namespace UnionFlight.XPlane.WebApi;

public sealed class XPlaneRestClient : IXPlaneRestClient
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private string _apiVersion = "v3";

    public XPlaneRestClient(XPlaneOptions options)
        : this(
            new HttpClient
            {
                BaseAddress = new Uri($"http://{options.Host}:{options.WebApiPort}"),
                Timeout = TimeSpan.FromSeconds(5)
            },
            ownsHttp: true)
    {
    }

    public XPlaneRestClient(HttpClient http, bool ownsHttp = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = ownsHttp;
    }

    public async Task<XPlaneCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("/api/capabilities", cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new XPlaneApiException(
                "forbidden",
                "X-Plane Web API is disabled (check the Network security setting).");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        var root = document.RootElement;
        var versions = root.GetProperty("api")
            .GetProperty("versions")
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();

        var version = root.GetProperty("x-plane").GetProperty("version").GetString() ?? "unknown";
        var capabilities = new XPlaneCapabilities(version, versions);
        _apiVersion = capabilities.PreferredApiVersion;
        return capabilities;
    }

    public async Task<long?> ResolveDatarefAsync(string name, CancellationToken cancellationToken)
    {
        var url = $"/api/{_apiVersion}/datarefs?filter[name]={Uri.EscapeDataString(name)}";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        var data = document.RootElement.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
        {
            return null;
        }

        return data[0].GetProperty("id").GetInt64();
    }

    public async Task<string?> GetDatarefDataStringAsync(long id, CancellationToken cancellationToken)
    {
        var url = $"/api/{_apiVersion}/datarefs/{id}/value";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        var data = document.RootElement.GetProperty("data");
        if (data.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var base64 = data.GetString();
        if (string.IsNullOrEmpty(base64))
        {
            return null;
        }

        var bytes = Convert.FromBase64String(base64);
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes, 0, length).Trim();
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
