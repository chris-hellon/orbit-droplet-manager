namespace Orbit.Shared;

/// <summary>Cloud provider behind a connection. Only DigitalOcean is implemented today.</summary>
public enum ProviderType
{
    DigitalOcean = 0,
    Hetzner = 1,
    Linode = 2,
}

/// <summary>Derived health used to colour a droplet on the globe (green / amber / red / grey).</summary>
public enum DropletHealth
{
    Unknown = 0,
    Healthy = 1,
    Warning = 2,
    Critical = 3,
}

/// <summary>Power operations that map onto the DigitalOcean droplet "actions" endpoint.</summary>
public enum PowerAction
{
    PowerOn = 0,
    PowerOff = 1,
    Reboot = 2,
    Shutdown = 3,
    PowerCycle = 4,
}
