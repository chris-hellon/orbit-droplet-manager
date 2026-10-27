using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Client.Services;

/// <summary>Raised when an Orbit API call returns a non-success status; carries a user-facing message.</summary>
public sealed class OrbitApiException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Typed wrapper over the Orbit minimal-API surface, used by the WASM pages.</summary>
public sealed class OrbitApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ConnectionStatusDto> GetConnectionStatusAsync(CancellationToken ct = default)
        => GetAsync<ConnectionStatusDto>(ApiRoutes.Connection.Status, ct)!;

    public async Task<ProviderAccountDto> ValidateAsync(ConnectRequest request, CancellationToken ct = default)
        => await PostAsync<ConnectRequest, ProviderAccountDto>(ApiRoutes.Connection.Validate, request, ct);

    public async Task<ConnectionStatusDto> ConnectAsync(ConnectRequest request, CancellationToken ct = default)
        => await PostAsync<ConnectRequest, ConnectionStatusDto>(ApiRoutes.Connection.Connect, request, ct);

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        using var res = await http.DeleteAsync(ApiRoutes.Connection.Disconnect, ct);
        await EnsureSuccessAsync(res, ct);
    }

    public async Task<IReadOnlyList<DropletDto>> GetDropletsAsync(CancellationToken ct = default)
        => await GetAsync<List<DropletDto>>(ApiRoutes.Droplets.List, ct) ?? [];

    public Task<DropletDto?> GetDropletAsync(long id, CancellationToken ct = default)
        => GetAsync<DropletDto>(ApiRoutes.Droplets.Get(id), ct);

    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default)
        => await GetAsync<List<RegionDto>>(ApiRoutes.Regions.List, ct) ?? [];

    public async Task<IReadOnlyList<SizeDto>> GetSizesAsync(CancellationToken ct = default)
        => await GetAsync<List<SizeDto>>(ApiRoutes.Catalog.Sizes, ct) ?? [];

    public async Task<IReadOnlyList<ImageDto>> GetImagesAsync(CancellationToken ct = default)
        => await GetAsync<List<ImageDto>>(ApiRoutes.Catalog.Images, ct) ?? [];

    public async Task<IReadOnlyList<ActivityEntryDto>> GetActivityAsync(int take = 25, CancellationToken ct = default)
        => await GetAsync<List<ActivityEntryDto>>($"{ApiRoutes.Activity.List}?take={take}", ct) ?? [];

    public Task<DropletActionDto> ResizeAsync(long id, string size, bool resizeDisk, CancellationToken ct = default)
        => PostAsync<ResizeRequest, DropletActionDto>(ApiRoutes.Droplets.Resize(id), new ResizeRequest { Size = size, ResizeDisk = resizeDisk }, ct);

    public Task<DropletActionDto> RebuildAsync(long id, string image, CancellationToken ct = default)
        => PostAsync<RebuildRequest, DropletActionDto>(ApiRoutes.Droplets.Rebuild(id), new RebuildRequest { Image = image }, ct);

    public async Task<DropletMetricsDto> RefreshMetricsAsync(long id, CancellationToken ct = default)
        => await GetAsync<DropletMetricsDto>(ApiRoutes.Droplets.Metrics(id), ct)
           ?? throw new OrbitApiException("No metrics returned.", 500);

    public Task<DropletActionDto> PowerAsync(long id, PowerAction action, CancellationToken ct = default)
        => PostAsync<PowerActionRequest, DropletActionDto>(ApiRoutes.Droplets.Power(id), new PowerActionRequest { Action = action }, ct);

    public Task<DropletActionDto> SnapshotAsync(long id, string name, CancellationToken ct = default)
        => PostAsync<SnapshotRequest, DropletActionDto>(ApiRoutes.Droplets.Snapshot(id), new SnapshotRequest { Name = name }, ct);

    // ---------- domains / DNS ----------

    public async Task<IReadOnlyList<DomainDto>> GetDomainsAsync(CancellationToken ct = default)
        => await GetAsync<List<DomainDto>>(ApiRoutes.Domains.List, ct) ?? [];

    public Task<DomainDto> CreateDomainAsync(CreateDomainRequest request, CancellationToken ct = default)
        => PostAsync<CreateDomainRequest, DomainDto>(ApiRoutes.Domains.List, request, ct);

    public Task DeleteDomainAsync(string name, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Domains.Get(name), ct);

    public async Task<IReadOnlyList<DnsRecordDto>> GetRecordsAsync(string domain, CancellationToken ct = default)
        => await GetAsync<List<DnsRecordDto>>(ApiRoutes.Domains.Records(domain), ct) ?? [];

    public Task<DnsRecordDto> CreateRecordAsync(string domain, DnsRecordRequest request, CancellationToken ct = default)
        => PostAsync<DnsRecordRequest, DnsRecordDto>(ApiRoutes.Domains.Records(domain), request, ct);

    public Task<DnsRecordDto> UpdateRecordAsync(string domain, long id, DnsRecordRequest request, CancellationToken ct = default)
        => PutAsync<DnsRecordRequest, DnsRecordDto>(ApiRoutes.Domains.Record(domain, id), request, ct);

    public Task DeleteRecordAsync(string domain, long id, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Domains.Record(domain, id), ct);

    // ---------- managed databases ----------

    public async Task<IReadOnlyList<DatabaseClusterDto>> GetDatabasesAsync(CancellationToken ct = default)
        => await GetAsync<List<DatabaseClusterDto>>(ApiRoutes.Databases.List, ct) ?? [];

    public Task<DatabaseClusterDto?> GetDatabaseAsync(string id, CancellationToken ct = default)
        => GetAsync<DatabaseClusterDto>(ApiRoutes.Databases.Get(id), ct);

    public async Task<IReadOnlyList<DatabaseUserDto>> GetDbUsersAsync(string id, CancellationToken ct = default)
        => await GetAsync<List<DatabaseUserDto>>(ApiRoutes.Databases.Users(id), ct) ?? [];

    public Task<DatabaseUserDto> CreateDbUserAsync(string id, string name, CancellationToken ct = default)
        => PostAsync<CreateDatabaseUserRequest, DatabaseUserDto>(ApiRoutes.Databases.Users(id), new CreateDatabaseUserRequest { Name = name }, ct);

    public Task DeleteDbUserAsync(string id, string name, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Databases.User(id, name), ct);

    public async Task<IReadOnlyList<DatabaseDbDto>> GetDbsAsync(string id, CancellationToken ct = default)
        => await GetAsync<List<DatabaseDbDto>>(ApiRoutes.Databases.Dbs(id), ct) ?? [];

    public Task<DatabaseDbDto> CreateDbAsync(string id, string name, CancellationToken ct = default)
        => PostAsync<CreateDatabaseDbRequest, DatabaseDbDto>(ApiRoutes.Databases.Dbs(id), new CreateDatabaseDbRequest { Name = name }, ct);

    public Task DeleteDbAsync(string id, string name, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Databases.Db(id, name), ct);

    // ---------- SSH access ----------

    public Task<SshCredentialStatusDto> GetSshStatusAsync(CancellationToken ct = default)
        => GetAsync<SshCredentialStatusDto>(ApiRoutes.Ssh.Status, ct)!;

    public Task<SshCredentialStatusDto> SaveSshKeyAsync(SaveSshKeyRequest request, CancellationToken ct = default)
        => PostAsync<SaveSshKeyRequest, SshCredentialStatusDto>(ApiRoutes.Ssh.Save, request, ct);

    public Task DeleteSshAsync(CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Ssh.Delete, ct);

    // ---------- inside-the-droplet management ----------

    public Task<SshTestResultDto> TestSshAsync(long id, CancellationToken ct = default)
        => PostEmptyAsync<SshTestResultDto>(ApiRoutes.Droplets.SshTest(id), ct);

    public async Task<IReadOnlyList<ServiceUnitDto>> GetServicesAsync(long id, CancellationToken ct = default)
        => await GetAsync<List<ServiceUnitDto>>(ApiRoutes.Droplets.Services(id), ct) ?? [];

    public Task<CommandResultDto> ServiceActionAsync(long id, string unit, string action, CancellationToken ct = default)
        => PostAsync<ServiceActionRequest, CommandResultDto>(ApiRoutes.Droplets.ServiceAction(id), new ServiceActionRequest { Unit = unit, Action = action }, ct);

    public async Task<IReadOnlyList<SiteDto>> GetSitesAsync(long id, CancellationToken ct = default)
        => await GetAsync<List<SiteDto>>(ApiRoutes.Droplets.Sites(id), ct) ?? [];

    public Task<CommandResultDto> CreateSiteAsync(long id, CreateSiteRequest req, CancellationToken ct = default)
        => PostAsync<CreateSiteRequest, CommandResultDto>(ApiRoutes.Droplets.Sites(id), req, ct);

    public Task<CommandResultDto> EnableHttpsAsync(long id, string domain, string email, CancellationToken ct = default)
        => PostAsync<EnableHttpsRequest, CommandResultDto>(ApiRoutes.Droplets.SiteHttps(id), new EnableHttpsRequest { Domain = domain, Email = email }, ct);

    public Task<CommandResultDto> ToggleSiteAsync(long id, string configPath, bool enabled, CancellationToken ct = default)
        => PostAsync<SiteToggleRequest, CommandResultDto>(ApiRoutes.Droplets.SiteToggle(id), new SiteToggleRequest { ConfigPath = configPath, Enabled = enabled }, ct);

    public Task<CommandResultDto> ReloadNginxAsync(long id, CancellationToken ct = default)
        => PostEmptyAsync<CommandResultDto>(ApiRoutes.Droplets.NginxReload(id), ct);

    public Task<FirewallStatusDto> GetFirewallAsync(long id, CancellationToken ct = default)
        => GetAsync<FirewallStatusDto>(ApiRoutes.Droplets.Firewall(id), ct)!;

    public Task<CommandResultDto> AddFirewallRuleAsync(long id, AddFirewallRuleRequest req, CancellationToken ct = default)
        => PostAsync<AddFirewallRuleRequest, CommandResultDto>(ApiRoutes.Droplets.Firewall(id), req, ct);

    public Task DeleteFirewallRuleAsync(long id, int number, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Droplets.FirewallRule(id, number), ct);

    public Task<CommandResultDto> ToggleFirewallAsync(long id, bool enabled, CancellationToken ct = default)
        => PostAsync<FirewallToggleRequest, CommandResultDto>(ApiRoutes.Droplets.FirewallToggle(id), new FirewallToggleRequest { Enabled = enabled }, ct);

    public Task<DbServerDto> GetDbServerAsync(long id, CancellationToken ct = default)
        => GetAsync<DbServerDto>(ApiRoutes.Droplets.Db(id), ct)!;

    public Task<CommandResultDto> CreateServerDatabaseAsync(long id, string name, CancellationToken ct = default)
        => PostAsync<DbNameRequest, CommandResultDto>(ApiRoutes.Droplets.DbDatabases(id), new DbNameRequest { Name = name }, ct);

    public Task DropServerDatabaseAsync(long id, string name, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Droplets.DbDatabase(id, name), ct);

    public Task<CreatedDbUserDto> CreateServerDbUserAsync(long id, string name, CancellationToken ct = default)
        => PostAsync<DbNameRequest, CreatedDbUserDto>(ApiRoutes.Droplets.DbUsers(id), new DbNameRequest { Name = name }, ct);

    public Task DropServerDbUserAsync(long id, string name, CancellationToken ct = default)
        => DeleteAsync(ApiRoutes.Droplets.DbUser(id, name), ct);

    public Task<LogOutputDto> GetLogAsync(long id, string source, string? unit, int lines, CancellationToken ct = default)
    {
        var url = $"{ApiRoutes.Droplets.Logs(id)}?source={Uri.EscapeDataString(source)}&lines={lines}";
        if (!string.IsNullOrWhiteSpace(unit)) url += $"&unit={Uri.EscapeDataString(unit)}";
        return GetAsync<LogOutputDto>(url, ct)!;
    }

    // ---------- plumbing ----------

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return default;
        await EnsureSuccessAsync(res, ct);
        return await res.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private async Task<TOut> PostAsync<TIn, TOut>(string url, TIn body, CancellationToken ct)
    {
        using var res = await http.PostAsJsonAsync(url, body, Json, ct);
        await EnsureSuccessAsync(res, ct);
        return (await res.Content.ReadFromJsonAsync<TOut>(Json, ct))
               ?? throw new OrbitApiException("The server returned an empty response.", (int)res.StatusCode);
    }

    private async Task<TOut> PutAsync<TIn, TOut>(string url, TIn body, CancellationToken ct)
    {
        using var res = await http.PutAsJsonAsync(url, body, Json, ct);
        await EnsureSuccessAsync(res, ct);
        return (await res.Content.ReadFromJsonAsync<TOut>(Json, ct))
               ?? throw new OrbitApiException("The server returned an empty response.", (int)res.StatusCode);
    }

    private async Task DeleteAsync(string url, CancellationToken ct)
    {
        using var res = await http.DeleteAsync(url, ct);
        await EnsureSuccessAsync(res, ct);
    }

    private async Task<TOut> PostEmptyAsync<TOut>(string url, CancellationToken ct)
    {
        using var res = await http.PostAsync(url, content: null, ct);
        await EnsureSuccessAsync(res, ct);
        return (await res.Content.ReadFromJsonAsync<TOut>(Json, ct))
               ?? throw new OrbitApiException("The server returned an empty response.", (int)res.StatusCode);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;

        string message = $"Request failed ({(int)res.StatusCode}).";
        try
        {
            var problem = await res.Content.ReadFromJsonAsync<ProblemResponse>(Json, ct);
            if (!string.IsNullOrWhiteSpace(problem?.Detail)) message = problem!.Detail!;
            else if (!string.IsNullOrWhiteSpace(problem?.Title)) message = problem!.Title!;
        }
        catch
        {
            // ignore non-JSON bodies
        }

        throw new OrbitApiException(message, (int)res.StatusCode);
    }

    private sealed record ProblemResponse(string? Title, string? Detail, int? Status);
}
