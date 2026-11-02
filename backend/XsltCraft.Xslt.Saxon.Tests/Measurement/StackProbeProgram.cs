using System.Diagnostics;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Xslt.Saxon.Tests.Measurement;

/// <summary>
/// Test derlemesinin giriş noktası (<c>GenerateProgramFile=false</c>). xUnit/vstest bunu KULLANMAZ; yalnız
/// <see cref="StackBudgetMeasurementTests"/> alt süreç olarak <c>dotnet XsltCraft.Xslt.Saxon.Tests.dll --stack-probe …</c>
/// çalıştırır: yığın taşması süreci öldürür, ölçüm çıkış kodu + son "OK" satırıyla alınır.
/// Argümanlar: <c>--stack-probe &lt;template|function|parens|lre&gt; &lt;stackMb&gt; &lt;derinlik1,derinlik2,…&gt;</c>.
/// </summary>
public static class StackProbeProgram
{
    public const string Flag = "--stack-probe";

    public static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != Flag)
            return 0; // test SDK'nın varsayılan boş Main davranışı

        var kind = args[1];
        var stackMb = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
        var depths = args[3].Split(',').Select(s => int.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

        // Derinlik sınırı ölçüm için devre dışı (çok büyük); yığın gerçek sınırdır.
        var engine = new SaxonXsltEngine(SaxonEngineOptions.Isolated with { MaxTraceDepth = int.MaxValue });
        var exit = 0;
        var thread = new Thread(() => exit = Probe(engine, kind, depths), stackMb * 1024 * 1024);
        thread.Start();
        thread.Join();
        return exit;
    }

    private static int Probe(SaxonXsltEngine engine, string kind, int[] depths)
    {
        Console.WriteLine($"START {kind}");
        foreach (var depth in depths)
        {
            var xslt = StackProbeStylesheets.For(kind, depth);
            var limits = XsltExecutionLimits.Default with { MaxStylesheetDepth = int.MaxValue, Deadline = TimeSpan.FromMinutes(5) };
            var sw = Stopwatch.StartNew();
            try
            {
                var compiled = engine.Compile(xslt, limits);
                compiled.Transform("<r/>", TextWriter.Null, limits);
                Console.WriteLine($"OK {depth} {sw.ElapsedMilliseconds}");
            }
            catch (XsltProcessingException ex)
            {
                // Yakalanabilir hata (ör. Saxon'un kendi iç sınırı) — çökme değil.
                Console.WriteLine($"ERR {depth} {sw.ElapsedMilliseconds} {ex.Diagnostic.Kind}/{ex.Diagnostic.ErrorCode}");
                return 2;
            }

            Console.Out.Flush();
        }

        return 0;
    }
}

/// <summary>Derinlik ölçümü stylesheet'leri (özyineleme kuyruk çağrısı DEĞİL — Saxon TCO'su ölçümü bozmasın).</summary>
public static class StackProbeStylesheets
{
    private const string Head = """<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:f="urn:f" xmlns:xs="http://www.w3.org/2001/XMLSchema" exclude-result-prefixes="f xs">""";

    public static string For(string kind, int depth) => kind switch
    {
        "template" => Head + $"""
            <xsl:template match="/"><out><xsl:call-template name="r"><xsl:with-param name="n" select="0"/></xsl:call-template></out></xsl:template>
            <xsl:template name="r"><xsl:param name="n"/><xsl:if test="$n lt {depth}"><x><xsl:call-template name="r"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></x></xsl:if></xsl:template>
            </xsl:stylesheet>
            """,
        "function" => Head + $"""
            <xsl:function name="f:f" as="xs:integer"><xsl:param name="n" as="xs:integer"/><xsl:sequence select="if ($n ge {depth}) then 0 else 1 + f:f($n + 1)"/></xsl:function>
            <xsl:template match="/"><out><xsl:value-of select="f:f(0)"/></out></xsl:template>
            </xsl:stylesheet>
            """,
        "parens" => Head + "<xsl:template match=\"/\"><out><xsl:value-of select=\"" + new string('(', depth) + "1" + new string(')', depth) + "\"/></out></xsl:template></xsl:stylesheet>",
        "lre" => Head + "<xsl:template match=\"/\"><out>" + string.Concat(Enumerable.Repeat("<a>", depth)) + string.Concat(Enumerable.Repeat("</a>", depth)) + "</out></xsl:template></xsl:stylesheet>",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
