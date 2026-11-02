using System.Diagnostics;
using System.Reflection;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Xslt.Saxon.Limits;
using XsltCraft.Xslt.Saxon.Tests.Support;
using static XsltCraft.Xslt.Saxon.Tests.Support.SaxonTestKit;

namespace XsltCraft.Xslt.Saxon.Tests;

/// <summary>
/// G0 K-1 / SR2: derinlik sınırı yapışkan, xsl:catch ile yakalanamaz/sıfırlanamaz, transform başına; deadline
/// template döngüsünü durdurur. xsl:try kaynak tarayıcıda (XsltSafety) yasaktır — burada tarayıcı BİLEREK
/// atlanır, motor katmanı tek başına sınanır.
/// </summary>
public sealed class DepthLimitTests
{
    private const string Head = $"""<xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}" xmlns:f="urn:f" xmlns:xs="http://www.w3.org/2001/XMLSchema" exclude-result-prefixes="f xs"><xsl:output omit-xml-declaration="yes"/>""";

    private const string InfiniteTemplate = Head + """
        <xsl:template match="/"><out><xsl:call-template name="r"/></out></xsl:template>
        <xsl:template name="r"><x><xsl:call-template name="r"/></x></xsl:template>
        </xsl:stylesheet>
        """;

    private const string InfiniteFunction = Head + """
        <xsl:function name="f:f" as="xs:integer"><xsl:param name="n" as="xs:integer"/>
          <xsl:sequence select="1 + f:f($n + 1)"/></xsl:function>
        <xsl:template match="/"><out><xsl:value-of select="f:f(0)"/></out></xsl:template>
        </xsl:stylesheet>
        """;

    // Spike/G0: catch dalı özyinelemeyi sürdürür; sayaç catch'te sıfırlanırsa süreç dakikalarca CPU yakıp çöker.
    private const string TryCatchRecursion = Head + """
        <xsl:template match="/"><out><xsl:call-template name="r"/></out></xsl:template>
        <xsl:template name="r"><xsl:param name="n" select="0"/>
          <xsl:try>
            <x><xsl:call-template name="r"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></x>
            <xsl:catch><caught/><xsl:call-template name="r"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></xsl:catch>
          </xsl:try>
        </xsl:template>
        </xsl:stylesheet>
        """;

    [Theory]
    [InlineData(InfiniteTemplate)]
    [InlineData(InfiniteFunction)]
    public void InfiniteRecursion_LimitExceeded_NotCrash(string xslt)
    {
        var d = OnBigStack(() => Fail(xslt));
        Assert.Equal(XsltFailureKind.LimitExceeded, d.Kind);
        Assert.Equal(LimitCodes.Depth, d.ErrorCode);
    }

    [Theory]
    [InlineData(InfiniteTemplate)]
    [InlineData(InfiniteFunction)]
    public void GridProfile_DepthLimit_HoldsOn16MbStack(string xslt)
    {
        // InProcessGrid: Api guard thread'i 16 MB (ADR 008 §7); sınır 256 bu yığında yakalanabilir hata üretmeli.
        var d = OnBigStack(() => Fail(xslt, engine: GridEngine), stackMb: 16);
        Assert.Equal(LimitCodes.Depth, d.ErrorCode);
    }

    [Fact]
    public void Limit_IsSticky_AfterFirstTrip()
    {
        var compiled = (SaxonCompiledXslt)Engine.Compile(InfiniteTemplate, XsltExecutionLimits.Default);
        SaxonTransformProbe? probe = null;

        OnBigStack(() => Assert.Throws<XsltProcessingException>(() =>
            compiled.Transform("<r/>", new StringWriter(), XsltExecutionLimits.Default, p => probe = p)));

        Assert.NotNull(probe);
        Assert.NotNull(probe!.Tripped);
        Assert.Equal(LimitCodes.Depth, probe.Tripped!.Code);
        Assert.Equal(Engine.Options.MaxTraceDepth + 1, probe.MaxTraceDepthSeen);

        // Bütçe düzeyinde: ihlal kaydedildikten sonra her kontrol aynı hatayı yeniden fırlatır.
        var budget = new ExecutionBudget(TimeSpan.Zero, CancellationToken.None);
        var listener = new DepthLimitingTraceListener(1, budget);
        listener.enter(null!, null!, null!);
        var first = Assert.Throws<SaxonLimitError>(() => listener.enter(null!, null!, null!));
        listener.leave(null!);
        listener.leave(null!);
        var again = Assert.Throws<SaxonLimitError>(() => listener.enter(null!, null!, null!));
        Assert.Same(first, again);
    }

