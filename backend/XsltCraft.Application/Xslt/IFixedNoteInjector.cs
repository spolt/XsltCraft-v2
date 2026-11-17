namespace XsltCraft.Application.Xslt;

/// <summary>Sabit not tekrar uygulandığında izlenecek strateji.</summary>
public enum FixedNoteMode
{
    /// <summary>Mevcut sabit not bloklarını yeni metinle değiştir (idempotent — çoğalmaz).</summary>
    Replace,

    /// <summary>Mevcut sabit not bloklarına yeni bir not satırı ekle.</summary>
    Append,
}

/// <summary>Birden fazla eşit olasılıklı Notlar bölümü bulunduğunda davranış.</summary>
public enum FixedNoteAmbiguity
{
    /// <summary>Şablonu değiştirme, <see cref="FixedNoteStatus.Ambiguous"/> dön (kullanıcıya sorulur).</summary>
    Ask,

    /// <summary>Kullanıcı onayladı: belge sırasında önce gelen bölüm (ve kopyaları).</summary>
    PickFirst,
}

/// <summary>Enjeksiyon sonucu.</summary>
public enum FixedNoteStatus
{
    /// <summary>Sabit not başarıyla eklendi/güncellendi.</summary>
    Updated,

    /// <summary>Görünür bir Notlar bölümü (genel <c>cbc:Note</c> döngüsü / not tablosu) bulunamadı.</summary>
    NoNotesSection,

    /// <summary>Birden fazla eşit olasılıklı Notlar bölümü var; kullanıcı onayı gerekli.</summary>
    Ambiguous,

    /// <summary>XSLT ayrıştırılamadı ya da sonuç güvenlik/yerleşim kontrolünden geçemedi.</summary>
    Failed,
}

/// <param name="Targets">Notun eklendiği kopya sayısı (A5 çift kopyada 2).</param>
/// <param name="Candidates">Belirsizlikte aday Notlar bölümü sayısı.</param>
public sealed record FixedNoteResult(FixedNoteStatus Status, string Xslt, string? Error = null, int Targets = 0, int Candidates = 0);

/// <summary>
/// Üretilmiş/yüklenmiş bir XSLT'nin gerçek <b>Notlar</b> bölümünü (tablo bazında) bulup her belge kopyasında
/// bölümün başına tek bir sabit not satırı gömer. Seçici not okumaları (kodlu notlar, değişkenler) ve içerik
/// koşullu bölümler hedef alınmaz. Saf fonksiyon: string girer, string çıkar; Infrastructure'a bağımlı değildir.
/// </summary>
public interface IFixedNoteInjector
{
    FixedNoteResult Inject(string xslt, string noteText, FixedNoteMode mode, FixedNoteAmbiguity ambiguity = FixedNoteAmbiguity.Ask);
}
