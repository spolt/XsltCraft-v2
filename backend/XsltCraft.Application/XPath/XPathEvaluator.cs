using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

using XsltCraft.Application.Preview;
using XsltCraft.Application.Xslt;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;

namespace XsltCraft.Application.XPath;

/// <summary>
/// XPath konsolu (serbest XPath 1.0 deneme aracı). Güvenlik katmanları (G4):
/// <list type="number">
/// <item><b>Maliyet kapısı:</b> <see cref="XPathPortability.Analyze"/> — yalnız <c>XPATH_TOO_LONG</c> /
///   <c>XPATH_TOO_COMPLEX</c> engeller. <c>XPATH_NOT_PORTABLE</c> (fonksiyon beyaz listesi, çift-motor anlamı)
///   bilerek uygulanmaz: konsol tek motorlu (.NET XPath 1.0) bir deneme aracıdır, "iki motorda aynı sonuç"
///   garantisi gerekmez; beyaz liste dışı fonksiyonlar zaten .NET'te <see cref="XPathException"/> verir.</item>
/// <item><b>Güvenli okuma:</b> <see cref="SecureXslt.ReaderSettings"/> (DTD yasak, resolver yok) + grid sınırları
///   (≤ 6M karakter, derinlik 64, ≤ 200k düğüm — <see cref="LimitedXmlReader"/>).</item>
/// <item><b>Süre:</b> özel bir <see cref="XsltExecutionGuard"/> (grid slotlarından ayrı): 16 MB yığınlı thread,
///   <see cref="DefaultTimeout"/>; iptal edilemeyen kaçak iş slotunu bitene dek tutar (en çok
///   <see cref="MaxConcurrent"/> eşzamanlı → süreç geneli etki üst sınırı).</item>
/// <item><b>Hata metni:</b> ham istisna mesajı yok — XPath sözdizimi (<see cref="XPathException"/>, yol/sınıf adı
///   içermez) ve sınır ihlali (kendi Türkçe metnimiz) dışında sabit mesaj.</item>
/// </list>
/// </summary>
public sealed class XPathEvaluator : IXPathEvaluator
{
    public const int MaxXmlChars = 6_000_000;
    public const int MaxInputDepth = 64;
    public const int MaxInputNodes = 200_000;
    public const int MaxConcurrent = 2;

    /// <summary>Yanıttaki en fazla öğe (büyük düğüm kümelerinde JSON şişmesin).</summary>
    public const int MaxItems = 1_000;

    public const string XmlInvalid = "XML_INVALID";
    public const string XmlLimit = "XML_LIMIT";
    public const string SyntaxError = "XPATH_SYNTAX";
    public const string EvaluationError = "XPATH_ERROR";
    public const string TimedOut = "XPATH_TIMEOUT";
    public const string Busy = "XPATH_BUSY";

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly XsltExecutionGuard _guard;
    private readonly TimeSpan _timeout;

    public XPathEvaluator()
        : this(new XsltExecutionGuard(new XsltGridOptions { MaxConcurrent = MaxConcurrent, QueueWaitMs = 1_000 },
            TimeProvider.System), DefaultTimeout)
    {
    }

    public XPathEvaluator(XsltExecutionGuard guard, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(guard);
        _guard = guard;
        _timeout = timeout;
    }

    /// <summary>Kapı reddi mi (<c>XPATH_TOO_LONG</c>/<c>XPATH_TOO_COMPLEX</c>) — Api 400 döner.</summary>
    public static bool IsRejection(string? code) => code is XPathPortability.TooLong or XPathPortability.TooComplex;

    public async Task<XPathEvaluateResponse> EvaluateAsync(string xpathExpression, string xmlContent, Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var gate = XPathPortability.Analyze(xpathExpression);
        if (IsRejection(gate.ErrorCode))
            return Error(sw, gate.Message!, gate.ErrorCode!);

        var ticket = new XsltGuardTicket(Hash(xpathExpression), userId, XsltEngineKind.DotNet);
        var result = await _guard.RunAsync(ticket, _timeout, ct => Run(xpathExpression, xmlContent, ct), cancellationToken)
            .ConfigureAwait(false);

        return result.Status switch
        {
            XsltGuardStatus.Completed => result.Value! with { ExecutionMs = sw.ElapsedMilliseconds },
            XsltGuardStatus.Busy => Error(sw, "XPath konsolu şu an meşgul, biraz sonra tekrar deneyin.", Busy),
            _ => Error(sw, $"XPath değerlendirmesi {_timeout.TotalSeconds:0} sn sınırını aştı.", TimedOut),
        };
    }

