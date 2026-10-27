using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbit.Core.Providers.DigitalOcean;

// Minimal shapes of the DigitalOcean v2 API responses we consume. Only the fields Orbit uses are mapped.

internal sealed record DoAccountResponse([property: JsonPropertyName("account")] DoAccount Account);

internal sealed record DoAccount(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("uuid")] string Uuid,
    [property: JsonPropertyName("droplet_limit")] int DropletLimit,
    [property: JsonPropertyName("status")] string Status);

internal sealed record DoDropletsResponse(
    [property: JsonPropertyName("droplets")] List<DoDroplet> Droplets,
    [property: JsonPropertyName("links")] DoLinks? Links);

internal sealed record DoLinks([property: JsonPropertyName("pages")] DoPages? Pages);
internal sealed record DoPages([property: JsonPropertyName("next")] string? Next);

internal sealed record DoDroplet(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("memory")] int Memory,
    [property: JsonPropertyName("vcpus")] int Vcpus,
    [property: JsonPropertyName("disk")] int Disk,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("features")] List<string>? Features,
    [property: JsonPropertyName("tags")] List<string>? Tags,
    [property: JsonPropertyName("size_slug")] string SizeSlug,
    [property: JsonPropertyName("size")] DoSize? Size,
    [property: JsonPropertyName("region")] DoRegion? Region,
    [property: JsonPropertyName("image")] DoImage? Image,
    [property: JsonPropertyName("networks")] DoNetworks? Networks);

internal sealed record DoSize([property: JsonPropertyName("price_monthly")] decimal PriceMonthly);

internal sealed record DoRegionsResponse([property: JsonPropertyName("regions")] List<DoRegion> Regions);

internal sealed record DoRegion(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("available")] bool Available);

internal sealed record DoImage(
    [property: JsonPropertyName("distribution")] string? Distribution,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record DoNetworks(
    [property: JsonPropertyName("v4")] List<DoNetworkV4>? V4,
    [property: JsonPropertyName("v6")] List<DoNetworkV6>? V6);

internal sealed record DoNetworkV4(
    [property: JsonPropertyName("ip_address")] string IpAddress,
    [property: JsonPropertyName("type")] string Type);

internal sealed record DoNetworkV6(
    [property: JsonPropertyName("ip_address")] string IpAddress,
    [property: JsonPropertyName("type")] string Type);

internal sealed record DoSizesResponse(
    [property: JsonPropertyName("sizes")] List<DoSizeFull> Sizes,
    [property: JsonPropertyName("links")] DoLinks? Links);

internal sealed record DoSizeFull(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("memory")] int Memory,
    [property: JsonPropertyName("vcpus")] int Vcpus,
    [property: JsonPropertyName("disk")] int Disk,
    [property: JsonPropertyName("price_monthly")] decimal PriceMonthly,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("regions")] List<string>? Regions);

internal sealed record DoImagesResponse(
    [property: JsonPropertyName("images")] List<DoImageFull> Images,
    [property: JsonPropertyName("links")] DoLinks? Links);

internal sealed record DoImageFull(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("distribution")] string? Distribution,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

internal sealed record DoActionResponse([property: JsonPropertyName("action")] DoAction Action);

internal sealed record DoAction(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("started_at")] DateTimeOffset StartedAt);

internal sealed record DoErrorResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("message")] string? Message);

// ---- Monitoring (Prometheus-style matrix) ----

internal sealed record DoMetricsResponse([property: JsonPropertyName("data")] DoMetricsData? Data);

internal sealed record DoMetricsData(
    [property: JsonPropertyName("resultType")] string? ResultType,
    [property: JsonPropertyName("result")] List<DoMetricsSeries>? Result);

internal sealed record DoMetricsSeries(
    [property: JsonPropertyName("metric")] Dictionary<string, string>? Metric,
    [property: JsonPropertyName("values")] List<JsonElement>? Values);
