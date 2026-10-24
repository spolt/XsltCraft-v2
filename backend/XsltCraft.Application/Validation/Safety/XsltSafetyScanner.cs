using System.Xml;
using XsltCraft.Application.Xslt;

namespace XsltCraft.Application.Validation.Safety;

/// <summary>
/// Tamamen yasakçı (fail-closed, izin listesi) XSLT güvenlik tarayıcısı.
///
/// <para><b>Fail-closed parse:</b> içerik <see cref="SecureXslt.ReaderSettings"/> (DTD yasak,
/// <c>XmlResolver=null</c>) ile tam ayrıştırılır. Ayrıştırma hatası veya DOCTYPE → Security tehdidi.
/// Regex yedeği yoktur.</para>
///
/// <para><b>İzin listesi:</b> yalnız XSLT 2.0 komut kümesi, bilinen güvenli ad alanları ve fonksiyon
/// ad alanları serbesttir; tanınmayan her şey tehdittir. XPath, regex ile değil
/// <see cref="XPathTokenizer"/> ile (string literal / iç içe yorum farkında) incelenir.</para>
///
/// Durumsuzdur (pure); her çağrı bağımsızdır. Satır/kolon <c>IXmlLineInfo</c>'dan gelir.
/// </summary>
public sealed class XsltSafetyScanner
{
    public XsltSafetyReport Scan(string? xslt, XsltSafetyMode mode)
    {
        if (string.IsNullOrEmpty(xslt))
            return XsltSafetyReport.Clean;

        var findings = new List<XsltSafetyFinding>();
        var userFunctionNamespaces = XsltSafetyNamespaces.CollectUserFunctionNamespaces(xslt);

        try
        {
            using var reader = XmlReader.Create(new StringReader(xslt), SecureXslt.ReaderSettings());
            var lineInfo = reader as IXmlLineInfo;
            bool rootChecked = false;
            bool depthReported = false;

            while (reader.Read())
            {
                if (findings.Count >= XsltSafetyRules.MaxFindings)
                    break;

                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                // Depth 0 tabanlı (kök = 0) → seviye = Depth + 1; en fazla MaxElementDepth seviye serbest.
                if (reader.Depth + 1 > XsltSafetyRules.MaxElementDepth && !depthReported)
                {
                    depthReported = true;
                    Add(findings, "StylesheetDepthOver256", XsltSafetySeverity.Security,
                        $"Stylesheet eleman derinliği çok fazla (en fazla {XsltSafetyRules.MaxElementDepth} seviye).", lineInfo);
                }

                if (!rootChecked)
                {
                    rootChecked = true;
                    CheckRoot(reader, findings, lineInfo);
                }

                ScanElement(reader, findings, lineInfo, userFunctionNamespaces);
            }
        }
        catch (XmlException ex)
        {
            // Fail-closed: iyi biçimli olmayan XML veya DOCTYPE güvenli kabul edilmez.
            bool isDtd = ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
                         || ex.Message.Contains("DOCTYPE", StringComparison.OrdinalIgnoreCase);
            findings.Insert(0, new XsltSafetyFinding(
                isDtd ? "Doctype" : "MalformedXml",
                XsltSafetySeverity.Security,
                isDtd
                    ? "DOCTYPE / DTD içeren stylesheet reddedildi (XXE / varlık genişletme riski)."
                    : $"Stylesheet iyi biçimli XML değil: {ex.Message}",
                ex.LineNumber, ex.LinePosition));
        }

        var filtered = mode == XsltSafetyMode.Store
            ? findings.Where(f => f.Severity == XsltSafetySeverity.Security).ToList()
            : findings;

        return filtered.Count == 0 ? XsltSafetyReport.Clean : new XsltSafetyReport(filtered);
    }

    // ── Kök eleman kontrolü ──────────────────────────────────────────────────

    private static void CheckRoot(XmlReader reader, List<XsltSafetyFinding> findings, IXmlLineInfo? lineInfo)
    {
        bool isXslRoot = reader.NamespaceURI == XsltSafetyRules.Xsl
                         && (reader.LocalName == "stylesheet" || reader.LocalName == "transform");
        if (isXslRoot)
            return;

        // Basitleştirilmiş (LRE) stylesheet: xsl:version özniteliği olmalı.
        bool hasXslVersion = reader.GetAttribute("version", XsltSafetyRules.Xsl) != null;
        if (!hasXslVersion)
        {
            Add(findings, "NotAStylesheet", XsltSafetySeverity.Security,
                "Kök eleman xsl:stylesheet/xsl:transform değil ve xsl:version taşımıyor — geçerli bir stylesheet değil.",
                lineInfo);
        }
    }

