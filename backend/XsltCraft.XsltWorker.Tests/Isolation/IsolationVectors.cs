using System.Security;
using static XsltCraft.XsltWorker.Tests.Engine.EngineTestKit;

namespace XsltCraft.XsltWorker.Tests.Isolation;

/// <summary>
/// G0 security-reviewer K4 DoS matrisi (scratchpad g0probe/Crash.cs) — XsltSafety taramasız (<c>render-unscreened</c>)
/// gerçek worker'a verilen stylesheet'ler. Tarayıcı bunların çoğunu zaten reddeder; burada motor + süreç sınırı ölçülür.
/// </summary>
internal static class IsolationVectors
{
    private const string Head = $"""<xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}" xmlns:f="urn:f" xmlns:xs="http://www.w3.org/2001/XMLSchema" exclude-result-prefixes="f xs"><xsl:output method="xml" omit-xml-declaration="yes"/>""";

    private const string Head30 = $"""<xsl:stylesheet version="3.0" xmlns:xsl="{XslNs}" xmlns:f="urn:f" xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:map="http://www.w3.org/2005/xpath-functions/map" exclude-result-prefixes="f xs map"><xsl:output method="xml" omit-xml-declaration="yes"/>""";

    /// <summary>Satır içi fonksiyon özyinelemesi (enter() üretmez; 64 MB'ta bile çökerdi — G0).</summary>
    public const string InlineFunctionRecursion = Head + """
        <xsl:template match="/"><out><xsl:value-of select="let $f := function($g, $n) { 1 + $g($g, $n + 1) } return $f($f, 0)"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>use-when içinde satır içi özyineleme (DERLEME anı).</summary>
    public const string UseWhenInlineRecursion = Head + """
        <xsl:template match="/" use-when="let $f := function($g, $n) { 1 + $g($g, $n + 1) } return $f($f, 0) gt 0"><out/></xsl:template>
        <xsl:template match="/"><out/></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>static parametrede satır içi özyineleme (DERLEME anı).</summary>
    public const string StaticParamInlineRecursion = Head30 + """
        <xsl:param name="p" static="yes" select="let $f := function($g, $n) { 1 + $g($g, $n + 1) } return $f($f, 0)"/>
        <xsl:template match="/"><out/></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>Gölge öznitelik: <c>_select</c> derleme anında string birleştirmeyle satır içi fn kurar (taramada "function(" görünmez).</summary>
    public const string ShadowSelectInline = Head30 + """
        <xsl:template match="/"><out><xsl:value-of _select="{concat('let $f := func', 'tion($g, $n) ', codepoints-to-string(123), ' 1 + $g($g, $n + 1) ', codepoints-to-string(125), ' return $f($f, 0)')}"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>xsl:catch dalı özyinelemeyi sürdürür; sayaç sıfırlanırsa dakikalarca CPU + çökme (G0 K-1).</summary>
    public const string TryCatchRecursion = Head30 + """
        <xsl:template match="/"><out><xsl:call-template name="r"/></out></xsl:template>
        <xsl:template name="r"><xsl:param name="n" select="0"/>
          <xsl:try>
            <x><xsl:call-template name="r"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></x>
            <xsl:catch><caught/><xsl:call-template name="r"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></xsl:catch>
          </xsl:try>
        </xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>Durdurulamaz XPath CPU döngüsü (enter() yok → işbirlikçi süre devreye giremez; yalnız kill).</summary>
    public const string CpuLoop = Head + """
        <xsl:template match="/"><out><xsl:value-of select="sum(for $i in 1 to 2000000000 return $i mod 7)"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>ReDoS (REGEX_BACKTRACKING_LIMIT).</summary>
    public const string Redos = Head + """
        <xsl:template match="/"><out><xsl:value-of select="matches(concat(string-join(for $i in 1 to 30 return 'a', ''), '!'), '^(a+)+$')"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>transform(stylesheet-text) iç stylesheet'inde sonsuz template özyinelemesi (iç transform trace'siz).</summary>
    public static string TransformInnerRecursion
    {
        get
        {
            var inner = $"<xsl:stylesheet version=\"2.0\" xmlns:xsl=\"{XslNs}\"><xsl:template match=\"/\"><xsl:call-template name=\"r\"/></xsl:template><xsl:template name=\"r\"><x><xsl:call-template name=\"r\"/></x></xsl:template></xsl:stylesheet>";
            var escaped = SecurityElement.Escape(inner).Replace("&apos;", "''", StringComparison.Ordinal);
            return Head30 + $"<xsl:template match=\"/\"><out><xsl:copy-of select=\"map:get(transform(map:merge((map:entry('stylesheet-text', '{escaped}'), map:entry('source-node', /)))), 'output')\"/></out></xsl:template></xsl:stylesheet>";
        }
    }

    /// <summary>Bellek ikiye katlama: n adımda 2^n karakter (n=40 → ~1T; GC tavanında OOM ya da bekçi kill).</summary>
    public static string MemoryDoubling(int steps) => Head + $"""
        <xsl:template match="/"><out><xsl:call-template name="d"><xsl:with-param name="s" select="'x'"/><xsl:with-param name="n" select="{steps}"/></xsl:call-template></out></xsl:template>
        <xsl:template name="d"><xsl:param name="s"/><xsl:param name="n"/>
          <xsl:choose>
            <xsl:when test="$n = 0"><xsl:value-of select="string-length($s)"/></xsl:when>
            <xsl:otherwise><xsl:call-template name="d"><xsl:with-param name="s" select="concat($s, $s)"/><xsl:with-param name="n" select="$n - 1"/></xsl:call-template></xsl:otherwise>
          </xsl:choose>
        </xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>Derleme anı derin iç içe literal sonuç elemanı.</summary>
    public static string NestedLre(int depth) =>
        Head + "<xsl:template match=\"/\"><out>" + string.Concat(Enumerable.Repeat("<a>", depth)) + string.Concat(Enumerable.Repeat("</a>", depth)) + "</out></xsl:template></xsl:stylesheet>";

    /// <summary>Derleme anı derin iç içe parantez (XPath ayrıştırıcısı özyinelemeli).</summary>
    public static string NestedParens(int depth) =>
        Head + "<xsl:template match=\"/\"><out><xsl:value-of select=\"" + new string('(', depth) + "1" + new string(')', depth) + "\"/></out></xsl:template></xsl:stylesheet>";

    /// <summary>Sağlık kontrolü stylesheet'i.</summary>
    public static string Echo => Stylesheet("<xsl:value-of select=\"/r\"/>");
}
