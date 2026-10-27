using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orbit.Core.Providers;

namespace Orbit.Core.Providers.DigitalOcean;

/// <summary>
/// Shared HTTP plumbing for every DigitalOcean resource provider: bearer auth, JSON (de)serialization,
/// friendly error mapping and cursor pagination. Stateless — the token is supplied per call.
/// </summary>
public sealed class DigitalOceanApi(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "digitalocean";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<T> GetAsync<T>(string token, string path, CancellationToken ct = default)
        => SendAsync<T>(token, HttpMethod.Get, path, null, ct);

    public Task<T> PostAsync<T>(string token, string path, object? body, CancellationToken ct = default)
        => SendAsync<T>(token, HttpMethod.Post, path, body, ct);

    public Task<T> PutAsync<T>(string token, string path, object? body, CancellationToken ct = default)
        => SendAsync<T>(token, HttpMethod.Put, path, body, ct);

    /// <summary>For DELETE and other calls that return 204 No Content.</summary>
    public async Task SendNoContentAsync(string token, HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(token, method, path, body, ct);
        if (!response.IsSuccessStatusCode)
            throw new ProviderException(await ReadErrorAsync(response, ct), (int)response.StatusCode);
    }

    public async Task<T> SendAsync<T>(string token, HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        using var response = await SendRawAsync(token, method, path, body, ct);

        if (!response.IsSuccessStatusCode)
            throw new ProviderException(await ReadErrorAsync(response, ct), (int)response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NoContent)
            return default!;

        var payload = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        return payload ?? throw new ProviderException("DigitalOcean returned an empty response.");
    }

    /// <summary>
    /// Walk every page of a list endpoint, following <c>links.pages.next</c>. The selector pulls the
    /// item list out of each page's response envelope.
    /// </summary>
    public async Task<List<TItem>> ListAllAsync<TResponse, TItem>(
        string token, string firstPath, Func<TResponse, IEnumerable<TItem>> items, Func<TResponse, string?> next, CancellationToken ct = default)
    {
        var result = new List<TItem>();
        string? path = firstPath;
        while (path is not null)
        {
            var page = await GetAsync<TResponse>(token, path, ct);
            result.AddRange(items(page));
            var nextUrl = next(page);
            path = string.IsNullOrWhiteSpace(nextUrl) ? null : ToRelative(nextUrl);
        }
        return result;
    }

    private async Task<HttpResponseMessage> SendRawAsync(string token, HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        try
        {
            return await client.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException($"Couldn't reach DigitalOcean — {ex.Message}", 502);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ProviderException("The DigitalOcean request timed out. Try again.", 504);
        }
    }

    private async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        var friendly = status switch
        {
            401 => "DigitalOcean rejected the token (401). Check that it's a valid personal access token with read/write scope.",
            403 => "The token doesn't have permission for that operation (403).",
            404 => "DigitalOcean returned 404 (not found).",
            422 => "DigitalOcean rejected the request (422).",
            429 => "DigitalOcean rate limit hit (429). Try again shortly.",
            >= 500 => $"DigitalOcean had a server error ({status}). Try again shortly.",
            _ => $"DigitalOcean request failed ({status}).",
        };

        try
        {
            var err = await response.Content.ReadFromJsonAsync<DoErrorResponse>(Json, ct);
            if (!string.IsNullOrWhiteSpace(err?.Message))
                return $"{friendly} — {err!.Message}";
        }
        catch
        {
            // non-JSON error body; fall through
        }
        return friendly;
    }

    public static string ToRelative(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var abs) ? abs.PathAndQuery : url;

    /// <summary>Parse a Prometheus-style metric value string (invariant culture).</summary>
    public static double? ParseMetricValue(string? s)
        => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}