    // ── Eleman taraması ──────────────────────────────────────────────────────

    private void ScanElement(
        XmlReader reader,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        string ns = reader.NamespaceURI;
        string local = reader.LocalName;
        bool isXslElement = ns == XsltSafetyRules.Xsl;

        if (isXslElement)
        {
            if (XsltSafetyRules.ForbiddenXslElements.Contains(local))
                Add(findings, "ForbiddenXslElement", XsltSafetySeverity.Security,
                    $"Yasak XSLT elemanı: xsl:{local}.", lineInfo);
            else if (XsltSafetyRules.UnsupportedXslElements.Contains(local))
                Add(findings, "UnsupportedXslElement", XsltSafetySeverity.Unsupported,
                    $"Desteklenmeyen (XSLT 2.0 üstü) eleman: xsl:{local}.", lineInfo);
            else if (!XsltSafetyRules.AllowedXslElements.Contains(local))
                Add(findings, "UnknownXslElement", XsltSafetySeverity.Security,
                    $"Bilinmeyen/izinsiz XSLT elemanı: xsl:{local}.", lineInfo);
        }
        else if (XsltSafetyNamespaces.IsForbidden(ns))
        {
            Add(findings, "ForbiddenNamespace", XsltSafetySeverity.Security,
                $"Yasak ad alanında eleman: {{{ns}}}{local}.", lineInfo);
        }
        else if (ns == XsltSafetyRules.Msxsl && local == "script")
        {
            Add(findings, "MsxslScript", XsltSafetySeverity.Security,
                "msxsl:script (gömülü betik) yasak.", lineInfo);
        }
        else if (ns == XsltSafetyRules.XInclude)
        {
            Add(findings, "XInclude", XsltSafetySeverity.Security,
                "xi:include (XInclude) yasak — dış kaynak çekme.", lineInfo);
        }

        if (!reader.HasAttributes)
            return;

        var resolver = (IXmlNamespaceResolver)reader;
        for (int a = 0; a < reader.AttributeCount; a++)
        {
            reader.MoveToAttribute(a);
            ScanAttribute(reader, isXslElement, local, resolver, findings, lineInfo, userFunctionNamespaces);
        }
        reader.MoveToElement();
    }

