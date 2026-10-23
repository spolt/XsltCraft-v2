using System.Xml;
using System.Xml.Xsl;

namespace XsltCraft.Application.Xslt.Engines;

/// <summary>.NET XML/XSLT istisnalarını motor-bağımsız <see cref="XsltDiagnostic"/>'e çevirir.</summary>
public static class XsltDiagnostics
{
    /// <summary>
    /// XslCompiledTransform.Load derleme hatalarında dıştaki XsltException'ın mesajı her zaman
    /// jenerik "XSLT compile error."tır; asıl neden (geçersiz XPath, yanlış konumlanmış eleman vb.)
    /// InnerException zincirinde saklıdır. Zincirdeki tüm anlamlı mesajları birleştirir ve
    /// dıştaki satır/kolon 0 ise inner XsltException'dan doldurur.
    /// </summary>
    public static XsltDiagnostic FromXsltException(
        XsltException ex,
        XsltFailureKind kind = XsltFailureKind.Compile,
        XsltEngineKind? engine = null)
    {
        int line = ex.LineNumber;
        int column = ex.LinePosition;

        var messages = new List<string>();
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(cur.Message))
                messages.Add(cur.Message.Trim());
            if ((line == 0 || column == 0) && cur is XsltException xe && xe.LineNumber != 0)
            {
                line = xe.LineNumber;
                column = xe.LinePosition;
            }
        }

        var message = string.Join(" → ", messages.Distinct());
        return new XsltDiagnostic(message, line, column, kind, engine);
    }

    /// <summary>XML ayrıştırma hatası: mesaj ve konum olduğu gibi aktarılır.</summary>
    public static XsltDiagnostic FromXmlException(
        XmlException ex,
        XsltFailureKind kind = XsltFailureKind.Xml,
        XsltEngineKind? engine = null) =>
        new(ex.Message, ex.LineNumber, ex.LinePosition, kind, engine);
}
