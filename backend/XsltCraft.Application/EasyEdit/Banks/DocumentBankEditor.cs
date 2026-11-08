using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Source;
using XsltCraft.Domain.Entities;

using static XsltCraft.Application.ThemeSlots.Analysis.ThemeDocument;

namespace XsltCraft.Application.EasyEdit.Banks;

/// <summary>Belgedeki bir banka satırına düzenleme.</summary>
/// <param name="Index">Tespit edilen satır (<see cref="DocumentBankRowInfo.Index"/>); null → yeni satır eklenir.</param>
/// <param name="Remove">Satır silinir (<paramref name="Index"/> zorunlu).</param>
public sealed record DocumentBankEditInput(int? Index, string BankName, string Iban, bool Remove);

/// <param name="AddedBytes">Metnin UTF-8 boyutundaki net artış (küçüldüyse 0).</param>
internal sealed record DocumentBankEditResult(string Xslt, bool Changed, IReadOnlyList<ThemeComposeWarning> Warnings, int AddedBytes);

/// <summary>
/// Belgenin kendi banka satırlarını (<see cref="DocumentBankDetector"/>) metin üzerinde cerrahi olarak düzenler: IBAN/ad
/// aralıkları değiştirilir, satırlar silinir, yeni satırlar son satırın kopyası olarak eklenir. Tüm satırlar silinen ve
/// kaldırılabilir tablo bütünüyle kaldırılır. XSL yapısı, görseller ve <c>data-xc-*</c> nitelikleri korunur; uygulanamayan
/// istekler uyarıyla atlanır. Okunamayan durumda metin değişmeden döner.
/// </summary>
internal static class DocumentBankEditor
{
    public const string NotFound = "document-bank-not-found";
    public const string RowNotFound = "document-bank-row-not-found";
    public const string RowLocked = "document-bank-row-locked";
    public const string AddUnsupported = "document-bank-add-unsupported";
    public const string Limit = "document-bank-limit";
    public const string TooLarge = "document-bank-too-large";
    public const string Skipped = "document-bank-skipped";

    private const string XcAttributePrefix = "data-xc-";

    public static DocumentBankEditResult Apply(string xslt, DocumentType type, IReadOnlyList<DocumentBankEditInput>? edits)
    {
        if (edits is null || edits.Count == 0)
            return new DocumentBankEditResult(xslt, false, [], 0);

        try
        {
            return ApplyCore(xslt, type, edits);
        }
        catch (Exception ex) when (ex is RegexMatchTimeoutException or InvalidOperationException or ArgumentException
                                       or KeyNotFoundException or XmlException)
        {
            return Unchanged(xslt, [Warning(Skipped)]);
        }
    }

    private static DocumentBankEditResult ApplyCore(string xslt, DocumentType type, IReadOnlyList<DocumentBankEditInput> edits)
    {
        var detection = DocumentBankDetector.Detect(xslt, type);
        if (!detection.Found)
            return Unchanged(xslt, [Warning(NotFound)]);

        var warnings = new List<ThemeComposeWarning>();
        var budget = (long)ThemeComposeRequestValidator.MaxXsltBytes - Encoding.UTF8.GetByteCount(xslt);
        long growth = 0;

        var rowEdits = new List<(int Start, int End, string Text, int? Row)>();
        var removed = new HashSet<int>();
        var seen = new HashSet<int>();

        // 1. Mevcut satırlar: değiştirme ve silme.
        foreach (var edit in edits.Where(e => e.Index is not null))
        {
            var index = edit.Index!.Value;
            if (index < 0 || index >= detection.Rows.Count || !seen.Add(index))
            {
                warnings.Add(Warning(RowNotFound));
                continue;
            }

            var row = detection.Rows[index];
            if (edit.Remove)
            {
                if (!row.Deletable)
                    warnings.Add(Warning(RowLocked));
                else
                    removed.Add(index);
                continue;
            }

            var nameChanged = !string.Equals(edit.BankName, row.BankName, StringComparison.Ordinal);
            if (nameChanged && !row.BankNameEditable)
            {
                warnings.Add(Warning(RowLocked));
                continue;
            }

            var pending = new List<(int Start, int End, string Text, int? Row)>();
            if (!string.Equals(edit.Iban, row.Iban, StringComparison.Ordinal))
                pending.Add((row.IbanStart, row.IbanEnd, Escape(edit.Iban), index));
            if (nameChanged)
                pending.Add((row.NameStart!.Value, row.NameEnd!.Value, Escape(edit.BankName), index));
            if (pending.Count == 0)
                continue;

            var delta = pending.Sum(p => (long)Encoding.UTF8.GetByteCount(p.Text) - Encoding.UTF8.GetByteCount(xslt.AsSpan(p.Start, p.End - p.Start)));
            if (growth + delta > budget)
            {
                warnings.Add(Warning(TooLarge));
                continue;
            }
            growth += delta;
            rowEdits.AddRange(pending);
        }

        // 2. Yeni satırlar: şablon satırın özgün metninin kopyası, toplam satır tavanı içinde.
        var inserts = new List<string>();
        var total = detection.Rows.Count - removed.Count;
        foreach (var edit in edits.Where(e => e.Index is null))
        {
            if (!detection.CanAdd || detection.InsertOffset is null)
            {
                warnings.Add(Warning(AddUnsupported));
                continue;
            }
            if (total + 1 > ThemeComposeRequestValidator.MaxBankRows)
            {
                warnings.Add(Warning(Limit));
                continue;
            }

            var text = (detection.InsertIndent ?? "") + CopyRow(xslt, detection.Rows[^1], edit);
            var delta = (long)Encoding.UTF8.GetByteCount(text);
            if (growth + delta > budget)
            {
                warnings.Add(Warning(TooLarge));
                continue;
            }
            growth += delta;
            total++;
            inserts.Add(text);
        }

        // 3. Silmeler: tüm satırları giden kaldırılabilir tablo bütünüyle kalkar (ekleme yoksa).
        var sourceEdits = new SourceEdits();
        var coveredByTable = new HashSet<int>();
        if (inserts.Count == 0)
        {
            foreach (var table in detection.Tables.Where(t => t.Removable && t.RowIndexes.Count > 0 && t.RowIndexes.All(removed.Contains)))
            {
                sourceEdits.Replace(table.Start, table.End, "");
                coveredByTable.UnionWith(table.RowIndexes);
            }
        }
        foreach (var index in removed.Where(i => !coveredByTable.Contains(i)).Order())
        {
            var row = detection.Rows[index];
            sourceEdits.Replace(row.RowStart, row.RowEnd, "");
        }
        foreach (var (start, end, text, rowIndex) in rowEdits)
        {
            if (rowIndex is { } r && (removed.Contains(r) || coveredByTable.Contains(r)))
                continue;
            sourceEdits.Replace(start, end, text);
        }
        foreach (var text in inserts)
            sourceEdits.Insert(detection.InsertOffset!.Value, text);

        if (sourceEdits.Count == 0)
            return Unchanged(xslt, warnings);

        var result = sourceEdits.Apply(xslt);
        if (!XsltSourceMap.TryBuild(result, out var rebuilt, out _)
            || !XsltSourceMap.TryBuild(xslt, out var original, out _)
            || !SameStructure(original.Document, rebuilt.Document))
            return Unchanged(xslt, [.. warnings, Warning(Skipped)]);

        var added = Math.Max(0, Encoding.UTF8.GetByteCount(result) - Encoding.UTF8.GetByteCount(xslt));
        return new DocumentBankEditResult(result, true, warnings, added);
    }