    private void ScanAttribute(
        XmlReader reader,
        bool ownerIsXsl,
        string ownerLocal,
        IXmlNamespaceResolver resolver,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        string ans = reader.NamespaceURI;
        string alocal = reader.LocalName;

        // Ad alanı bildirimleri (xmlns, xmlns:x) — serbest. Yalnız ad alanı URI'sine bakılır:
        // başka ad alanındaki "p:xmlns" sıradan (AVT) özniteliktir ve taranmalıdır.
        if (ans == XsltSafetyRules.XmlnsNs)
            return;

        string value = reader.Value;
        bool inNoNs = string.IsNullOrEmpty(ans);

        // XSLT denetim özniteliği: xsl: elemanında ad alanısız ya da herhangi bir elemanda xsl: ad alanlı.
        // (xsl: elemanında xsl: ad alanlı öznitelik XTSE0090'dır; fail-closed olarak yine denetlenir.)
        bool xslControlAttr = (ownerIsXsl && inNoNs) || ans == XsltSafetyRules.Xsl;

        // Gölge öznitelik (XSLT 3.0 §3.13.2): xsl: elemanında "_ad" veya LRE'de "xsl:_ad" → derleme
        // anında değerlendirilen statik AVT; kaçış vektörü. LRE üzerindeki ad alanısız "_ad" gölge DEĞİL,
        // sıradan çıktı özniteliğidir (aşağıda AVT olarak taranır).
        if (xslControlAttr && alocal.Length > 0 && alocal[0] == '_')
        {
            Add(findings, "ShadowAttribute", XsltSafetySeverity.Security,
                $"Gölge (shadow) öznitelik yasak: {alocal}.", lineInfo);
            return;
        }

        // Yasak ad alanında öznitelik (saxon:/java:/clitype:).
        if (XsltSafetyNamespaces.IsForbidden(ans))
        {
            Add(findings, "ForbiddenNamespace", XsltSafetySeverity.Security,
                $"Yasak ad alanında öznitelik: {{{ans}}}{alocal}.", lineInfo);
            return;
        }

        if (xslControlAttr)
        {
            switch (alocal)
            {
                case "use-when":
                    Add(findings, "UseWhen", XsltSafetySeverity.Security,
                        "use-when (statik derleme ifadesi) yasak — derleme anında doc()/unparsed-text() çalıştırabilir.", lineInfo);
                    return;
                case "extension-element-prefixes":
                    Add(findings, "ExtensionElementPrefixes", XsltSafetySeverity.Security,
                        "extension-element-prefixes yasak — uzantı komutlarını etkinleştirir.", lineInfo);
                    return;
                case "static" when !IsExplicitlyOff(value):
                    Add(findings, "StaticVariable", XsltSafetySeverity.Security,
                        "static (statik değişken/parametre) yalnız açıkça \"no\" ile serbest — derleme anında yürütülür.", lineInfo);
                    return;
                case "expand-text" when !IsExplicitlyOff(value):
                    Add(findings, "ExpandText", XsltSafetySeverity.Security,
                        "expand-text yalnız açıkça \"no\" ile serbest — metin düğümlerini ifadeye çevirir (tarama atlatma).", lineInfo);
                    return;
                case "version" when !ownerIsXsl || ownerLocal is "stylesheet" or "transform":
                    // xsl:stylesheet/xsl:transform (no-ns) veya LRE kökü (xsl:version) = XSLT sürümü.
                    // xsl:output@version HTML çıktı sürümüdür → bu case'e girmez, AVT olarak taranır.
                    CheckVersion(value, findings, lineInfo);
                    return;
                case "collation":
                case "default-collation":
                    // Literal kontrolünden sonra değer AVT olarak da taranır (return YOK).
                    CheckCollation(value, findings, lineInfo);
                    break;
                case "name" when ownerIsXsl && inNoNs && ownerLocal == "function":
                    CheckFunctionName(value, resolver, findings, lineInfo);
                    break;
            }
        }

        // XPath taşıyan öznitelikler yalnız xsl: elemanlarında ve ad alanısız.
        if (ownerIsXsl && inNoNs && XsltSafetyRules.XPathAttributes.Contains(alocal))
            AnalyzeExpression(value, resolver, findings, lineInfo, userFunctionNamespaces);
        else
            ScanAvt(value, resolver, findings, lineInfo, userFunctionNamespaces);
    }

    // ── AVT ({ } gömülü ifade) taraması ───────────────────────────────────────

    private void ScanAvt(
        string value,
        IXmlNamespaceResolver resolver,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        int i = 0, n = value.Length;
        while (i < n)
        {
            char c = value[i];
            if (c == '{')
            {
                if (i + 1 < n && value[i + 1] == '{') { i += 2; continue; } // {{ kaçışı
                // Kapanış '}' string literal / (iç içe) yorum / Q{…} içindeyse ifadeyi bitirmez.
                int j = XPathLexer.FindEnclosedExpressionEnd(value, i + 1, out bool malformed);
                if (malformed)
                    Add(findings, "MalformedAvt", XsltSafetySeverity.Security,
                        "AVT ifadesi kapanmıyor ya da sınırı belirsiz (kapanmamış '{', string literal veya yorum) — fail-closed.",
                        lineInfo);
                AnalyzeExpression(value.Substring(i + 1, j - (i + 1)), resolver, findings, lineInfo, userFunctionNamespaces);
                i = j < n ? j + 1 : j;
                continue;
            }
            if (c == '}' && i + 1 < n && value[i + 1] == '}') { i += 2; continue; } // }} kaçışı
            i++;
        }
    }

    // ── XPath ifade analizi ────────────────────────────────────────────────────

