using XsltCraft.Domain.Entities;

namespace XsltCraft.Application.ThemeSlots;

/// <summary>XSLT metninden belge türü: UBL-TR DespatchAdvice-2 ad alanı geçiyorsa irsaliye, değilse fatura.</summary>
public static class ThemeDocumentTypeSniffer
{
    private const string DespatchNamespace = "urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2";

    public static DocumentType Sniff(string xslt) =>
        xslt.Contains(DespatchNamespace, StringComparison.Ordinal) ? DocumentType.Despatch : DocumentType.Invoice;
}
