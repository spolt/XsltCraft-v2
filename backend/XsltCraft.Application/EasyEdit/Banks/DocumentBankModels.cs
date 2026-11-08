using System.Text.RegularExpressions;

namespace XsltCraft.Application.EasyEdit.Banks;

/// <summary>Türkiye IBAN desenleri: 'TR' + 24 rakam, rakamlar arasında tek boşluk ya da bölünmez boşluk olabilir.
/// Yalnız ASCII rakam kabul edilir.</summary>
internal static partial class TrIban
{
    private const int MatchTimeoutMs = 2000;

    /// <summary>Metin içinde arama: önünde harf/rakam, arkasında rakam olmamalı.</summary>
    [GeneratedRegex(@"(?<![A-Za-z0-9])TR[0-9]{2}(?:[  ]?[0-9]){22}(?![0-9])", RegexOptions.CultureInvariant, MatchTimeoutMs)]
    public static partial Regex Find();

    /// <summary>Değerin tamamı bir IBAN mı (istek doğrulaması).</summary>
    [GeneratedRegex(@"^TR[0-9]{2}(?:[  ]?[0-9]){22}\z", RegexOptions.CultureInvariant, MatchTimeoutMs)]
    public static partial Regex Exact();
}

/// <summary>Belgede tespit edilen bir banka satırı. Ofsetler özgün XSLT metnine göredir.</summary>
/// <param name="Index">Belge sırası (düzenleme isteğindeki index).</param>
/// <param name="BankNameEditable">Ad aralığı bulundu ve kenarlardaki dolgu (boşluk, bölünmez boşluk varlıkları, ayraçlar)
/// kırpıldıktan sonra ham parçada varlık (<c>&amp;</c>) yok.</param>
/// <param name="Deletable">Satır alt ağacında <c>xsl:text</c> dışında XSL, <c>img</c> ya da <c>data-xc-*</c> niteliği yok.</param>
/// <param name="Line">IBAN'ın 1 tabanlı satır numarası.</param>
/// <param name="RowStart">Satır elemanının başı.</param>
/// <param name="RowEnd">Satır elemanının sonu.</param>
/// <param name="TableId">Satır bir tablo <c>tr</c>'si ise <see cref="DocumentBankTable.Id"/>.</param>
internal sealed record DocumentBankRow(int Index, string BankName, string Iban, bool BankNameEditable, bool Deletable, int Line,
    int RowStart, int RowEnd, int IbanStart, int IbanEnd, int? NameStart, int? NameEnd, int? TableId);

/// <param name="Removable">Tüm satırlar silinince tablo da kaldırılabilir.</param>
internal sealed record DocumentBankTable(int Id, int Start, int End, bool Removable, IReadOnlyList<int> RowIndexes);

/// <param name="InsertOffset">Yeni satırın ekleneceği ofset (şablon satırın sonu); ekleme yapılamıyorsa null.</param>
/// <param name="InsertIndent">Yeni satırın önüne yazılacak boşluk (şablon satırın önündeki girinti).</param>
internal sealed record DocumentBankDetection(bool Found, bool CanAdd, IReadOnlyList<DocumentBankRow> Rows,
    IReadOnlyList<DocumentBankTable> Tables, int? InsertOffset, string? InsertIndent)
{
    public static readonly DocumentBankDetection None = new(false, false, [], [], null, null);

    public DocumentBankInfo ToInfo() => new(Found, CanAdd,
        Rows.Select(r => new DocumentBankRowInfo(r.Index, r.BankName, r.Iban, r.BankNameEditable, r.Deletable, r.Line)).ToList());
}

/// <summary>Belgenin kendi banka/IBAN satırları (kolay düzenleme analizi).</summary>
public sealed record DocumentBankInfo(bool Found, bool CanAdd, IReadOnlyList<DocumentBankRowInfo> Rows);

public sealed record DocumentBankRowInfo(int Index, string BankName, string Iban, bool BankNameEditable, bool Deletable, int Line);
