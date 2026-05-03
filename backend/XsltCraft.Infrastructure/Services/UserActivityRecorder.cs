using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Services;

public class UserActivityRecorder(AppDbContext db) : IUserActivityRecorder
{
    public async Task RecordAsync(Guid userId, UserActivityType type, Guid? entityId = null, string? entityKind = null)
    {
        db.UserActivities.Add(new UserActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            EntityId = entityId,
            EntityKind = entityKind,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
