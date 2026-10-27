namespace Orbit.Shared;

/// <summary>Single source of truth for API + hub routes, shared by the minimal-API server and the WASM client.</summary>
public static class ApiRoutes
{
    public const string Base = "/api";

    public static class Connection
    {
        public const string Status = Base + "/connection";
        public const string Connect = Base + "/connection";       // POST
        public const string Disconnect = Base + "/connection";    // DELETE
        public const string Validate = Base + "/connection/validate"; // POST (test a token without saving)
    }

    public static class Droplets
    {
        public const string List = Base + "/droplets";
        public static string Get(long id) => $"{Base}/droplets/{id}";
        public static string Power(long id) => $"{Base}/droplets/{id}/power";
        public static string Snapshot(long id) => $"{Base}/droplets/{id}/snapshot";
        public static string Resize(long id) => $"{Base}/droplets/{id}/resize";
        public static string Rebuild(long id) => $"{Base}/droplets/{id}/rebuild";
        public static string Metrics(long id) => $"{Base}/droplets/{id}/metrics";

        // --- inside-the-droplet management (over SSH) ---
        public static string SshTest(long id) => $"{Base}/droplets/{id}/ssh-test";
        public static string Services(long id) => $"{Base}/droplets/{id}/services";
        public static string ServiceAction(long id) => $"{Base}/droplets/{id}/service-action";
        public static string Logs(long id) => $"{Base}/droplets/{id}/logs";
        public static string Sites(long id) => $"{Base}/droplets/{id}/sites";                 // GET list, POST create
        public static string SiteHttps(long id) => $"{Base}/droplets/{id}/sites/https";        // POST
        public static string SiteToggle(long id) => $"{Base}/droplets/{id}/sites/toggle";       // POST
        public static string NginxReload(long id) => $"{Base}/droplets/{id}/nginx-reload";      // POST
        public static string Firewall(long id) => $"{Base}/droplets/{id}/firewall";             // GET status, POST add rule
        public static string FirewallRule(long id, int number) => $"{Base}/droplets/{id}/firewall/{number}"; // DELETE
        public static string FirewallToggle(long id) => $"{Base}/droplets/{id}/firewall/toggle"; // POST

        public static string Db(long id) => $"{Base}/droplets/{id}/db";                            // GET overview
        public static string DbDatabases(long id) => $"{Base}/droplets/{id}/db/databases";          // POST create
        public static string DbDatabase(long id, string name) => $"{Base}/droplets/{id}/db/databases/{Uri.EscapeDataString(name)}"; // DELETE
        public static string DbUsers(long id) => $"{Base}/droplets/{id}/db/users";                  // POST create
        public static string DbUser(long id, string name) => $"{Base}/droplets/{id}/db/users/{Uri.EscapeDataString(name)}"; // DELETE
    }

    public static class Ssh
    {
        public const string Status = Base + "/ssh";
        public const string Save = Base + "/ssh";      // POST
        public const string Delete = Base + "/ssh";    // DELETE
    }

    public static class Regions
    {
        public const string List = Base + "/regions";
    }

    public static class Catalog
    {
        public const string Sizes = Base + "/sizes";
        public const string Images = Base + "/images";
    }

    public static class Activity
    {
        public const string List = Base + "/activity";
    }

    public static class Domains
    {
        public const string List = Base + "/domains";
        public static string Get(string name) => $"{Base}/domains/{Uri.EscapeDataString(name)}";
        public static string Records(string name) => $"{Base}/domains/{Uri.EscapeDataString(name)}/records";
        public static string Record(string name, long id) => $"{Base}/domains/{Uri.EscapeDataString(name)}/records/{id}";
    }

    public static class Databases
    {
        public const string List = Base + "/databases";
        public static string Get(string id) => $"{Base}/databases/{id}";
        public static string Users(string id) => $"{Base}/databases/{id}/users";
        public static string Dbs(string id) => $"{Base}/databases/{id}/dbs";
        public static string Db(string id, string name) => $"{Base}/databases/{id}/dbs/{Uri.EscapeDataString(name)}";
        public static string User(string id, string name) => $"{Base}/databases/{id}/users/{Uri.EscapeDataString(name)}";
    }

    public static class Hubs
    {
        /// <summary>SignalR hub that streams live metrics + droplet-state changes.</summary>
        public const string Metrics = "/hubs/metrics";

        /// <summary>SignalR hub bridging an in-browser terminal to an SSH shell on a droplet.</summary>
        public const string Terminal = "/hubs/terminal";
    }
}
