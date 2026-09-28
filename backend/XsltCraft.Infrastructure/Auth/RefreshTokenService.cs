using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using XsltCraft.Application.Auth;
using XsltCraft.Application.Interfaces;
using XsltCraft.Domain.Entities;
using XsltCraft.Infrastructure.Persistence;

namespace XsltCraft.Infrastructure.Auth;

public class RefreshTokenService(AppDbContext db, IOptions<SessionOptions> options, TimeProvider clock)
    : IRefreshTokenService
{
    private readonly SessionPolicy _policy = new(options.Value);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken ct = default)
    {
        var now = Now;

        // Süresi dolmuş kayıtlar artık hiçbir karar için gerekmez (reuse tespiti de süre içindekilere bakar).
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.ExpiresAt < now)
            .ExecuteDeleteAsync(ct);

        var (expiresAt, sessionExpiresAt) = _policy.StartSession(now);
        return await AddAsync(Guid.NewGuid(), userId, now, expiresAt, sessionExpiresAt, ct);
    }

    public async Task<RefreshResult> RotateAsync(string rawToken, CancellationToken ct = default)
    {
        var now = Now;
        var hash = RefreshTokenHasher.Hash(rawToken);

        var existing = await db.RefreshTokens
            .AsNoTracking()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash, ct);

        if (existing is null)
            return new RefreshResult(RefreshStatus.Invalid);

        var state = new RefreshTokenState(
            existing.ExpiresAt, existing.SessionExpiresAt, existing.RevokedAt, existing.ReplacedByTokenId is not null);

        switch (_policy.Evaluate(state, now))
        {
            case RefreshDecision.ConcurrentRotation:
                return new RefreshResult(RefreshStatus.ConcurrentRotation);
            case RefreshDecision.ReuseDetected:
                await RevokeAllAsync(existing.UserId, ct);
                return new RefreshResult(RefreshStatus.ReuseDetected);
            case RefreshDecision.Expired:
                return new RefreshResult(RefreshStatus.Invalid);
        }

        if (!existing.User.IsActive)
            return new RefreshResult(RefreshStatus.UserInactive);

        // Atomik sahiplenme: token hâlâ aktifse iptal et. Aynı anda gelen ikinci istek 0 satır görür
        // ve aynı token'dan ikinci bir geçerli halef üretilemez.
        var successorId = Guid.NewGuid();
        var claimed = await db.RefreshTokens
            .Where(rt => rt.Id == existing.Id && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(rt => rt.RevokedAt, now)
                .SetProperty(rt => rt.ReplacedByTokenId, successorId), ct);

        if (claimed == 0)
            return new RefreshResult(RefreshStatus.ConcurrentRotation);

        var issued = await AddAsync(
            successorId, existing.UserId, now,
            _policy.NextExpiry(now, existing.SessionExpiresAt), existing.SessionExpiresAt, ct);

        return new RefreshResult(RefreshStatus.Success, issued, existing.User);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenHasher.Hash(rawToken);
        var now = Now;
        await db.RefreshTokens
            .Where(rt => rt.TokenHash == hash && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now), ct);
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        var now = Now;
        await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, now), ct);
    }

    private async Task<IssuedRefreshToken> AddAsync(
        Guid id, Guid userId, DateTime now, DateTime expiresAt, DateTime sessionExpiresAt, CancellationToken ct)
    {
        var raw = RefreshTokenHasher.Generate();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = id,
            UserId = userId,
            TokenHash = RefreshTokenHasher.Hash(raw),
            ExpiresAt = expiresAt,
            SessionExpiresAt = sessionExpiresAt,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(ct);
        return new IssuedRefreshToken(raw, expiresAt);
    }
}