    private static XPathEvaluateResponse Run(string xpathExpression, string xmlContent, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        XPathNavigator nav;
        try
        {
            nav = Load(xmlContent).CreateNavigator();
        }
        catch (XsltProcessingException ex)
        {
            return Error(sw, ex.Diagnostic.Message, XmlLimit); // LimitedXmlReader: kendi metnimiz
        }
        catch (XmlException ex)
        {
            return Error(sw, $"XML okunamadı: iyi biçimli değil, DOCTYPE/DTD içeriyor ya da boyut sınırını aşıyor " +
                             $"(satır {ex.LineNumber}, sütun {ex.LinePosition}).", XmlInvalid);
        }

        try
        {
            var raw = nav.Evaluate(xpathExpression, CollectNamespaces(nav));
            return raw is XPathNodeIterator iter ? NodeSet(iter, sw, ct) : Atomic(raw, sw);
        }
        catch (XPathException ex)
        {
            return Error(sw, ex.Message, SyntaxError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            return Error(sw, "XPath değerlendirilemedi.", EvaluationError);
        }
    }

    private static XPathDocument Load(string xmlContent)
    {
        var settings = SecureXslt.ReaderSettings();
        settings.MaxCharactersInDocument = MaxXmlChars;
        using var reader = new LimitedXmlReader(XmlReader.Create(new StringReader(xmlContent), settings),
            MaxInputDepth, MaxInputNodes, XsltFailureKind.LimitExceeded, XsltEngineKind.DotNet);
        return new XPathDocument(reader);
    }

    /// <summary>Kök elemanın kapsamındaki önekler (navigator'ın NameTable'ı ile).</summary>
    private static XmlNamespaceManager CollectNamespaces(XPathNavigator nav)
    {
        var nsMgr = new XmlNamespaceManager(nav.NameTable);
        var nsNav = nav.Clone();
        nsNav.MoveToRoot();
        if (nsNav.MoveToFirstChild() && nsNav.MoveToFirstNamespace(XPathNamespaceScope.All))
        {
            do
            {
                var prefix = nsNav.Name;
                var uri = nsNav.Value;
                if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(uri))
                    nsMgr.AddNamespace(prefix, uri);
            } while (nsNav.MoveToNextNamespace(XPathNamespaceScope.All));
        }
        return nsMgr;
    }

    private static XPathEvaluateResponse NodeSet(XPathNodeIterator iter, Stopwatch sw, CancellationToken ct)
    {
        var items = new List<XPathResultItem>();
        while (items.Count < MaxItems && iter.MoveNext())
        {
            ct.ThrowIfCancellationRequested(); // süre aşımında işbirlikçi dur
            var cur = iter.Current!;
            var kind = cur.NodeType switch
            {
                XPathNodeType.Element => "element",
                XPathNodeType.Attribute => "attribute",
                XPathNodeType.Text => "text",
                XPathNodeType.Comment => "comment",
                XPathNodeType.ProcessingInstruction => "pi",
                _ => "node"
            };
            var name = string.IsNullOrEmpty(cur.Prefix) ? cur.LocalName : $"{cur.Prefix}:{cur.LocalName}";
            items.Add(new XPathResultItem(kind, name, Truncate(cur.Value), null, null));
        }

        return new XPathEvaluateResponse(items.Count == 0 ? "empty" : "node-set", items, sw.ElapsedMilliseconds, null);
    }

    private static XPathEvaluateResponse Atomic(object? raw, Stopwatch sw) =>
        new("atomic", [new XPathResultItem("atomic", "", Truncate(raw?.ToString() ?? ""), null, null)],
            sw.ElapsedMilliseconds, null);

    private static XPathEvaluateResponse Error(Stopwatch sw, string message, string code) =>
        new("error", [], sw.ElapsedMilliseconds, message, code);

    private static string Hash(string xpath) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xpath)))[..16];

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length > 4_000) value = value[..4_000]; // boşluk sıkıştırmadan önce: büyük alt ağaç metninde regex maliyeti
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.Length > 200 ? value[..200] + "…" : value;
    }
}
