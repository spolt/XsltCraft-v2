using System.Globalization;

using XsltCraft.Application.Xslt.Engines;

namespace XsltCraft.Tests.Xslt;

public sealed class XsltEngineSelectorTests
{
    [Theory]
    [InlineData(null, XsltEngineKind.DotNet)]
    [InlineData("0.5", XsltEngineKind.DotNet)]
    [InlineData("1.0", XsltEngineKind.DotNet)]
    [InlineData("1.99", XsltEngineKind.DotNet)]
    [InlineData("2.0", XsltEngineKind.Saxon)]
    [InlineData("2.5", XsltEngineKind.Saxon)]
    [InlineData("3.0", XsltEngineKind.Saxon)]
    public void Selector_AutoMatrix(string? version, XsltEngineKind expected) =>
        Assert.Equal(expected, XsltEngineSelector.Select(XsltEngineMode.Auto, Parse(version)));

    [Theory]
    [InlineData(null)]
    [InlineData("1.0")]
    [InlineData("2.0")]
    [InlineData("3.0")]
    public void Selector_DotNetMode_AlwaysDotNet(string? version) =>
        Assert.Equal(XsltEngineKind.DotNet, XsltEngineSelector.Select(XsltEngineMode.DotNet, Parse(version)));

    [Theory]
    [InlineData(null)]
    [InlineData("1.0")]
    [InlineData("2.0")]
    [InlineData("3.0")]
    public void Selector_SaxonMode_AlwaysSaxon(string? version) =>
        Assert.Equal(XsltEngineKind.Saxon, XsltEngineSelector.Select(XsltEngineMode.Saxon, Parse(version)));

    [Fact]
    public void Selector_UnknownMode_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => XsltEngineSelector.Select((XsltEngineMode)99, 1.0m));

    [Fact]
    public void Selector_WithSniffer_EndToEnd()
    {
        const string v2 = """<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"/>""";

        Assert.Equal(XsltEngineKind.Saxon, XsltEngineSelector.Select(XsltEngineMode.Auto, XsltVersionSniffer.Sniff(v2)));
        Assert.Equal(XsltEngineKind.DotNet, XsltEngineSelector.Select(XsltEngineMode.Auto, XsltVersionSniffer.Sniff("çöp")));
    }

    private static decimal? Parse(string? version) =>
        version is null ? null : decimal.Parse(version, CultureInfo.InvariantCulture);
}