    private void AnalyzeExpression(
        string expr,
        IXmlNamespaceResolver resolver,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        if (string.IsNullOrEmpty(expr))
            return;

        if (expr.Length > XsltSafetyRules.MaxExpressionLength)
            Add(findings, "ExpressionOver8KB", XsltSafetySeverity.Security,
                $"Tek XPath ifadesi çok uzun (>{XsltSafetyRules.MaxExpressionLength} bayt).", lineInfo);

        var tokens = XPathTokenizer.Tokenize(expr, out bool malformed);
        if (malformed)
            Add(findings, "MalformedXPath", XsltSafetySeverity.Security,
                "XPath ifadesinde kapanmamış string literal / yorum ya da belirsiz Q{…} — fail-closed.", lineInfo);

        int parenDepth = 0, maxParen = 0;

        for (int k = 0; k < tokens.Count; k++)
        {
            var t = tokens[k];
            switch (t.Kind)
            {
                case XPathTokenKind.LParen:
                    parenDepth++;
                    if (parenDepth > maxParen) maxParen = parenDepth;
                    break;
                case XPathTokenKind.RParen:
                    if (parenDepth > 0) parenDepth--;
                    break;
                case XPathTokenKind.String:
                    CheckStringLiteral(t.Text, findings, lineInfo);
                    break;
                case XPathTokenKind.Var:
                    if (Next(tokens, k).Kind == XPathTokenKind.LParen)
                        Add(findings, "DynamicFunctionCall", XsltSafetySeverity.Security,
                            "Dinamik fonksiyon çağrısı ($değişken(...)) yasak.", lineInfo);
                    break;
                case XPathTokenKind.Name:
                    {
                        var next = Next(tokens, k);
                        if (next.Kind == XPathTokenKind.Hash)
                        {
                            Add(findings, "NamedFunctionReference", XsltSafetySeverity.Security,
                                $"İsimli fonksiyon referansı (ad#N) yasak: {t.Text}#…", lineInfo);
                            break;
                        }
                        if (next.Kind == XPathTokenKind.LParen)
                        {
                            if (t.Text == "function")
                            {
                                Add(findings, "InlineFunction", XsltSafetySeverity.Security,
                                    "Satır içi fonksiyon (function(...){...}) yasak.", lineInfo);
                                break;
                            }
                            ClassifyFunctionCall(t.Text, resolver, tokens, k, findings, lineInfo, userFunctionNamespaces);
                        }
                        break;
                    }
                case XPathTokenKind.EqName:
                    {
                        var next = Next(tokens, k);
                        if (next.Kind == XPathTokenKind.Hash)
                        {
                            Add(findings, "NamedFunctionReference", XsltSafetySeverity.Security,
                                $"İsimli fonksiyon referansı (ad#N) yasak: Q{{{t.Uri}}}{t.Text}#…", lineInfo);
                            break;
                        }
                        if (next.Kind == XPathTokenKind.LParen)
                            ClassifyFunction(t.Uri ?? "", t.Text, resolver, tokens, k, findings, lineInfo, userFunctionNamespaces);
                        break;
                    }
            }
        }

        if (maxParen > XsltSafetyRules.MaxParenDepth)
            Add(findings, "NestedParensOver64", XsltSafetySeverity.Security,
                $"İç içe parantez derinliği çok fazla (>{XsltSafetyRules.MaxParenDepth}).", lineInfo);
    }

    private void ClassifyFunctionCall(
        string qname,
        IXmlNamespaceResolver resolver,
        List<XPathToken> tokens,
        int k,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        string prefix, local;
        int colon = qname.IndexOf(':');
        if (colon >= 0) { prefix = qname[..colon]; local = qname[(colon + 1)..]; }
        else { prefix = ""; local = qname; }

        string? ns = prefix.Length == 0
            ? XsltSafetyRules.Fn                // öneksiz → varsayılan fonksiyon ad alanı (fn)
            : resolver.LookupNamespace(prefix);

        if (ns == null)
        {
            Add(findings, "UnresolvedPrefix", XsltSafetySeverity.Security,
                $"Çözülemeyen önek içeren fonksiyon çağrısı: {qname}().", lineInfo);
            return;
        }

        ClassifyFunction(ns, local, resolver, tokens, k, findings, lineInfo, userFunctionNamespaces);
    }

