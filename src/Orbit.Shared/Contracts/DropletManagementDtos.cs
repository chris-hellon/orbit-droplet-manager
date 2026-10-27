using System.ComponentModel.DataAnnotations;

namespace Orbit.Shared.Contracts;

/// <summary>A systemd service unit on a droplet.</summary>
public sealed record ServiceUnitDto
{
    public string Unit { get; init; } = "";
    public string Load { get; init; } = "";
    public string Active { get; init; } = "";
    public string Sub { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>True when this is one of your own / app services (admin-installed or a known web/app/db server), as opposed to stock OS machinery.</summary>
    public bool App { get; init; }

    public bool IsRunning => Active.Equals("active", StringComparison.OrdinalIgnoreCase)
                             && Sub.Equals("running", StringComparison.OrdinalIgnoreCase);
    public bool IsFailed => Active.Equals("failed", StringComparison.OrdinalIgnoreCase)
                            || Sub.Equals("failed", StringComparison.OrdinalIgnoreCase);
}

public sealed class ServiceActionRequest
{
    public string Unit { get; set; } = "";

    /// <summary>start | stop | restart.</summary>
    public string Action { get; set; } = "restart";
}

/// <summary>A web site/vhost discovered on a droplet (nginx).</summary>
public sealed record SiteDto
{
    public string Server { get; init; } = "nginx";
    public string PrimaryDomain { get; init; } = "";
    public IReadOnlyList<string> Domains { get; init; } = [];
    public string Root { get; init; } = "";
    /// <summary>Reverse-proxy upstream, if the site proxies (e.g. http://localhost:5000).</summary>
    public string? ProxyPass { get; init; }
    public string Listen { get; init; } = "";
    public bool Ssl { get; init; }
    public DateTimeOffset? SslExpiresAt { get; init; }
    public string ConfigPath { get; init; } = "";

    public int? SslDaysLeft => SslExpiresAt is { } e ? (int)Math.Floor((e - DateTimeOffset.UtcNow).TotalDays) : null;
}

/// <summary>Output of a log tail command.</summary>
public sealed record LogOutputDto
{
    public string Source { get; init; } = "";
    public string Content { get; init; } = "";
}

/// <summary>A command run result surfaced to the UI.</summary>
public sealed record CommandResultDto
{
    public int ExitStatus { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public bool Ok => ExitStatus == 0;
}

// ---- Actions: create a site, enable HTTPS ----

public sealed class CreateSiteRequest
{
    [Required] public string Domain { get; set; } = "";
    /// <summary>Extra domains for the same site (space or comma separated), e.g. "www.example.com".</summary>
    public string? Aliases { get; set; }
    /// <summary>"proxy" (reverse proxy to a local port) or "static" (serve files from a folder).</summary>
    public string Mode { get; set; } = "proxy";
    public int? UpstreamPort { get; set; }
    public string? Root { get; set; }
}

public sealed class EnableHttpsRequest
{
    [Required] public string Domain { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
}

public sealed class SiteToggleRequest
{
    [Required] public string ConfigPath { get; set; } = "";
    public bool Enabled { get; set; }
}

// ---- Firewall (ufw) ----

public sealed record FirewallRuleDto
{
    public int Number { get; init; }
    public string Raw { get; init; } = "";
}

public sealed record FirewallStatusDto
{
    public bool Installed { get; init; }
    public bool Active { get; init; }
    public IReadOnlyList<FirewallRuleDto> Rules { get; init; } = [];

    public static FirewallStatusDto NotInstalled { get; } = new() { Installed = false };
}

public sealed class AddFirewallRuleRequest
{
    /// <summary>allow | deny</summary>
    public string Action { get; set; } = "allow";
    /// <summary>Port number ("80") or a known service name ("OpenSSH").</summary>
    [Required] public string Port { get; set; } = "";
    /// <summary>tcp | udp | any</summary>
    public string Proto { get; set; } = "tcp";
}

public sealed class FirewallToggleRequest
{
    public bool Enabled { get; set; }
}

// ---- On-box database server (MySQL/MariaDB or PostgreSQL) ----

public sealed record DbServerDto
{
    public string Engine { get; init; } = "none"; // mysql | postgres | none
    public bool Installed { get; init; }
    public bool Running { get; init; }
    public string Version { get; init; } = "";
    public IReadOnlyList<string> Databases { get; init; } = [];
    public IReadOnlyList<string> Users { get; init; } = [];

    public string EngineLabel => Engine switch
    {
        "mysql" => "MySQL / MariaDB",
        "postgres" => "PostgreSQL",
        _ => "None",
    };

    public string ServiceUnit => Engine switch { "mysql" => "mysql", "postgres" => "postgresql", _ => "" };

    public static DbServerDto None { get; } = new() { Engine = "none" };
}

public sealed class DbNameRequest
{
    [Required, MinLength(1)] public string Name { get; set; } = "";
}

/// <summary>A newly created DB user — the generated password is shown once.</summary>
public sealed record CreatedDbUserDto
{
    public string Name { get; init; } = "";
    public string Password { get; init; } = "";
}
