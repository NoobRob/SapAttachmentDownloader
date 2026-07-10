using System.Net.Http.Headers;
using System.Text;

namespace SapAttachmentDownloader.Core;

/// <summary>
/// Duenner Wrapper um HttpClient fuer den Zugriff auf die SAP OData-V2-Services
/// (Basic Auth, wie in eurer Communication Arrangement konfiguriert).
/// </summary>
public class SapODataClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _host;

    public SapODataClient(SapApiOptions options)
    {
        _host = options.Host.TrimEnd('/');

        _http = new HttpClient();
        var basicAuth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.Username}:{options.Password}"));
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicAuth);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromMinutes(5);
    }

    /// <summary>Baut die volle URL aus relativem Pfad + Query-String.</summary>
    public string BuildUrl(string relativePath) => $"{_host}{relativePath}";

    public async Task<string> GetJsonAsync(string relativePath, CancellationToken ct = default)
    {
        var url = BuildUrl(relativePath);
        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new SapApiException(
                $"SAP-Aufruf fehlgeschlagen ({(int)response.StatusCode} {response.StatusCode}): {url}\n{body}");
        }

        return body;
    }

    public async Task<byte[]> GetBytesAsync(string relativePath, CancellationToken ct = default)
    {
        var url = BuildUrl(relativePath);
        using var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new SapApiException(
                $"Download fehlgeschlagen ({(int)response.StatusCode} {response.StatusCode}): {url}\n{body}");
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>
    /// Escaped einen String fuer die Verwendung als OData-V2-Literal in $filter oder
    /// einem Key-Predicate: einfache Anführungszeichen werden gemaess OData-Spec verdoppelt.
    /// </summary>
    public static string ODataLiteral(string value) => value.Replace("'", "''");

    public void Dispose() => _http.Dispose();
}

public class SapApiException : Exception
{
    public SapApiException(string message) : base(message) { }
}
