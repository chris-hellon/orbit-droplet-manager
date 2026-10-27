using Orbit.Core.Providers;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers.DigitalOcean;

/// <summary>DigitalOcean DNS: domains and records via /v2/domains.</summary>
public sealed class DigitalOceanDnsProvider(DigitalOceanApi api) : IDnsProvider
{
    public async Task<IReadOnlyList<DomainDto>> ListDomainsAsync(string token, CancellationToken ct = default)
    {
        var domains = await api.ListAllAsync<DoDomainsResponse, DoDomain>(
            token, "/v2/domains?per_page=200", r => r.Domains, r => r.Links?.Pages?.Next, ct);
        return domains.Select(Map).ToList();
    }

    public async Task<DomainDto> CreateDomainAsync(string token, CreateDomainRequest request, CancellationToken ct = default)
    {
        var body = request.IpAddress is { Length: > 0 }
            ? new { name = request.Name.Trim(), ip_address = request.IpAddress.Trim() }
            : (object)new { name = request.Name.Trim() };
        var res = await api.PostAsync<DoDomainResponse>(token, "/v2/domains", body, ct);
        return Map(res.Domain);
    }

    public Task DeleteDomainAsync(string token, string name, CancellationToken ct = default)
        => api.SendNoContentAsync(token, HttpMethod.Delete, $"/v2/domains/{Uri.EscapeDataString(name)}", ct: ct);

    public async Task<IReadOnlyList<DnsRecordDto>> ListRecordsAsync(string token, string domain, CancellationToken ct = default)
    {
        var records = await api.ListAllAsync<DoRecordsResponse, DoDomainRecord>(
            token, $"/v2/domains/{Uri.EscapeDataString(domain)}/records?per_page=200", r => r.Records, r => r.Links?.Pages?.Next, ct);
        return records.Select(Map).ToList();
    }

    public async Task<DnsRecordDto> CreateRecordAsync(string token, string domain, DnsRecordRequest request, CancellationToken ct = default)
    {
        var res = await api.PostAsync<DoRecordResponse>(token, $"/v2/domains/{Uri.EscapeDataString(domain)}/records", ToBody(request), ct);
        return Map(res.Record);
    }

    public async Task<DnsRecordDto> UpdateRecordAsync(string token, string domain, long recordId, DnsRecordRequest request, CancellationToken ct = default)
    {
        var res = await api.PutAsync<DoRecordResponse>(token, $"/v2/domains/{Uri.EscapeDataString(domain)}/records/{recordId}", ToBody(request), ct);
        return Map(res.Record);
    }

    public Task DeleteRecordAsync(string token, string domain, long recordId, CancellationToken ct = default)
        => api.SendNoContentAsync(token, HttpMethod.Delete, $"/v2/domains/{Uri.EscapeDataString(domain)}/records/{recordId}", ct: ct);

    private static object ToBody(DnsRecordRequest r) => new
    {
        type = r.Type.Trim().ToUpperInvariant(),
        name = r.Name.Trim(),
        data = r.Data.Trim(),
        priority = r.Priority,
        port = r.Port,
        weight = r.Weight,
        ttl = r.Ttl <= 0 ? 3600 : r.Ttl,
        flags = r.Flags,
        tag = r.Tag,
    };

    private static DomainDto Map(DoDomain d) => new() { Name = d.Name, Ttl = d.Ttl ?? 1800 };

    private static DnsRecordDto Map(DoDomainRecord r) => new()
    {
        Id = r.Id,
        Type = r.Type,
        Name = r.Name,
        Data = r.Data,
        Priority = r.Priority,
        Port = r.Port,
        Weight = r.Weight,
        Ttl = r.Ttl ?? 3600,
        Flags = r.Flags,
        Tag = r.Tag,
    };
}