    [Fact]
    public void TryCatch_CannotCatchOrResetLimit()
    {
        var compiled = (SaxonCompiledXslt)Engine.Compile(TryCatchRecursion, XsltExecutionLimits.Default);
        SaxonTransformProbe? probe = null;
        var output = new StringWriter();
        var sw = Stopwatch.StartNew();

        var ex = OnBigStack(() => Assert.Throws<XsltProcessingException>(() =>
            compiled.Transform("<r/>", output, XsltExecutionLimits.Default, p => probe = p)));

        sw.Stop();
        Assert.Equal(XsltFailureKind.LimitExceeded, ex.Diagnostic.Kind);
        Assert.Equal(LimitCodes.Depth, ex.Diagnostic.ErrorCode);
        // xsl:catch hiç çalışmadı: catch dalının <caught/> çıktısı yok ve enter sayısı ≈ derinlik
        // (catch özyinelemesi olsaydı katlanarak büyürdü).
        Assert.DoesNotContain("caught", output.ToString());
        Assert.True(probe!.TraceEnterCount < Engine.Options.MaxTraceDepth * 4L,
            $"enter sayısı {probe.TraceEnterCount}: catch dalı özyinelemeyi sürdürmüş olabilir");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"süre {sw.Elapsed}");
    }

    [Fact]
    public void SaxonLimitError_IsJavaError_NotXPathException()
    {
        var error = new SaxonLimitError(XsltFailureKind.LimitExceeded, LimitCodes.Depth, "x");
        Assert.IsAssignableFrom<java.lang.Error>(error);
        Assert.False(typeof(net.sf.saxon.trans.XPathException).IsAssignableFrom(typeof(SaxonLimitError)));
        Assert.False(typeof(java.lang.Exception).IsAssignableFrom(typeof(SaxonLimitError)));
    }

    [Fact]
    public void Deadline_StopsTemplateLoop()
    {
        // Derinlik sabit (enter/leave dengeli) ama sonsuza yakın döngü: yalnız deadline durdurur.
        var xslt = Head + """
            <xsl:template match="/"><out><xsl:for-each select="1 to 2000000000"><xsl:call-template name="t"/></xsl:for-each></out></xsl:template>
            <xsl:template name="t"><xsl:if test="false()"><x/></xsl:if></xsl:template>
            </xsl:stylesheet>
            """;
        var limits = XsltExecutionLimits.Default with { Deadline = TimeSpan.FromMilliseconds(300) };
        var sw = Stopwatch.StartNew();

        var d = Fail(xslt, limits: limits);

        Assert.Equal(XsltFailureKind.Timeout, d.Kind);
        Assert.Equal(LimitCodes.Deadline, d.ErrorCode);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), $"süre {sw.Elapsed}");
    }

    [Fact]
    public void Cancellation_StopsTemplateLoop()
    {
        var xslt = Head + """
            <xsl:template match="/"><out><xsl:for-each select="1 to 2000000000"><xsl:call-template name="t"/></xsl:for-each></out></xsl:template>
            <xsl:template name="t"><xsl:if test="false()"><x/></xsl:if></xsl:template>
            </xsl:stylesheet>
            """;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var limits = XsltExecutionLimits.Default with { Deadline = Timeout.InfiniteTimeSpan, Cancellation = cts.Token };

        var d = Fail(xslt, limits: limits);

        Assert.Equal(XsltFailureKind.Timeout, d.Kind);
        Assert.Equal(LimitCodes.Cancelled, d.ErrorCode);
    }

    [Fact]
    public void ListenerIsPerTransform_NoThreadStatic()
    {
        // Statik/ThreadStatic durum yok.
        var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        Assert.Empty(typeof(DepthLimitingTraceListener).GetFields(flags));
        Assert.Empty(typeof(ExecutionBudget).GetFields(flags));

        // Aynı thread'de: limiti aşan dönüşümden sonra aynı derlenmiş stylesheet temiz başlar.
        var xslt = Head + """
            <xsl:template match="/"><out><xsl:call-template name="r"><xsl:with-param name="n" select="number(/r/@n)"/></xsl:call-template></out></xsl:template>
            <xsl:template name="r"><xsl:param name="n"/><xsl:if test="$n gt 0"><x><xsl:call-template name="r"><xsl:with-param name="n" select="$n - 1"/></xsl:call-template></x></xsl:if></xsl:template>
            </xsl:stylesheet>
            """;
        var compiled = Engine.Compile(xslt, XsltExecutionLimits.Default);
        var tooDeep = Engine.Options.MaxTraceDepth * 2;

        OnBigStack(() =>
        {
            var ex = Assert.Throws<XsltProcessingException>(() => compiled.Transform($"<r n=\"{tooDeep}\"/>", new StringWriter(), XsltExecutionLimits.Default));
            Assert.Equal(XsltFailureKind.LimitExceeded, ex.Diagnostic.Kind);

            var ok = new StringWriter();
            compiled.Transform("<r n=\"5\"/>", ok, XsltExecutionLimits.Default);
            Assert.Equal("<out><x><x><x><x><x/></x></x></x></x></out>", ok.ToString());
            return 0;
        });

        // Paralel: biri limiti aşarken diğerleri etkilenmez.
        var results = Enumerable.Range(0, 8).AsParallel().Select(i => OnBigStack(() =>
        {
            try
            {
                var sw = new StringWriter();
                compiled.Transform(i % 2 == 0 ? $"<r n=\"{tooDeep}\"/>" : "<r n=\"3\"/>", sw, XsltExecutionLimits.Default);
                return sw.ToString();
            }
            catch (XsltProcessingException ex)
            {
                return ex.Diagnostic.Kind.ToString();
            }
        })).ToList();

        for (var i = 0; i < results.Count; i++)
            Assert.Equal(i % 2 == 0 ? nameof(XsltFailureKind.LimitExceeded) : "<out><x><x><x/></x></x></out>", results[i]);
    }
}
