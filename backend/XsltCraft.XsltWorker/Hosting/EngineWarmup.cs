using XsltCraft.Application.Validation;
using XsltCraft.Application.Validation.Safety;
using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.XsltWorker.Hosting;

/// <summary>
/// Hello'dan ÖNCE motor thread'inde koşan ısınma: Java akışlarını yönlendirir, motorları kurar, küçük bir
/// XSLT 2.0 Saxon derleme+render ve XSLT 1.0 .NET render yapar (IKVM sınıf yükleme + JIT, tarayıcı JIT'i).
/// Havuz Hello'yu Ready sayar → Ready = ısınmış worker; ilk kullanıcı isteği soğuk açılışı (~0.7–1.5 sn)
/// validate'in 5 sn bütçesinden yemez. Herhangi bir adım başarısızsa (motor yüklenemedi, beklenen çıktı yok)
/// istisna fırlar; Program Hello göndermeden <see cref="WorkerExitCodes.WarmupFailed"/> ile çıkar.
/// </summary>
internal static class EngineWarmup
{
    private const string SaxonWarmupXslt = """
        <xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:w="urn:xsltcraft:warmup" exclude-result-prefixes="xs w">
          <xsl:output method="html" encoding="UTF-8" indent="no"/>
          <xsl:function name="w:twice" as="xs:integer">
            <xsl:param name="n" as="xs:integer"/>
            <xsl:sequence select="$n * 2"/>
          </xsl:function>
          <xsl:template match="/">
            <p><xsl:for-each-group select="r/i" group-by="@g"><xsl:value-of select="concat(current-grouping-key(), '=', w:twice(count(current-group())), ';')"/></xsl:for-each-group></p>
          </xsl:template>
        </xsl:stylesheet>
        """;

    private const string DotNetWarmupXslt = """
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
          <xsl:output method="html" encoding="UTF-8" indent="no"/>
          <xsl:template match="/"><p><xsl:value-of select="count(r/i)"/></p></xsl:template>
        </xsl:stylesheet>
        """;

    private const string WarmupXml = """<r><i g="a"/><i g="a"/><i g="b"/></r>""";

    private static readonly XsltExecutionLimits Limits = XsltExecutionLimits.Default with
    {
        Deadline = TimeSpan.FromSeconds(30),
    };

    /// <summary>Motorları kurar ve ısıtır. Yalnız motor thread'inde çağrılır.</summary>
    public static WorkerEngineSet CreateAndWarm(Stream stderr)
    {
        JavaStdio.Redirect(stderr);

        var engines = WorkerEngineSet.Create();

        // Tarayıcı da istek yolundadır: JIT'ini burada öde. Sonuç önemsizdir (bulgu ısınmayı durdurmaz).
        _ = XsltSafety.Scan(SaxonWarmupXslt, XsltSafetyMode.Execute);

        Expect(Render(engines.Saxon, SaxonWarmupXslt), "a=4;b=2;", "Saxon");
        Expect(Render(engines.DotNet, DotNetWarmupXslt), "<p>3</p>", ".NET");
        return engines;
    }

    private static string Render(IXsltEngine engine, string xslt)
    {
        var compiled = engine.Compile(xslt, Limits);
        var output = new StringWriter();
        compiled.Transform(WarmupXml, output, Limits);
        return output.ToString();
    }

    private static void Expect(string output, string marker, string engine)
    {
        if (!output.Contains(marker, StringComparison.Ordinal))
            throw new InvalidOperationException($"{engine} ısınma çıktısı beklenen değeri içermiyor.");
    }
}
