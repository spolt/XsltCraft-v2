using XsltCraft.Application.Membership;

namespace XsltCraft.Application.Interfaces;

/// <summary>
/// Kullanıcının etkin yetkilerini (rol + plan + abonelik) DB'den okuyup hesaplar.
/// Para-ilişkili tüm gate'ler bunu otoritatif kaynak olarak kullanır (JWT claim'ine güvenmez).
/// </summary>
public interface IEntitlementService
{
    Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default);
}
