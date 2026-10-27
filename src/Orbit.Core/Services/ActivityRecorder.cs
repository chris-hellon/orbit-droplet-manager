using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Core.Data;
using Orbit.Shared.Contracts;

namespace Orbit.Core.Services;

public interface IActivityRecorder
{
    /// <summary>Record an action. Never throws — a logging failure must not fail the action it describes.</summary>
    Task RecordAsync(string category, string message, long? dropletId = null, string kind = "info", CancellationToken ct = default);

    Task<IReadOnlyList<ActivityEntryDto>> GetRecentAsync(int take = 25, CancellationToken ct = default);
}

public sealed class ActivityRecorder(OrbitDbContext db, TimeProvider clock, ILogger<ActivityRecorder> logger) : IActivityRecorder
{
    public async Task RecordAsync(string category, string message, long? dropletId = null, string kind = "info", CancellationToken ct = default)
    {
        try
        {
            db.Activity.Add(new ActivityEntry
            {
                WorkspaceId = Seed.WorkspaceId,
                TimestampUtc = clock.GetUtcNow(),
                Category = category,
                DropletId = dropletId,
                Message = message.Length > 500 ? message[..500] : message,
                Kind = kind,
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to record activity '{Category}: {Message}'.", category, message);
        }
    }

    public async Task<IReadOnlyList<ActivityEntryDto>> GetRecentAsync(int take = 25, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        // SQLite can't ORDER BY a DateTimeOffset; Id is auto-increment so it's the same chronological order.
        return await db.Activity.AsNoTracking()
            .Where(a => a.WorkspaceId == Seed.WorkspaceId)
            .OrderByDescending(a => a.Id)
            .Take(take)
            .Select(a => new ActivityEntryDto
            {
                Id = a.Id,
                TimestampUtc = a.TimestampUtc,
                Category = a.Category,
                DropletId = a.DropletId,
                Message = a.Message,
                Kind = a.Kind,
            })
            .ToListAsync(ct);
    }
}