    private static DocumentBankEditResult Unchanged(string xslt, IReadOnlyList<ThemeComposeWarning> warnings) =>
        new(xslt, false, warnings, 0);

    /// <summary>Şablon satırın özgün metni; IBAN ve ad aralıkları satıra göreli ofsetle değiştirilir.</summary>
    private static string CopyRow(string xslt, DocumentBankRow template, DocumentBankEditInput edit)
    {
        var rowText = xslt[template.RowStart..template.RowEnd];
        var local = new SourceEdits();
        local.Replace(template.IbanStart - template.RowStart, template.IbanEnd - template.RowStart, Escape(edit.Iban));
        local.Replace(template.NameStart!.Value - template.RowStart, template.NameEnd!.Value - template.RowStart, Escape(edit.BankName));
        return local.Apply(rowText);
    }

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>XSL elemanları (<c>xsl:text</c> hariç — silinen satırın tutucusu olabilir), görseller ve <c>data-xc-*</c>
    /// nitelikleri sayı ve sırasıyla aynı mı?</summary>
    private static bool SameStructure(XDocument before, XDocument after) =>
        Signature(before).SequenceEqual(Signature(after), StringComparer.Ordinal);

    private static IEnumerable<string> Signature(XDocument doc)
    {
        foreach (var e in doc.Descendants())
        {
            if (IsXsl(e) && !IsXsl(e, "text"))
                yield return "xsl:" + e.Name.LocalName;
            else if (IsLiteral(e, "img"))
                yield return "img:" + (string?)e.Attribute("src");
            foreach (var a in e.Attributes().Where(a => a.Name.LocalName.StartsWith(XcAttributePrefix, StringComparison.Ordinal)))
                yield return a.Name.LocalName + "=" + a.Value;
        }
    }

    private static ThemeComposeWarning Warning(string code) => new(code, code switch
    {
        NotFound => "Belgede düzenlenebilir banka bilgisi bulunamadı; banka değişiklikleri uygulanmadı.",
        RowNotFound => "Belgedeki banka satırı bulunamadı; değişiklik uygulanmadı.",
        RowLocked => "Bu banka satırı düzenlenemez ya da silinemez; değişiklik uygulanmadı.",
        AddUnsupported => "Bu belgedeki banka tablosuna yeni satır eklenemiyor.",
        Limit => $"En çok {ThemeComposeRequestValidator.MaxBankRows} banka satırı olabilir; fazlası eklenmedi.",
        TooLarge => "Banka değişikliği şablonu izin verilen boyutun üstüne çıkarırdı; atlandı.",
        _ => "Belgedeki banka bilgisinde yapılan değişiklikler uygulanamadı; metin değiştirilmedi.",
    });
}
