using Orbit.Core.Providers;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

public interface IDomainService
{
    Task<IReadOnlyList<DomainDto>> ListAsync(CancellationToken ct = default);
    Task<DomainDto> CreateAsync(CreateDomainRequest request, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<DnsRecordDto>> ListRecordsAsync(string domain, CancellationToken ct = default);
    Task<DnsRecordDto> CreateRecordAsync(string domain, DnsRecordRequest request, CancellationToken ct = default);
    Task<DnsRecordDto> UpdateRecordAsync(string domain, long recordId, DnsRecordRequest request, CancellationToken ct = default);
    Task DeleteRecordAsync(string domain, long recordId, CancellationToken ct = default);
}

public sealed class DomainService(IConnectionService connections, IDnsProvider dns) : IDomainService
{
    private async Task<string> TokenAsync(CancellationToken ct) => await connections.GetTokenAsync(ProviderType.DigitalOcean, ct);

    public async Task<IReadOnlyList<DomainDto>> ListAsync(CancellationToken ct = default)
        => await dns.ListDomainsAsync(await TokenAsync(ct), ct);

    public async Task<DomainDto> CreateAsync(CreateDomainRequest request, CancellationToken ct = default)
        => await dns.CreateDomainAsync(await TokenAsync(ct), request, ct);

    public async Task DeleteAsync(string name, CancellationToken ct = default)
        => await dns.DeleteDomainAsync(await TokenAsync(ct), name, ct);

    public async Task<IReadOnlyList<DnsRecordDto>> ListRecordsAsync(string domain, CancellationToken ct = default)
        => await dns.ListRecordsAsync(await TokenAsync(ct), domain, ct);

    public async Task<DnsRecordDto> CreateRecordAsync(string domain, DnsRecordRequest request, CancellationToken ct = default)
        => await dns.CreateRecordAsync(await TokenAsync(ct), domain, request, ct);

    public async Task<DnsRecordDto> UpdateRecordAsync(string domain, long recordId, DnsRecordRequest request, CancellationToken ct = default)
        => await dns.UpdateRecordAsync(await TokenAsync(ct), domain, recordId, request, ct);

    public async Task DeleteRecordAsync(string domain, long recordId, CancellationToken ct = default)
        => await dns.DeleteRecordAsync(await TokenAsync(ct), domain, recordId, ct);
}
