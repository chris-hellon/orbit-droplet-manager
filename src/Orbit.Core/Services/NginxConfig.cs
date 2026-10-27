using System.Text.RegularExpressions;

namespace Orbit.Core.Services;

/// <summary>
/// Minimal parser for the output of <c>nginx -T</c> (the full, resolved config dump). Pulls out the
/// server blocks Orbit cares about — domains, root, proxy target, listen ports, SSL cert path —
/// merging the HTTP + HTTPS blocks that make up one site.
/// </summary>
public static partial class NginxConfig
{
    public sealed record ParsedSite(
        string PrimaryDomain,
        IReadOnlyList<string> Domains,
        string Root,
        string? ProxyPass,
        string Listen,
        bool Ssl,
        string? SslCertPath,
        string ConfigPath);

    [GeneratedRegex(@"^\s*server\s*\{\s*$")]
    private static partial Regex ServerStartRegex();

    private sealed class Acc
    {
        public readonly SortedSet<string> Domains = new(StringComparer.OrdinalIgnoreCase);
        public readonly SortedSet<string> Ports = new(StringComparer.OrdinalIgnoreCase);
        public string Root = "";
        public string? ProxyPass;
        public string? SslCertPath;
        public bool Ssl;
        public string ConfigPath = "";
    }

    public static List<ParsedSite> Parse(string dump)
    {
        var sites = new Dictionary<string, Acc>(StringComparer.OrdinalIgnoreCase);
        var lines = dump.Replace("\r", "").Split('\n');
        var currentFile = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("# configuration file ", StringComparison.Ordinal))
            {
                currentFile = line["# configuration file ".Length..].TrimEnd(':').Trim();
                continue;
            }

            if (!ServerStartRegex().IsMatch(lines[i])) continue;

            // Gather the server block via brace counting.
            var block = new List<string>();
            var depth = 0;
            var opened = false;
            var j = i;
            for (; j < lines.Length; j++)
            {
                var l = lines[j];
                depth += l.Count(c => c == '{') - l.Count(c => c == '}');
                block.Add(l);
                if (l.Contains('{')) opened = true;
                if (opened && depth <= 0) break;
            }

            MergeBlock(block, currentFile, sites);
            i = j;
        }

        return sites.Values
            .Select(a => new ParsedSite(
                a.Domains.First(),
                a.Domains.ToList(),
                a.Root,
                a.ProxyPass,
                string.Join(", ", a.Ports),
                a.Ssl,
                a.SslCertPath,
                a.ConfigPath))
            .OrderBy(s => s.PrimaryDomain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void MergeBlock(List<string> block, string file, Dictionary<string, Acc> sites)
    {
        var names = new List<string>();
        string? root = null, proxy = null, sslCert = null;
        var ports = new List<string>();
        var ssl = false;

        foreach (var raw in block)
        {
            var line = raw.Trim();

            if (TryValue(line, "server_name", out var sn))
                names.AddRange(sn.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            else if (TryValue(line, "listen", out var ln))
            {
                var token = ln.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                var port = token.Contains(':') ? token[(token.LastIndexOf(':') + 1)..] : token;
                if (port.Length > 0) ports.Add(port);
                if (ln.Contains("ssl", StringComparison.OrdinalIgnoreCase) || port == "443") ssl = true;
            }
            else if (root is null && TryValue(line, "root", out var rt)) root = rt;
            else if (proxy is null && TryValue(line, "proxy_pass", out var pp)) proxy = pp;
            else if (sslCert is null && line.StartsWith("ssl_certificate ", StringComparison.Ordinal)
                     && TryValue(line, "ssl_certificate", out var sc)) sslCert = sc;
        }

        var realNames = names.Where(n => n != "_" && n.Length > 0 && n != "localhost").Distinct().ToList();
        if (realNames.Count == 0) return;

        var primary = realNames[0];
        if (!sites.TryGetValue(primary, out var acc))
        {
            acc = new Acc { ConfigPath = file };
            sites[primary] = acc;
        }

        foreach (var n in realNames) acc.Domains.Add(n);
        foreach (var p in ports) acc.Ports.Add(p);
        acc.Ssl |= ssl;
        if (acc.Root.Length == 0 && root is not null) acc.Root = root;
        acc.ProxyPass ??= proxy;
        acc.SslCertPath ??= sslCert;
        if (acc.ConfigPath.Length == 0) acc.ConfigPath = file;
    }

    private static bool TryValue(string line, string directive, out string value)
    {
        value = "";
        if (!line.StartsWith(directive + " ", StringComparison.Ordinal) &&
            !line.StartsWith(directive + "\t", StringComparison.Ordinal))
            return false;
        var rest = line[directive.Length..].Trim();
        var semi = rest.IndexOf(';');
        if (semi >= 0) rest = rest[..semi];
        value = rest.Trim();
        return value.Length > 0;
    }
}
