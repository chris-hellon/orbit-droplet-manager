using Orbit.Core.Providers;
using Orbit.Core.Services;
using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Endpoints;

public static class OrbitApiEndpoints
{
    public static IEndpointRouteBuilder MapOrbitApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(ApiRoutes.Base).DisableAntiforgery();

        MapConnection(api);
        MapDroplets(api);
        MapRegions(api);
        MapDomains(api);
        MapDatabases(api);
        MapSsh(api);
        MapManagement(api);

        return app;
    }

    private static void MapConnection(IEndpointRouteBuilder api)
    {
        api.MapGet("/connection", (IConnectionService connections, CancellationToken ct)
            => connections.GetStatusAsync(ct));

        api.MapPost("/connection/validate", (ConnectRequest req, IConnectionService connections, CancellationToken ct)
            => Guard(() =>
            {
                ValidateToken(req.Token);
                return connections.ValidateAsync(req.Token, req.Provider, ct);
            }));

        api.MapPost("/connection", (ConnectRequest req, IConnectionService connections, CancellationToken ct)
            => Guard(() =>
            {
                ValidateToken(req.Token);
                return connections.ConnectAsync(req, ct);
            }));

        api.MapDelete("/connection", async (IConnectionService connections, CancellationToken ct) =>
        {
            await connections.DisconnectAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapDroplets(IEndpointRouteBuilder api)
    {
        api.MapGet("/droplets", (IDropletService droplets, CancellationToken ct)
            => Guard(() => droplets.GetDropletsAsync(ct)));

        api.MapGet("/droplets/{id:long}", (long id, IDropletService droplets, CancellationToken ct)
            => Guard(async () =>
            {
                var d = await droplets.GetDropletAsync(id, ct);
                return d is null ? Results.NotFound() : Results.Ok(d);
            }));

        api.MapGet("/droplets/{id:long}/metrics", (long id, IDropletService droplets, CancellationToken ct)
            => Guard(() => droplets.RefreshMetricsAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/power",
            (long id, PowerActionRequest req, IDropletService droplets, IMetricsNotifier notifier, CancellationToken ct)
            => Guard(async () =>
            {
                var action = await droplets.PowerAsync(id, req.Action, ct);
                await notifier.DropletsRefreshedAsync(ct);
                return action;
            }));

        api.MapPost("/droplets/{id:long}/snapshot",
            (long id, SnapshotRequest req, IDropletService droplets, IMetricsNotifier notifier, CancellationToken ct)
            => Guard(async () =>
            {
                if (string.IsNullOrWhiteSpace(req.Name))
                    throw new ProviderException("A snapshot name is required.", 400);
                var action = await droplets.SnapshotAsync(id, req.Name.Trim(), ct);
                await notifier.DropletsRefreshedAsync(ct);
                return action;
            }));

        api.MapPost("/droplets/{id:long}/resize",
            (long id, ResizeRequest req, IDropletService droplets, IMetricsNotifier notifier, CancellationToken ct)
            => Guard(async () =>
            {
                if (string.IsNullOrWhiteSpace(req.Size))
                    throw new ProviderException("A target size is required.", 400);
                var action = await droplets.ResizeAsync(id, req.Size.Trim(), req.ResizeDisk, ct);
                await notifier.DropletsRefreshedAsync(ct);
                return action;
            }));

        api.MapPost("/droplets/{id:long}/rebuild",
            (long id, RebuildRequest req, IDropletService droplets, IMetricsNotifier notifier, CancellationToken ct)
            => Guard(async () =>
            {
                if (string.IsNullOrWhiteSpace(req.Image))
                    throw new ProviderException("An image is required.", 400);
                var action = await droplets.RebuildAsync(id, req.Image.Trim(), ct);
                await notifier.DropletsRefreshedAsync(ct);
                return action;
            }));
    }

    private static void MapRegions(IEndpointRouteBuilder api)
    {
        api.MapGet("/regions", (IDropletService droplets, CancellationToken ct)
            => Guard(() => droplets.GetRegionsAsync(ct)));

        api.MapGet("/sizes", (IDropletService droplets, CancellationToken ct)
            => Guard(() => droplets.GetSizesAsync(ct)));

        api.MapGet("/images", (IDropletService droplets, CancellationToken ct)
            => Guard(() => droplets.GetImagesAsync(ct)));

        api.MapGet("/activity", (int? take, IActivityRecorder activity, CancellationToken ct)
            => activity.GetRecentAsync(take ?? 25, ct));
    }

    private static void MapDomains(IEndpointRouteBuilder api)
    {
        api.MapGet("/domains", (IDomainService svc, CancellationToken ct)
            => Guard(() => svc.ListAsync(ct)));

        api.MapPost("/domains", (CreateDomainRequest req, IDomainService svc, CancellationToken ct)
            => Guard(() =>
            {
                if (string.IsNullOrWhiteSpace(req.Name))
                    throw new ProviderException("A domain name is required.", 400);
                return svc.CreateAsync(req, ct);
            }));

        api.MapDelete("/domains/{name}", (string name, IDomainService svc, CancellationToken ct)
            => Guard(async () => { await svc.DeleteAsync(name, ct); return Results.NoContent(); }));

        api.MapGet("/domains/{name}/records", (string name, IDomainService svc, CancellationToken ct)
            => Guard(() => svc.ListRecordsAsync(name, ct)));

        api.MapPost("/domains/{name}/records", (string name, DnsRecordRequest req, IDomainService svc, CancellationToken ct)
            => Guard(() =>
            {
                ValidateRecord(req);
                return svc.CreateRecordAsync(name, req, ct);
            }));

        api.MapPut("/domains/{name}/records/{id:long}", (string name, long id, DnsRecordRequest req, IDomainService svc, CancellationToken ct)
            => Guard(() =>
            {
                ValidateRecord(req);
                return svc.UpdateRecordAsync(name, id, req, ct);
            }));

        api.MapDelete("/domains/{name}/records/{id:long}", (string name, long id, IDomainService svc, CancellationToken ct)
            => Guard(async () => { await svc.DeleteRecordAsync(name, id, ct); return Results.NoContent(); }));
    }

    private static void MapDatabases(IEndpointRouteBuilder api)
    {
        api.MapGet("/databases", (IDatabaseService svc, CancellationToken ct)
            => Guard(() => svc.ListAsync(ct)));

        api.MapGet("/databases/{id}", (string id, IDatabaseService svc, CancellationToken ct)
            => Guard(async () =>
            {
                var c = await svc.GetAsync(id, ct);
                return c is null ? Results.NotFound() : Results.Ok(c);
            }));

        api.MapGet("/databases/{id}/users", (string id, IDatabaseService svc, CancellationToken ct)
            => Guard(() => svc.ListUsersAsync(id, ct)));

        api.MapPost("/databases/{id}/users", (string id, CreateDatabaseUserRequest req, IDatabaseService svc, CancellationToken ct)
            => Guard(() =>
            {
                if (string.IsNullOrWhiteSpace(req.Name)) throw new ProviderException("A user name is required.", 400);
                return svc.CreateUserAsync(id, req.Name, ct);
            }));

        api.MapDelete("/databases/{id}/users/{name}", (string id, string name, IDatabaseService svc, CancellationToken ct)
            => Guard(async () => { await svc.DeleteUserAsync(id, name, ct); return Results.NoContent(); }));

        api.MapGet("/databases/{id}/dbs", (string id, IDatabaseService svc, CancellationToken ct)
            => Guard(() => svc.ListDatabasesAsync(id, ct)));

        api.MapPost("/databases/{id}/dbs", (string id, CreateDatabaseDbRequest req, IDatabaseService svc, CancellationToken ct)
            => Guard(() =>
            {
                if (string.IsNullOrWhiteSpace(req.Name)) throw new ProviderException("A database name is required.", 400);
                return svc.CreateDatabaseAsync(id, req.Name, ct);
            }));

        api.MapDelete("/databases/{id}/dbs/{name}", (string id, string name, IDatabaseService svc, CancellationToken ct)
            => Guard(async () => { await svc.DeleteDatabaseAsync(id, name, ct); return Results.NoContent(); }));
    }

    private static void MapSsh(IEndpointRouteBuilder api)
    {
        api.MapGet("/ssh", (ISshCredentialService ssh, CancellationToken ct)
            => ssh.GetStatusAsync(ct));

        api.MapPost("/ssh", (SaveSshKeyRequest req, ISshCredentialService ssh, CancellationToken ct)
            => Guard(async () =>
            {
                if (string.IsNullOrWhiteSpace(req.PrivateKey))
                    throw new ProviderException("An SSH private key is required.", 400);
                await ssh.SaveAsync(req, ct);
                return await ssh.GetStatusAsync(ct);
            }));

        api.MapDelete("/ssh", async (ISshCredentialService ssh, CancellationToken ct) =>
        {
            await ssh.DeleteAsync(ct);
            return Results.NoContent();
        });
    }

    private static void MapManagement(IEndpointRouteBuilder api)
    {
        api.MapPost("/droplets/{id:long}/ssh-test", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.TestAsync(id, ct)));

        api.MapGet("/droplets/{id:long}/services", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ListServicesAsync(id, ct)));

        api.MapGet("/droplets/{id:long}/monitoring", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.GetMonitoringStatusAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/monitoring/install", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.InstallMonitoringAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/service-action", (long id, ServiceActionRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ServiceActionAsync(id, req.Unit, req.Action, ct)));

        api.MapGet("/droplets/{id:long}/logs", (long id, string source, string? unit, int? lines, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.TailLogAsync(id, source, unit, lines ?? 200, ct)));

        api.MapGet("/droplets/{id:long}/sites", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ListSitesAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/sites", (long id, CreateSiteRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.CreateSiteAsync(id, req, ct)));

        api.MapPost("/droplets/{id:long}/sites/https", (long id, EnableHttpsRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.EnableHttpsAsync(id, req.Domain, req.Email, ct)));

        api.MapPost("/droplets/{id:long}/sites/toggle", (long id, SiteToggleRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ToggleSiteAsync(id, req.ConfigPath, req.Enabled, ct)));

        api.MapPost("/droplets/{id:long}/nginx-reload", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ReloadNginxAsync(id, ct)));

        api.MapGet("/droplets/{id:long}/firewall", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.GetFirewallAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/firewall", (long id, AddFirewallRuleRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.AddFirewallRuleAsync(id, req, ct)));

        api.MapDelete("/droplets/{id:long}/firewall/{number:int}", (long id, int number, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.DeleteFirewallRuleAsync(id, number, ct)));

        api.MapPost("/droplets/{id:long}/firewall/toggle", (long id, FirewallToggleRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ToggleFirewallAsync(id, req.Enabled, ct)));

        // --- on-box database ---
        api.MapGet("/droplets/{id:long}/db", (long id, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.GetDbServerAsync(id, ct)));

        api.MapPost("/droplets/{id:long}/db/databases", (long id, DbNameRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.CreateServerDatabaseAsync(id, req.Name, ct)));

        api.MapDelete("/droplets/{id:long}/db/databases/{name}", (long id, string name, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.DropServerDatabaseAsync(id, name, ct)));

        api.MapPost("/droplets/{id:long}/db/users", (long id, DbNameRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.CreateServerDbUserAsync(id, req.Name, ct)));

        api.MapDelete("/droplets/{id:long}/db/users/{name}", (long id, string name, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.DropServerDbUserAsync(id, name, ct)));

        // --- file manager (SFTP) ---
        api.MapGet("/droplets/{id:long}/files", (long id, string? path, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.ListFilesAsync(id, path, ct)));

        api.MapPost("/droplets/{id:long}/files/upload", (long id, string path, string name, HttpRequest req, IDropletManagementService mgmt, CancellationToken ct) =>
        {
            // Let an upload exceed Kestrel's default body-size cap for this request only.
            var feature = req.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = null;
            return Guard(async () =>
            {
                await mgmt.UploadFileAsync(id, path, name, req.Body, ct);
                return await mgmt.ListFilesAsync(id, path, ct);
            });
        });

        api.MapGet("/droplets/{id:long}/files/download", (long id, string path, IDropletManagementService mgmt, CancellationToken ct) =>
        {
            var fileName = path.TrimEnd('/');
            var slash = fileName.LastIndexOf('/');
            fileName = slash >= 0 && slash < fileName.Length - 1 ? fileName[(slash + 1)..] : "download";
            return Results.Stream(output => mgmt.DownloadFileAsync(id, path, output, ct), "application/octet-stream", fileName);
        });

        api.MapPost("/droplets/{id:long}/files/mkdir", (long id, MakeDirRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.MakeDirectoryAsync(id, req.Path, req.Name, ct)));

        api.MapPost("/droplets/{id:long}/files/rename", (long id, RenameFileRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.RenameFileAsync(id, req.Path, req.NewName, ct)));

        api.MapPost("/droplets/{id:long}/files/delete", (long id, DeleteFileRequest req, IDropletManagementService mgmt, CancellationToken ct)
            => Guard(() => mgmt.DeleteFileAsync(id, req.Path, ct)));
    }

    private static void ValidateRecord(DnsRecordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Type)) throw new ProviderException("A record type is required.", 400);
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ProviderException("A record name is required.", 400);
        if (string.IsNullOrWhiteSpace(req.Data)) throw new ProviderException("A record value is required.", 400);
    }

    // ---------- helpers ----------

    private static void ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Trim().Length < 20)
            throw new ProviderException("That token looks too short to be a valid DigitalOcean personal access token.", 400);
    }

    /// <summary>Runs an endpoint body, translating domain exceptions into ProblemDetails responses.</summary>
    private static async Task<IResult> Guard<T>(Func<Task<T>> action)
    {
        try
        {
            var value = await action();
            return value is IResult result ? result : Results.Ok(value);
        }
        catch (NotConnectedException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Not connected");
        }
        catch (NoSshCredentialException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict, title: "No SSH key");
        }
        catch (SshAccessException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway, title: "SSH error");
        }
        catch (ProviderException ex)
        {
            var status = ex.StatusCode switch
            {
                400 => StatusCodes.Status400BadRequest,
                401 => StatusCodes.Status401Unauthorized,
                403 => StatusCodes.Status403Forbidden,
                404 => StatusCodes.Status404NotFound,
                429 => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status502BadGateway,
            };
            return Results.Problem(ex.Message, statusCode: status, title: "Provider error");
        }
    }
}