    /// <remarks>
    /// Sıra güvenlik açısından önemlidir: yasak / ayrılmış ad alanı kontrolleri, kullanıcı-fonksiyon
    /// izin kümesinden ÖNCE yapılır — xsl:function ile yasak bir ad alanı "beyaz listeye" sokulamaz.
    /// </remarks>
    private void ClassifyFunction(
        string ns,
        string local,
        IXmlNamespaceResolver resolver,
        List<XPathToken> tokens,
        int k,
        List<XsltSafetyFinding> findings,
        IXmlLineInfo? lineInfo,
        HashSet<string> userFunctionNamespaces)
    {
        if (ns == XsltSafetyRules.Fn)
        {
            if (local == "system-property")
            {
                if (!IsSafeSystemPropertyCall(tokens, k, resolver))
                    Add(findings, "SystemProperty", XsltSafetySeverity.Security,
                        "system-property() yalnız tek 'xsl:*' / 'msxsl:*' sabit argümanıyla serbest.", lineInfo);
                return;
            }
            if (XsltSafetyRules.ForbiddenFnFunctions.Contains(local))
                Add(findings, "ForbiddenFunction", XsltSafetySeverity.Security,
                    $"Güvenli olmayan XPath fonksiyonu: {local}()", lineInfo);
            return; // diğer fn fonksiyonları serbest
        }

        if (ns == XsltSafetyRules.Xs)
            return; // xs: tip oluşturucular serbest

        if (XsltSafetyNamespaces.IsForbidden(ns))
        {
            Add(findings, "ForbiddenFunctionNamespace", XsltSafetySeverity.Security,
                $"Yasak ad alanında fonksiyon çağrısı: {{{ns}}}{local}().", lineInfo);
            return;
        }

        if (ns == XsltSafetyRules.MathFn || ns == XsltSafetyRules.MapFn || ns == XsltSafetyRules.ArrayFn)
        {
            Add(findings, "UnsupportedFunctionNamespace", XsltSafetySeverity.Unsupported,
                $"Desteklenmeyen (XSLT 2.0 üstü) fonksiyon ad alanı: {{{ns}}}{local}().", lineInfo);
            return;
        }

        if ((ns == XsltSafetyRules.Msxsl && XsltSafetyRules.AllowedMsxslFunctions.Contains(local))
            || (ns == XsltSafetyRules.Exsl && XsltSafetyRules.AllowedExslFunctions.Contains(local)))
            return; // yan etkisiz .NET / EXSLT yerleşikleri

        if (userFunctionNamespaces.Contains(ns))
            return; // stylesheet'te tanımlı xsl:function (küme yasak/ayrılmış ad alanı içermez)

        // İzin listesinde olmayan her ad alanı → fail-closed tehdit.
        Add(findings, "ForbiddenFunctionNamespace", XsltSafetySeverity.Security,
            $"İzinsiz ad alanında fonksiyon çağrısı: {{{ns}}}{local}().", lineInfo);
    }

    private static void CheckStringLiteral(string value, List<XsltSafetyFinding> findings, IXmlLineInfo? lineInfo)
    {
        if (value.Contains("saxon.sf.net", StringComparison.OrdinalIgnoreCase))
            Add(findings, "SaxonLiteral", XsltSafetySeverity.Security,
                "String literal içinde saxon.sf.net — Saxon uzantı/collation URI'si yasak.", lineInfo);
        else if (value.Contains('%') && value.Contains("collation", StringComparison.OrdinalIgnoreCase))
            Add(findings, "UnsafeCollation", XsltSafetySeverity.Security,
                "Collation URI'sinde % kaçışı yasak (NoSuchMethodError / sınıf yükleme riski).", lineInfo);
    }

    /// <summary>
    /// Collation özniteliği: AVT ifadesi içeren (dinamik) değer reddedilir — çalışma anında kurulan URI
    /// (ör. <c>concat('http://saxon', '.sf.net/collation?class=…')</c>) statik olarak doğrulanamaz.
    /// Statik değerde saxon.sf.net / <c>%</c> yasak. Çağıran değeri ayrıca AVT olarak tarar.
    /// </summary>
    private static void CheckCollation(string value, List<XsltSafetyFinding> findings, IXmlLineInfo? lineInfo)
    {
        if (HasAvtExpression(value))
        {
            Add(findings, "UnsafeCollation", XsltSafetySeverity.Security,
                "Dinamik (AVT ifadeli) collation yasak — collation URI'si statik olmalı.", lineInfo);
            return;
        }
        if (value.Contains("saxon.sf.net", StringComparison.OrdinalIgnoreCase)
            || value.Contains('%'))
            Add(findings, "UnsafeCollation", XsltSafetySeverity.Security,
                "Güvenli olmayan collation URI'si (yalnız codepoint / %'siz UCA serbest).", lineInfo);
    }

