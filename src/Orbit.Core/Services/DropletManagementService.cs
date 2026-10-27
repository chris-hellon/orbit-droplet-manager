using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Orbit.Core.Providers;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

/// <summary>
/// "Inside the droplet" operations driven over SSH: list/control systemd services, tail logs.
/// Resolves the droplet's public IP from the provider server-side (never trusts a client host).
/// </summary>
public interface IDropletManagementService
{
    Task<IReadOnlyList<ServiceUnitDto>> ListServicesAsync(long dropletId, CancellationToken ct = default);
    Task<CommandResultDto> ServiceActionAsync(long dropletId, string unit, string action, CancellationToken ct = default);
    Task<LogOutputDto> TailLogAsync(long dropletId, string source, string? unit, int lines, CancellationToken ct = default);
    Task<IReadOnlyList<SiteDto>> ListSitesAsync(long dropletId, CancellationToken ct = default);
    Task<CommandResultDto> CreateSiteAsync(long dropletId, CreateSiteRequest request, CancellationToken ct = default);
    Task<CommandResultDto> EnableHttpsAsync(long dropletId, string domain, string email, CancellationToken ct = default);
    Task<CommandResultDto> ToggleSiteAsync(long dropletId, string configPath, bool enabled, CancellationToken ct = default);
    Task<CommandResultDto> ReloadNginxAsync(long dropletId, CancellationToken ct = default);

    Task<DbServerDto> GetDbServerAsync(long dropletId, CancellationToken ct = default);
    Task<CommandResultDto> CreateServerDatabaseAsync(long dropletId, string name, CancellationToken ct = default);
    Task<CommandResultDto> DropServerDatabaseAsync(long dropletId, string name, CancellationToken ct = default);
    Task<CreatedDbUserDto> CreateServerDbUserAsync(long dropletId, string name, CancellationToken ct = default);
    Task<CommandResultDto> DropServerDbUserAsync(long dropletId, string name, CancellationToken ct = default);

    Task<FirewallStatusDto> GetFirewallAsync(long dropletId, CancellationToken ct = default);
    Task<CommandResultDto> AddFirewallRuleAsync(long dropletId, AddFirewallRuleRequest request, CancellationToken ct = default);
    Task<CommandResultDto> DeleteFirewallRuleAsync(long dropletId, int number, CancellationToken ct = default);
    Task<CommandResultDto> ToggleFirewallAsync(long dropletId, bool enabled, CancellationToken ct = default);

    Task<SshTestResultDto> TestAsync(long dropletId, CancellationToken ct = default);
    Task<string> ResolveHostAsync(long dropletId, CancellationToken ct = default);

    // --- files (SFTP) ---
    Task<DirectoryListingDto> ListFilesAsync(long dropletId, string? path, CancellationToken ct = default);
    Task UploadFileAsync(long dropletId, string directory, string fileName, Stream content, CancellationToken ct = default);
    Task DownloadFileAsync(long dropletId, string path, Stream destination, CancellationToken ct = default);
    Task<DirectoryListingDto> MakeDirectoryAsync(long dropletId, string directory, string name, CancellationToken ct = default);
    Task<DirectoryListingDto> DeleteFileAsync(long dropletId, string path, CancellationToken ct = default);
    Task<DirectoryListingDto> RenameFileAsync(long dropletId, string path, string newName, CancellationToken ct = default);
}

public sealed partial class DropletManagementService(IDropletService droplets, ISshService ssh, ISftpService sftp, DropletStateCache cache, IActivityRecorder activity) : IDropletManagementService
{
    [GeneratedRegex(@"^[a-zA-Z0-9_.@:\-\\]+$")]
    private static partial Regex UnitNameRegex();

    [GeneratedRegex(@"^/[A-Za-z0-9._/\-]+$")]
    private static partial Regex SafePathRegex();

    [GeneratedRegex(@"^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)(\.[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)+$")]
    private static partial Regex HostnameRegex();

