namespace XsltCraft.Application.Xslt;

/// <summary>Sabit not tekrar uygulandığında izlenecek strateji.</summary>
public enum FixedNoteMode
{
    /// <summary>Mevcut sabit not bloğunu yeni metinle değiştir (idempotent — çoğalmaz).</summary>
    Replace,

    /// <summary>Mevcut sabit not bloğuna yeni bir not satırı ekle.</summary>
    Append,
}

/// <summary>Enjeksiyon sonucu.</summary>
public enum FixedNoteStatus
{
    /// <summary>Sabit not başarıyla eklendi/güncellendi.</summary>
    Updated,

    /// <summary>Koşulsuz not döngüsü (//n1:Invoice/cbc:Note veya //n1:DespatchAdvice/cbc:Note) bulunamadı.</summary>
    NoNotesSection,

    /// <summary>XSLT ayrıştırılamadı ya da sonuç güvenlik taramasından geçemedi.</summary>
    Failed,
}

public sealed record FixedNoteResult(FixedNoteStatus Status, string Xslt, string? Error = null);

/// <summary>
/// Üretilmiş bir XSLT'nin <b>koşulsuz</b> not gösterim döngüsünden hemen sonra sabit bir not
/// metni gömer. Koşullu (SGK vb. <c>xsl:if/when/choose</c> içindeki) not döngülerine dokunulmaz.
/// Saf fonksiyon: string girer, string çıkar; Infrastructure'a bağımlı değildir.
/// </summary>
public interface IFixedNoteInjector
{
    FixedNoteResult Inject(string xslt, string noteText, FixedNoteMode mode);
}
