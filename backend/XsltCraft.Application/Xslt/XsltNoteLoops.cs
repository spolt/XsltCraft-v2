using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XsltCraft.Application.Xslt;

/// <summary>Tema/şablon XSLT'lerinde görünür not bölümünü bulan ortak yardımcı (sabit not ve tema yuvaları).</summary>
internal static class XsltNoteLoops
{
    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    // Hedef: koşulsuz not gösterim döngüleri (e-Fatura + e-İrsaliye).
    private static readonly HashSet<string> NotePaths = new(StringComparer.Ordinal)
    {
        "//n1:Invoice/cbc:Note",
        "//n1:DespatchAdvice/cbc:Note",
    };

    // Görünür not bölümü, atasında ne koşul (if/when/choose) ne de değer-üretimi
    // (variable/param/with-param) bulunan bir for-each'tir. Bu yapıların içindeki not döngüleri
    // SGK değer çıkarımı gibi yardımcı hesaplardır (çıktıya doğrudan render edilmez) → atlanır.
    private static readonly string[] ExcludedAncestors =
        ["if", "when", "choose", "variable", "param", "with-param"];

    /// <summary>select'i not yoluna eşit, dışlanan bir atası olmayan ilk for-each.</summary>
    public static XElement? FindUnconditionalNoteLoop(XDocument doc) =>
        doc.Descendants(Xsl + "for-each")
            .Where(fe => fe.Attribute("select") is { Value: var sel } && NotePaths.Contains(Collapse(sel)))
            .FirstOrDefault(fe => !fe.Ancestors().Any(a =>
                a.Name.Namespace == Xsl && ExcludedAncestors.Contains(a.Name.LocalName)));

    private static string Collapse(string value) => Regex.Replace(value, @"\s+", string.Empty);
}