    [GeneratedRegex(@"^[A-Za-z0-9]{1,40}$")]
    private static partial Regex PortOrServiceRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,63}$")]
    private static partial Regex DbIdentifierRegex();

    private static readonly string[] AllowedActions = ["start", "stop", "restart"];

    public async Task<string> ResolveHostAsync(long dropletId, CancellationToken ct = default)
    {
        // Prefer the cached IP (populated whenever droplets are listed) to avoid a provider round trip.
        var cached = cache.TryGetIp(dropletId);
        if (!string.IsNullOrWhiteSpace(cached)) return cached!;

        var droplet = await droplets.GetDropletAsync(dropletId, ct)
            ?? throw new ProviderException("Droplet not found.", 404);
        if (string.IsNullOrWhiteSpace(droplet.PublicIpv4))
            throw new ProviderException("This droplet has no public IPv4 address to connect to.", 400);
        return droplet.PublicIpv4!;
    }

    public async Task<SshTestResultDto> TestAsync(long dropletId, CancellationToken ct = default)
        => await ssh.TestAsync(await ResolveHostAsync(dropletId, ct), ct);

    private const string CustomMarker = "::ORBIT-CUSTOM::";

    private static readonly HashSet<string> AppServiceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "nginx", "apache2", "httpd", "caddy", "haproxy", "traefik",
        "mysql", "mysqld", "mariadb", "postgresql", "postgres", "redis", "redis-server",
        "mongod", "mongodb", "memcached", "rabbitmq-server", "elasticsearch",
        "docker", "containerd", "gunicorn", "uwsgi", "supervisor", "pm2", "node",
    };

    public async Task<IReadOnlyList<ServiceUnitDto>> ListServicesAsync(long dropletId, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);

        // One round trip: the unit list, then the names of admin-authored units in /etc/systemd/system.
        var result = await ssh.RunAsync(host,
            "systemctl list-units --type=service --all --no-legend --no-pager --plain 2>/dev/null; " +
            $"echo '{CustomMarker}'; " +
            "find /etc/systemd/system -maxdepth 1 -name '*.service' -type f -printf '%f\\n' 2>/dev/null", ct);

        var output = result.Output;
        var markerIndex = output.IndexOf(CustomMarker, StringComparison.Ordinal);
        var unitsSection = markerIndex >= 0 ? output[..markerIndex] : output;
        var customSection = markerIndex >= 0 ? output[(markerIndex + CustomMarker.Length)..] : "";

        var custom = customSection
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var list = new List<ServiceUnitDto>();
        foreach (var raw in unitsSection.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            // columns: UNIT LOAD ACTIVE SUB DESCRIPTION...
            var parts = line.Split((char[]?)null, 5, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) continue;
            if (!parts[0].EndsWith(".service", StringComparison.OrdinalIgnoreCase)) continue;

            list.Add(new ServiceUnitDto
            {
                Unit = parts[0],
                Load = parts[1],
                Active = parts[2],
                Sub = parts[3],
                Description = parts.Length >= 5 ? parts[4] : "",
                App = IsApp(parts[0], custom),
            });
        }

        return list
            .OrderByDescending(s => s.App)
            .ThenByDescending(s => s.IsRunning)
            .ThenBy(s => s.Unit, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsApp(string unit, HashSet<string> custom)
    {
        if (custom.Contains(unit)) return true;

        var baseName = unit.EndsWith(".service", StringComparison.OrdinalIgnoreCase)
            ? unit[..^".service".Length]
            : unit;
        var at = baseName.IndexOf('@');
        if (at >= 0) baseName = baseName[..at];

        if (AppServiceNames.Contains(baseName)) return true;
        if (baseName.StartsWith("php", StringComparison.OrdinalIgnoreCase) && baseName.EndsWith("-fpm", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }

    public async Task<CommandResultDto> ServiceActionAsync(long dropletId, string unit, string action, CancellationToken ct = default)
    {
        action = action.Trim().ToLowerInvariant();
        if (!AllowedActions.Contains(action))
            throw new ProviderException($"Unsupported service action '{action}'.", 400);
        if (!IsValidUnit(unit))
            throw new ProviderException("Invalid service unit name.", 400);

        var host = await ResolveHostAsync(dropletId, ct);
        var result = await ssh.RunAsync(host, $"systemctl {action} {unit} 2>&1 && systemctl is-active {unit}", ct);
        if (result.ExitStatus == 0)
            await activity.RecordAsync("service", $"{char.ToUpperInvariant(action[0])}{action[1..]} {unit}", dropletId, "info", ct);
        return new CommandResultDto { ExitStatus = result.ExitStatus, Output = result.Output, Error = result.Error };
    }

    public async Task<LogOutputDto> TailLogAsync(long dropletId, string source, string? unit, int lines, CancellationToken ct = default)
    {
        lines = Math.Clamp(lines, 10, 1000);
        var host = await ResolveHostAsync(dropletId, ct);

        var command = source switch
        {
            "nginx-access" => $"tail -n {lines} /var/log/nginx/access.log 2>&1",
            "nginx-error" => $"tail -n {lines} /var/log/nginx/error.log 2>&1",
            "syslog" => $"tail -n {lines} /var/log/syslog 2>/dev/null || journalctl -n {lines} --no-pager 2>&1",
            "journal" when IsValidUnit(unit) => $"journalctl -u {unit} -n {lines} --no-pager 2>&1",
            _ => throw new ProviderException("Unknown log source.", 400),
        };

        var result = await ssh.RunAsync(host, command, ct);
        var content = string.IsNullOrEmpty(result.Output) ? result.Error : result.Output;
        return new LogOutputDto { Source = source, Content = content };
    }

    private static bool IsValidUnit(string? unit)
        => !string.IsNullOrWhiteSpace(unit) && unit.Length <= 128 && UnitNameRegex().IsMatch(unit);

    public async Task<IReadOnlyList<SiteDto>> ListSitesAsync(long dropletId, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);

        // nginx -T dumps the full resolved config; needs root (DO droplets default to root).
        var result = await ssh.RunAsync(host, "command -v nginx >/dev/null 2>&1 && nginx -T 2>/dev/null || true", ct);
        if (string.IsNullOrWhiteSpace(result.Output)) return [];

        var parsed = NginxConfig.Parse(result.Output);
        if (parsed.Count == 0) return [];

        var certPaths = parsed
            .Select(p => p.SslCertPath)
            .Where(p => p is not null && p.Length <= 256 && SafePathRegex().IsMatch(p))
            .Select(p => p!)
            .Distinct()
            .ToList();

        var expiries = certPaths.Count > 0 ? await ReadCertExpiriesAsync(host, certPaths, ct) : [];

        return parsed.Select(p => new SiteDto
        {
            Server = "nginx",
            PrimaryDomain = p.PrimaryDomain,
            Domains = p.Domains,
            Root = p.Root,
            ProxyPass = p.ProxyPass,
            Listen = p.Listen,
            Ssl = p.Ssl,
            SslExpiresAt = p.SslCertPath is not null && expiries.TryGetValue(p.SslCertPath, out var e) ? e : null,
            ConfigPath = p.ConfigPath,
        }).ToList();
    }

    private async Task<Dictionary<string, DateTimeOffset>> ReadCertExpiriesAsync(string host, List<string> paths, CancellationToken ct)
    {
        // Paths are pre-validated to a safe charset, so they're safe to list unquoted.
        // Paths are pre-validated to a safe charset (no spaces), so $f is safe unquoted — and
        // quoting it here would pass literal quote characters to openssl as part of the filename.
        var list = string.Join(' ', paths);
        var command = $"for f in {list}; do echo \"$f|$(openssl x509 -enddate -noout -in $f 2>/dev/null | cut -d= -f2)\"; done";
        var result = await ssh.RunAsync(host, command, ct);

        var map = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var bar = line.IndexOf('|');
            if (bar <= 0) continue;
            var path = line[..bar].Trim();
            var dateStr = line[(bar + 1)..].Trim();
            if (ParseOpensslDate(dateStr) is { } expiry)
                map[path] = expiry;
        }
        return map;
    }

    // ---------- actions: sites ----------

    public async Task<CommandResultDto> CreateSiteAsync(long dropletId, CreateSiteRequest request, CancellationToken ct = default)
    {
        var domain = (request.Domain ?? "").Trim().ToLowerInvariant();
        if (!HostnameRegex().IsMatch(domain))
            throw new ProviderException("Enter a valid domain name, e.g. app.example.com.", 400);

        var aliases = (request.Aliases ?? "")
            .Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.ToLowerInvariant())
            .ToList();
        if (aliases.Any(a => !HostnameRegex().IsMatch(a)))
            throw new ProviderException("One of the extra domains isn't a valid hostname.", 400);

        var allDomains = new[] { domain }.Concat(aliases).Distinct().ToArray();
        var serverNames = string.Join(' ', allDomains);

        string config;
        var prelude = "";
        if (request.Mode == "static")
        {
            // Default the doc root to /var/www/<domain> so "create a website" needs nothing else.
            var root = (request.Root ?? "").Trim();
            if (root.Length == 0) root = $"/var/www/{domain}";
            if (!SafePathRegex().IsMatch(root))
                throw new ProviderException("Enter a valid document root path, e.g. /var/www/example.", 400);
            config = BuildStaticSite(serverNames, root);

            // Provision the folder up front and seed a placeholder page, so the site serves 200
            // immediately (no 403/404) and there's somewhere to upload the real files into.
            var placeholder = BuildPlaceholderHtml(domain);
            prelude =
                $"mkdir -p {root}\n" +
                $"if [ -z \"$(ls -A {root} 2>/dev/null)\" ]; then cat > {root}/index.html <<'ORBIT_HTML_EOF'\n{placeholder}\nORBIT_HTML_EOF\nfi\n";
        }
        else
        {
            var port = request.UpstreamPort ?? 0;
            if (port is < 1 or > 65535)
                throw new ProviderException("Enter the local port your app listens on (1–65535).", 400);
            config = BuildProxySite(serverNames, port);
        }

        var host = await ResolveHostAsync(dropletId, ct);
        var available = $"/etc/nginx/sites-available/{domain}";
        var enabled = $"/etc/nginx/sites-enabled/{domain}";

        // Provision (static only), write the vhost via a quoted heredoc (no shell expansion of $host
        // etc.), symlink, test, then reload — and if the test fails, remove the symlink so the next
        // reload isn't broken.
        var command =
            prelude +
            $"cat > {available} <<'ORBIT_NGINX_EOF'\n{config}\nORBIT_NGINX_EOF\n" +
            $"ln -sf {available} {enabled}\n" +
            $"if nginx -t 2>&1; then systemctl reload nginx && echo 'SITE_CREATED'; else rm -f {enabled}; echo 'REVERTED: nginx config test failed'; exit 1; fi";

        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus != 0)
            return ToCommandResult(result);

        await activity.RecordAsync("site", $"Added website {domain}", dropletId, "success", ct);

        // Smart check: for a reverse-proxy site, see whether anything is actually listening on the
        // upstream port — a dead upstream is the usual cause of a 502 right after creating a site.
        var output = $"Website {domain} created.";
        if (request.Mode != "static" && request.UpstreamPort is { } upstream)
        {
            var probe = await ssh.RunAsync(host,
                $"curl -s -o /dev/null -w '%{{http_code}}' --max-time 4 http://127.0.0.1:{upstream} || true", ct);
            if (probe.Output.Trim() is "" or "000")
                output += $"\n\n⚠ nginx is live, but nothing is responding on 127.0.0.1:{upstream} yet — " +
                          $"you'll get a 502 until your app is running and listening on that port. " +
                          $"(If this site should just serve files, recreate it as a Static site instead.)";
            else
                output += $"\nUpstream on 127.0.0.1:{upstream} is responding.";
        }
        else if (request.Mode == "static")
        {
            output += " Serving static files — upload your content from the site's Files area.";
        }

        return new CommandResultDto { ExitStatus = 0, Output = output };
    }

    public async Task<CommandResultDto> EnableHttpsAsync(long dropletId, string domain, string email, CancellationToken ct = default)
    {
        domain = (domain ?? "").Trim().ToLowerInvariant();
        email = (email ?? "").Trim();
        if (!HostnameRegex().IsMatch(domain))
            throw new ProviderException("Enter a valid domain name.", 400);
        if (!email.Contains('@') || email.Any(c => char.IsWhiteSpace(c)))
            throw new ProviderException("Enter a valid email for Let's Encrypt notices.", 400);

        var host = await ResolveHostAsync(dropletId, ct);
        var command =
            "command -v certbot >/dev/null 2>&1 || { echo 'certbot is not installed on this droplet. Install it (e.g. apt install certbot python3-certbot-nginx) and try again.'; exit 1; }\n" +
            $"certbot --nginx -d {domain} --non-interactive --agree-tos -m {email} --redirect 2>&1";

        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus == 0)
            await activity.RecordAsync("site", $"Enabled HTTPS for {domain}", dropletId, "success", ct);
        return ToCommandResult(result);
    }

    public async Task<CommandResultDto> ToggleSiteAsync(long dropletId, string configPath, bool enabled, CancellationToken ct = default)
    {
        var name = Path.GetFileName((configPath ?? "").Trim());
        if (string.IsNullOrWhiteSpace(name) || !PortOrServiceRegexName(name))
            throw new ProviderException("Invalid site name.", 400);

        var host = await ResolveHostAsync(dropletId, ct);
        var available = $"/etc/nginx/sites-available/{name}";
        var enabledPath = $"/etc/nginx/sites-enabled/{name}";
        var command = enabled
            ? $"ln -sf {available} {enabledPath} && nginx -t 2>&1 && systemctl reload nginx && echo ENABLED"
            : $"rm -f {enabledPath} && nginx -t 2>&1 && systemctl reload nginx && echo DISABLED";

        return ToCommandResult(await ssh.RunAsync(host, command, ct));
    }

    public async Task<CommandResultDto> ReloadNginxAsync(long dropletId, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        return ToCommandResult(await ssh.RunAsync(host, "nginx -t 2>&1 && systemctl reload nginx && echo RELOADED", ct));
    }

    // ---------- on-box database ----------

    private static readonly HashSet<string> SystemDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "mysql", "sys", "information_schema", "performance_schema", "postgres", "template0", "template1",
    };

    private static readonly HashSet<string> SystemDbUsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "root", "postgres", "debian-sys-maint", "mysql.sys", "mysql.session", "mysql.infoschema",
    };

    public async Task<DbServerDto> GetDbServerAsync(long dropletId, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        const string script =
            "if command -v mysql >/dev/null 2>&1; then " +
            "  echo 'ENGINE|mysql'; " +
            "  mysql --version 2>/dev/null | head -1 | sed 's/^/VERSION|/'; " +
            "  (systemctl is-active mysql 2>/dev/null || systemctl is-active mariadb 2>/dev/null) | head -1 | sed 's/^/RUNNING|/'; " +
            "  echo 'DBS|'; mysql -N -B -e 'SHOW DATABASES;' 2>/dev/null; " +
            "  echo 'USERS|'; mysql -N -B -e 'SELECT DISTINCT User FROM mysql.user;' 2>/dev/null; " +
            "elif command -v psql >/dev/null 2>&1; then " +
            "  echo 'ENGINE|postgres'; " +
            "  psql --version 2>/dev/null | head -1 | sed 's/^/VERSION|/'; " +
            "  systemctl is-active postgresql 2>/dev/null | head -1 | sed 's/^/RUNNING|/'; " +
            "  echo 'DBS|'; sudo -u postgres psql -tAc 'SELECT datname FROM pg_database WHERE datistemplate=false ORDER BY datname;' 2>/dev/null; " +
            "  echo 'USERS|'; sudo -u postgres psql -tAc 'SELECT usename FROM pg_user ORDER BY usename;' 2>/dev/null; " +
            "else echo 'ENGINE|none'; fi";

        var result = await ssh.RunAsync(host, script, ct);

        var engine = "none";
        var version = "";
        var running = false;
        var dbs = new List<string>();
        var users = new List<string>();
        var section = "";

        foreach (var raw in result.Output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("ENGINE|", StringComparison.Ordinal)) { engine = line[7..]; section = ""; }
            else if (line.StartsWith("VERSION|", StringComparison.Ordinal)) version = line[8..];
            else if (line.StartsWith("RUNNING|", StringComparison.Ordinal)) running = line[8..].Equals("active", StringComparison.OrdinalIgnoreCase);
            else if (line == "DBS|") section = "dbs";
            else if (line == "USERS|") section = "users";
            else if (section == "dbs" && !SystemDatabases.Contains(line)) dbs.Add(line);
            else if (section == "users" && !line.StartsWith("mysql.", StringComparison.OrdinalIgnoreCase)) users.Add(line);
        }

        if (engine is not ("mysql" or "postgres"))
            return DbServerDto.None;

        return new DbServerDto
        {
            Engine = engine,
            Installed = true,
            Running = running,
            Version = version,
            Databases = dbs,
            Users = users.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    public async Task<CommandResultDto> CreateServerDatabaseAsync(long dropletId, string name, CancellationToken ct = default)
    {
        name = ValidateDbName(name);
        var host = await ResolveHostAsync(dropletId, ct);
        var command =
            "if command -v mysql >/dev/null 2>&1; then " +
            $"mysql -e 'CREATE DATABASE `{name}`;' 2>&1 && echo CREATED; " +
            "elif command -v psql >/dev/null 2>&1; then " +
            $"sudo -u postgres createdb {name} 2>&1 && echo CREATED; " +
            "else echo 'No supported database engine found'; exit 1; fi";
        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus == 0)
            await activity.RecordAsync("database", $"Created database {name}", dropletId, "success", ct);
        return ToCommandResult(result);
    }

    public async Task<CommandResultDto> DropServerDatabaseAsync(long dropletId, string name, CancellationToken ct = default)
    {
        name = ValidateDbName(name);
        if (SystemDatabases.Contains(name))
            throw new ProviderException($"'{name}' is a system database and can't be dropped.", 400);

        var host = await ResolveHostAsync(dropletId, ct);
        var command =
            "if command -v mysql >/dev/null 2>&1; then " +
            $"mysql -e 'DROP DATABASE `{name}`;' 2>&1 && echo DROPPED; " +
            "elif command -v psql >/dev/null 2>&1; then " +
            $"sudo -u postgres dropdb {name} 2>&1 && echo DROPPED; " +
            "else echo 'No supported database engine found'; exit 1; fi";
        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus == 0)
            await activity.RecordAsync("database", $"Dropped database {name}", dropletId, "warning", ct);
        return ToCommandResult(result);
    }

    public async Task<CreatedDbUserDto> CreateServerDbUserAsync(long dropletId, string name, CancellationToken ct = default)
    {
        name = ValidateDbName(name);
        var password = GeneratePassword();
        var host = await ResolveHostAsync(dropletId, ct);
        var command =
            "if command -v mysql >/dev/null 2>&1; then " +
            $"mysql -e \"CREATE USER '{name}'@'localhost' IDENTIFIED BY '{password}'; GRANT ALL PRIVILEGES ON *.* TO '{name}'@'localhost'; FLUSH PRIVILEGES;\" 2>&1 && echo CREATED; " +
            "elif command -v psql >/dev/null 2>&1; then " +
            $"sudo -u postgres psql -c \"CREATE ROLE {name} LOGIN PASSWORD '{password}';\" 2>&1 && echo CREATED; " +
            "else echo 'No supported database engine found'; exit 1; fi";

        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus != 0 || !result.Output.Contains("CREATED", StringComparison.Ordinal))
            throw new ProviderException(string.IsNullOrWhiteSpace(result.Output) ? result.Error : result.Output.Trim(), 400);

        await activity.RecordAsync("database", $"Created database user {name}", dropletId, "success", ct);
        return new CreatedDbUserDto { Name = name, Password = password };
    }

    public async Task<CommandResultDto> DropServerDbUserAsync(long dropletId, string name, CancellationToken ct = default)
    {
        name = ValidateDbName(name);
        if (SystemDbUsers.Contains(name))
            throw new ProviderException($"'{name}' is a system account and can't be dropped.", 400);

        var host = await ResolveHostAsync(dropletId, ct);
        var command =
            "if command -v mysql >/dev/null 2>&1; then " +
            $"mysql -e \"DROP USER IF EXISTS '{name}'@'localhost'; DROP USER IF EXISTS '{name}'@'%';\" 2>&1 && echo DROPPED; " +
            "elif command -v psql >/dev/null 2>&1; then " +
            $"sudo -u postgres psql -c 'DROP ROLE IF EXISTS {name};' 2>&1 && echo DROPPED; " +
            "else echo 'No supported database engine found'; exit 1; fi";
        return ToCommandResult(await ssh.RunAsync(host, command, ct));
    }

    private static string ValidateDbName(string name)
    {
        name = (name ?? "").Trim();
        if (!DbIdentifierRegex().IsMatch(name))
            throw new ProviderException("Names may only contain letters, numbers and underscores (max 63).", 400);
        return name;
    }

    private static string GeneratePassword(int length = 24)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes) sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    // ---------- actions: firewall (ufw) ----------

    public async Task<FirewallStatusDto> GetFirewallAsync(long dropletId, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        var result = await ssh.RunAsync(host,
            "command -v ufw >/dev/null 2>&1 || { echo 'NOTINSTALLED'; exit 0; }; ufw status numbered 2>&1", ct);

        if (result.Output.Contains("NOTINSTALLED"))
            return FirewallStatusDto.NotInstalled;

        var active = result.Output.Contains("Status: active", StringComparison.OrdinalIgnoreCase);
        var rules = new List<FirewallRuleDto>();
        foreach (var raw in result.Output.Split('\n'))
        {
            var line = raw.Trim();
            var m = FirewallRuleLineRegex().Match(line);
            if (!m.Success) continue;
            rules.Add(new FirewallRuleDto { Number = int.Parse(m.Groups[1].Value), Raw = m.Groups[2].Value.Trim() });
        }

        return new FirewallStatusDto { Installed = true, Active = active, Rules = rules };
    }

    public async Task<CommandResultDto> AddFirewallRuleAsync(long dropletId, AddFirewallRuleRequest request, CancellationToken ct = default)
    {
        var action = (request.Action ?? "").Trim().ToLowerInvariant();
        if (action is not ("allow" or "deny"))
            throw new ProviderException("Rule action must be allow or deny.", 400);

        var port = (request.Port ?? "").Trim();
        if (!PortOrServiceRegex().IsMatch(port))
            throw new ProviderException("Enter a port number (e.g. 8080) or a service name (e.g. OpenSSH).", 400);

        var proto = (request.Proto ?? "tcp").Trim().ToLowerInvariant();
        if (proto is not ("tcp" or "udp" or "any"))
            throw new ProviderException("Protocol must be tcp, udp or any.", 400);

        // Numeric ports can carry a protocol; named services can't.
        var spec = int.TryParse(port, out _) && proto is "tcp" or "udp" ? $"{port}/{proto}" : port;

        var host = await ResolveHostAsync(dropletId, ct);
        return ToCommandResult(await ssh.RunAsync(host, $"ufw {action} {spec} 2>&1", ct));
    }

    public async Task<CommandResultDto> DeleteFirewallRuleAsync(long dropletId, int number, CancellationToken ct = default)
    {
        if (number < 1) throw new ProviderException("Invalid rule number.", 400);
        var host = await ResolveHostAsync(dropletId, ct);
        return ToCommandResult(await ssh.RunAsync(host, $"ufw --force delete {number} 2>&1", ct));
    }

    public async Task<CommandResultDto> ToggleFirewallAsync(long dropletId, bool enabled, CancellationToken ct = default)
    {
        var host = await ResolveHostAsync(dropletId, ct);
        // When enabling, make sure SSH stays open first so we don't lock ourselves (and the user) out.
        var command = enabled
            ? "ufw allow OpenSSH >/dev/null 2>&1; ufw allow 22/tcp >/dev/null 2>&1; ufw --force enable 2>&1"
            : "ufw disable 2>&1";
        var result = await ssh.RunAsync(host, command, ct);
        if (result.ExitStatus == 0)
            await activity.RecordAsync("firewall", enabled ? "Firewall enabled" : "Firewall disabled", dropletId, enabled ? "success" : "warning", ct);
        return ToCommandResult(result);
    }

    [GeneratedRegex(@"^\[\s*(\d+)\]\s+(.*)$")]
    private static partial Regex FirewallRuleLineRegex();

    private static bool PortOrServiceRegexName(string name)
        => System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9._\-]{1,128}$");

    private static CommandResultDto ToCommandResult(SshResult r)
        => new() { ExitStatus = r.ExitStatus, Output = string.IsNullOrEmpty(r.Output) ? r.Error : r.Output, Error = r.Error };

    private static string BuildProxySite(string serverNames, int port) =>
        $$"""
        server {
            listen 80;
            listen [::]:80;
            server_name {{serverNames}};

            location / {
                proxy_pass http://127.0.0.1:{{port}};
                proxy_http_version 1.1;
                proxy_set_header Host $host;
                proxy_set_header X-Real-IP $remote_addr;
                proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
                proxy_set_header X-Forwarded-Proto $scheme;
                proxy_set_header Upgrade $http_upgrade;
                proxy_set_header Connection "upgrade";
            }
        }
        """;

    // A friendly "it works" placeholder so a freshly-created static site serves 200 right away.
    // domain is validated as a hostname before it reaches here, so it's safe to interpolate.
    private static string BuildPlaceholderHtml(string domain) =>
        $$"""
        <!doctype html>
        <html lang="en">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{domain}}</title>
            <style>
                :root { color-scheme: dark; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center;
                    font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
                    background: #0b0f17; color: #e6edf6; }
                .card { text-align: center; padding: 2rem; }
                h1 { font-size: 1.4rem; margin: 0 0 .5rem; }
                p { color: #9fb0c3; margin: .25rem 0; }
                code { background: #161c28; padding: .15rem .4rem; border-radius: 6px; color: #8fd9c0; }
            </style>
        </head>
        <body>
            <div class="card">
                <h1>{{domain}} is live 🚀</h1>
                <p>This site was set up by Orbit and is serving from its document root.</p>
                <p>Upload your files to replace this page.</p>
            </div>
        </body>
        </html>
        """;

    private static string BuildStaticSite(string serverNames, string root) =>
        $$"""
        server {
            listen 80;
            listen [::]:80;
            server_name {{serverNames}};

            root {{root}};
            index index.html index.htm;

            location / {
                try_files $uri $uri/ =404;
            }
        }
        """;

    private static DateTimeOffset? ParseOpensslDate(string value)
    {
        // e.g. "Nov  5 12:00:00 2026 GMT"
        var cleaned = value.Replace(" GMT", "", StringComparison.OrdinalIgnoreCase).Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        if (DateTime.TryParseExact(cleaned, "MMM d HH:mm:ss yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var dt))
            return new DateTimeOffset(dt, TimeSpan.Zero);
        return null;
    }
}