    /// <summary>xsl:function/@name yasak veya ayrılmış ad alanında olamaz (bkz. XTSE0740).</summary>
    private static void CheckFunctionName(
        string value, IXmlNamespaceResolver resolver, List<XsltSafetyFinding> findings, IXmlLineInfo? lineInfo)
    {
        var ns = XsltSafetyNamespaces.ResolveFunctionNamespace(value, resolver.LookupNamespace);
        if (ns == null)
            return;
        if (XsltSafetyNamespaces.IsForbidden(ns))
            Add(findings, "ForbiddenNamespace", XsltSafetySeverity.Security,
                $"Yasak ad alanında xsl:function tanımı: {{{ns}}}.", lineInfo);
        else if (XsltSafetyNamespaces.IsReserved(ns))
            Add(findings, "ReservedFunctionNamespace", XsltSafetySeverity.Security,
                $"Ayrılmış ad alanında xsl:function tanımı yasak: {{{ns}}}.", lineInfo);
    }

    private static void CheckVersion(string value, List<XsltSafetyFinding> findings, IXmlLineInfo? lineInfo)
    {
        if (!XsltVersionNumber.TryParse(value, out var v))
            Add(findings, "InvalidVersion", XsltSafetySeverity.Unsupported,
                $"XSLT sürümü ayrıştırılamadı: '{value}'.", lineInfo);
        else if (v > 2.0m)
            Add(findings, "VersionAbove20", XsltSafetySeverity.Unsupported,
                $"XSLT sürümü {value} desteklenmiyor (tavan 2.0).", lineInfo);
    }

    // ── Yardımcılar ─────────────────────────────────────────────────────────

    /// <summary>
    /// system-property() yalnız tam olarak tek string literal argümanla ve argüman QName'inin öneki
    /// XSLT ya da msxsl ad alanına çözülüyorsa serbesttir (ör. <c>'xsl:vendor'</c>, <c>'msxsl:version'</c>).
    /// Dinamik argüman, boşluklu/öneksiz ad veya başka ad alanı (Java sistem özellikleri) → engel.
    /// </summary>
    private static bool IsSafeSystemPropertyCall(List<XPathToken> tokens, int k, IXmlNamespaceResolver resolver)
    {
        // k: ad, k+1: '(', k+2: argüman, k+3: ')'
        if (k + 3 >= tokens.Count
            || tokens[k + 2].Kind != XPathTokenKind.String
            || tokens[k + 3].Kind != XPathTokenKind.RParen)
            return false;

        string arg = tokens[k + 2].Text;
        int colon = arg.IndexOf(':');
        if (colon <= 0 || colon == arg.Length - 1 || arg.IndexOf(':', colon + 1) >= 0)
            return false;
        foreach (char c in arg)
            if (c != ':' && !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
                return false;

        string prefix = arg[..colon];
        string? ns = resolver.LookupNamespace(prefix);
        // Bildirilmemiş 'xsl'/'msxsl' öneki: işlemci çalışma anında hata verir (XTDE1390) — zararsız.
        // Önek başka bir ad alanına bağlıysa (ör. xmlns:xsl="urn:x") serbest DEĞİL.
        return ns == null
            ? prefix is "xsl" or "msxsl"
            : ns == XsltSafetyRules.Xsl || ns == XsltSafetyRules.Msxsl;
    }

    /// <summary>Değer <c>{{</c> kaçışı dışında bir <c>{</c> (AVT ifadesi) içeriyor mu?</summary>
    private static bool HasAvtExpression(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] != '{')
                continue;
            if (i + 1 < value.Length && value[i + 1] == '{') { i++; continue; }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Boolean XSLT özniteliği için fail-closed değerlendirme: XML boşlukları kırpılmış değer
    /// (büyük/küçük harf duyarsız) <c>no</c>/<c>false</c>/<c>0</c> ise "kapalı"; boş, boşluklu,
    /// tanınmayan dahil diğer her değer "açık" sayılır.
    /// </summary>
    private static bool IsExplicitlyOff(string value)
    {
        var v = value.Trim(XsltSafetyRules.XmlWhitespace);
        return v.Equals("no", StringComparison.OrdinalIgnoreCase)
               || v.Equals("false", StringComparison.OrdinalIgnoreCase)
               || v == "0";
    }

    private static XPathToken Next(List<XPathToken> tokens, int k) =>
        k + 1 < tokens.Count ? tokens[k + 1] : default;

    private static void Add(List<XsltSafetyFinding> findings, string code, XsltSafetySeverity severity, string message, IXmlLineInfo? lineInfo)
    {
        if (findings.Count >= XsltSafetyRules.MaxFindings)
            return;
        int line = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : 0;
        int col = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 0;
        findings.Add(new XsltSafetyFinding(code, severity, message, line, col));
    }
}
