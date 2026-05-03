using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.Interfaces;

public interface IUserActivityRecorder
{
    Task RecordAsync(Guid userId, UserActivityType type, Guid? entityId = null, string? entityKind = null);
}
