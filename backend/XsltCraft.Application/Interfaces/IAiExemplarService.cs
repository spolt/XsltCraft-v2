using XsltCraft.Application.Ai;

namespace XsltCraft.Application.Interfaces;

public interface IAiExemplarService
{
    /// <summary>
    /// Kullanıcının (ve global havuzun) pozitif geri bildirimlerinden, verilen soruya en
    /// alakalı 0-2 örneği döner. Erişim hata verirse boş liste döner (sohbet kırılmamalı).
    /// </summary>
    Task<IReadOnlyList<AiExemplar>> GetExemplarsAsync(Guid userId, string? userRequest, CancellationToken ct);
}
