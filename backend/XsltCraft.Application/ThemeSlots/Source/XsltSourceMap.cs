using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Xml.Linq;

using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.ThemeSlots.Source;

/// <summary>Bir elemanın kaynak metindeki konumu. Start '&lt;' karakteri; StartTagEnd açılış etiketinden hemen
/// sonra; EndTagStart '&lt;/' başlangıcı; End kapanış etiketinden hemen sonra. Self-closing'de
/// EndTagStart == StartTagEnd == End.</summary>
internal readonly record struct ElementSpan(int Start, int StartTagEnd, int EndTagStart, int End, bool SelfClosing);

/// <summary>
/// XSLT kaynağını yeniden serileştirmeden cerrahi düzenleme için: <see cref="XDocument"/> elemanlarını kaynak
/// metindeki ofsetlerine eşler. İki geçiş (XmlReader konumları + XDocument) belge sırasındaki ordinal ile
/// eşleştirilir; ikisi de <see cref="SecureXslt.ReaderSettings"/> ile okunur (XXE kapalı).
/// </summary>
internal sealed class XsltSourceMap
{
    private readonly Dictionary<XElement, ElementSpan> _spans;
    private readonly int[] _lineStarts;

    public string Source { get; }
    public XDocument Document { get; }

    private XsltSourceMap(string source, XDocument document, Dictionary<XElement, ElementSpan> spans, int[] lineStarts)
    {
        Source = source;
        Document = document;
        _spans = spans;
        _lineStarts = lineStarts;
    }

    public ElementSpan Span(XElement e) => _spans[e];

    /// <summary>Ofsetin 1 tabanlı satır numarası (rapor için).</summary>
    public int LineOf(int offset)
    {
        var idx = Array.BinarySearch(_lineStarts, offset);
        return (idx >= 0 ? idx : ~idx - 1) + 1;
    }

    public static bool TryBuild(string source, [NotNullWhen(true)] out XsltSourceMap? map, out string? error)
    {
        map = null;
        error = null;
        try
        {
            var lineStarts = LineStarts(source);
            var ordered = ReadSpans(source, lineStarts);

            XDocument doc;
            using (var xr = XmlReader.Create(new StringReader(source), SecureXslt.ReaderSettings()))
                doc = XDocument.Load(xr, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);

            var elements = doc.Descendants().ToList();
            if (elements.Count != ordered.Count)
            {
                error = "Kaynak eşlemesi tutarsız (eleman sayısı uyuşmuyor).";
                return false;
            }

            var spans = new Dictionary<XElement, ElementSpan>(elements.Count, ReferenceEqualityComparer.Instance);
            for (var i = 0; i < elements.Count; i++)
                spans[elements[i]] = ordered[i];

            map = new XsltSourceMap(source, doc, spans, lineStarts);
            return true;
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    // XmlReader satır sonlarını \r\n, \r ve \n olarak sayar; aynı kuralla satır başlangıç ofsetleri.
    private static int[] LineStarts(string s)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\r')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n') i++;
                starts.Add(i + 1);
            }
            else if (s[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }
        return [.. starts];
    }

    private static List<ElementSpan> ReadSpans(string source, int[] lineStarts)
    {
        var result = new List<ElementSpan>();
        var open = new Stack<int>(); // result içindeki indeks

        using var reader = XmlReader.Create(new StringReader(source), SecureXslt.ReaderSettings());
        var info = (IXmlLineInfo)reader;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var start = Offset(lineStarts, info.LineNumber, info.LinePosition) - 1;
                if (start < 0 || source[start] != '<')
                    throw new InvalidOperationException($"Eleman konumu doğrulanamadı (satır {info.LineNumber}).");

                var startTagEnd = FindTagEnd(source, start);
                if (reader.IsEmptyElement)
                {
                    result.Add(new ElementSpan(start, startTagEnd, startTagEnd, startTagEnd, SelfClosing: true));
                }
                else
                {
                    result.Add(new ElementSpan(start, startTagEnd, -1, -1, SelfClosing: false));
                    open.Push(result.Count - 1);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                var endTagStart = Offset(lineStarts, info.LineNumber, info.LinePosition) - 2;
                if (endTagStart < 0 || source[endTagStart] != '<' || source[endTagStart + 1] != '/')
                    throw new InvalidOperationException($"Kapanış etiketi konumu doğrulanamadı (satır {info.LineNumber}).");

                var gt = source.IndexOf('>', endTagStart);
                if (gt < 0)
                    throw new InvalidOperationException("Kapanış etiketi bitmiyor.");

                var idx = open.Pop();
                result[idx] = result[idx] with { EndTagStart = endTagStart, End = gt + 1 };
            }
        }

        return result;
    }

    private static int Offset(int[] lineStarts, int line, int position)
    {
        if (line < 1 || line > lineStarts.Length || position < 1)
            return -1;
        return lineStarts[line - 1] + position - 1;
    }

    // Açılış etiketinin bitişi: tırnak içindeki '>' (öznitelikte yasal) atlanır.
    private static int FindTagEnd(string s, int start)
    {
        char? quote = null;
        for (var i = start + 1; i < s.Length; i++)
        {
            var c = s[i];
            if (quote is not null)
            {
                if (c == quote) quote = null;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i + 1;
            }
        }
        throw new InvalidOperationException("Açılış etiketi bitmiyor.");
    }
}
