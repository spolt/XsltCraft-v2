using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.ThemeSlots.Overrides;

/// <summary>
/// Görsel bloğundaki banka tablosunu (<see cref="ParsedOverrides.BankTableXml"/>) geri okur: <c>tbody &gt; tr</c>
/// içindeki ilk iki <c>td</c> → banka adı, IBAN. Bozuk girdide boş liste.
/// </summary>
internal static class BankTableReader
{
    public static IReadOnlyList<BankInfoInput> Read(string? bankTableXml)
    {
        if (string.IsNullOrWhiteSpace(bankTableXml))
            return [];

        XElement root;
        try
        {
            using var reader = XmlReader.Create(new StringReader(bankTableXml), SecureXslt.ReaderSettings());
            root = XElement.Load(reader);
        }
        catch (XmlException)
        {
            return [];
        }

        return root.Descendants("tbody").Elements("tr")
            .Select(tr => tr.Elements("td").Take(2).Select(td => td.Value.Trim()).ToList())
            .Select(cells => new BankInfoInput(cells.ElementAtOrDefault(0) ?? "", cells.ElementAtOrDefault(1) ?? ""))
            .Take(ThemeComposeRequestValidator.MaxBankRows)
            .ToList();
    }
}
