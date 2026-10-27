using Orbit.Shared;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Providers;

/// <summary>Maps raw metrics + status onto the green/amber/red/grey health used by the globe.</summary>
public static class HealthEvaluator
{
    public const double CriticalThreshold = 90;
    public const double CpuWarnThreshold = 75;
    public const double MemWarnThreshold = 80;
    public const double DiskWarnThreshold = 80;

    public static DropletHealth Evaluate(string status, double? cpu, double? mem, double? disk)
    {
        // A droplet that isn't running has no meaningful live health.
        if (!string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            return DropletHealth.Unknown;

        if (cpu is null && mem is null && disk is null)
            return DropletHealth.Unknown;

        if (cpu >= CriticalThreshold || mem >= CriticalThreshold || disk >= CriticalThreshold)
            return DropletHealth.Critical;

        if (cpu >= CpuWarnThreshold || mem >= MemWarnThreshold || disk >= DiskWarnThreshold)
            return DropletHealth.Warning;

        return DropletHealth.Healthy;
    }
}
