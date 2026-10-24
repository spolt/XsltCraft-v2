using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Infrastructure.Xslt.Worker;
using XsltCraft.XsltWorker.Contracts.Messages;
using XsltCraft.XsltWorker.Tests.Support;

namespace XsltCraft.XsltWorker.Tests.Engine;

/// <summary>Motor yolu test op adları (worker'daki <c>Hosting.TestOps</c> ile aynı).</summary>
internal static class EngineTestOps
{
    public const string RenderUnscreened = "render-unscreened";
    public const string JavaStdout = "java-stdout";
    public const string ThreadInfo = "thread-info";
}

/// <summary>Motor yolu testleri için stylesheet'ler, fikstürler ve istek kurucuları.</summary>
internal static class EngineTestKit
{
    public const string XslNs = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>Gövdeyi <c>match="/"</c> şablonuna saran stylesheet.</summary>
    public static string Stylesheet(string body, string top = "", string version = "2.0") => $"""
        <xsl:stylesheet version="{version}" xmlns:xsl="{XslNs}" xmlns:xs="http://www.w3.org/2001/XMLSchema" exclude-result-prefixes="xs">
          <xsl:output method="xml" omit-xml-declaration="yes"/>
          {top}
          <xsl:template match="/"><out>{body}</out></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>Sonsuz template özyinelemesi (XsltSafety bunu engellemez; listener/kill durdurur).</summary>
    public static string InfiniteRecursion => $"""
        <xsl:stylesheet version="2.0" xmlns:xsl="{XslNs}">
          <xsl:template match="/"><xsl:call-template name="loop"><xsl:with-param name="n" select="1"/></xsl:call-template></xsl:template>
          <xsl:template name="loop"><xsl:param name="n"/><x><xsl:call-template name="loop"><xsl:with-param name="n" select="$n + 1"/></xsl:call-template></x></xsl:template>
        </xsl:stylesheet>
        """;

    /// <summary>Repo kökündeki <c>backend/</c> (kaynak dosyanın yerinden; artifacts-path'ten bağımsız).</summary>
    public static string BackendDir([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    public static string GibGeneral =>
        File.ReadAllText(Path.Combine(BackendDir(), "XsltCraft.Application.Tests", "ThemeSlots", "Fixtures", "gib-general.xslt"));

    public static string InvoiceXml =>
        File.ReadAllText(Path.Combine(BackendDir(), "XsltCraft.Application", "ThemeSlots", "Samples", "invoice.xml"));

    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static RequestLimits Limits(int deadlineMs = 8_000) => new()
    {
        DeadlineMs = deadlineMs,
        MaxOutputChars = 8_000_000,
        MaxStylesheetDepth = 256,
        MaxInputDepth = 512,
        MaxInputNodes = 1_000_000,
    };

    public static XsltWorkerRequest Render(string engine, string xslt, string xml, string? sha256 = null, string op = WorkerOps.Render) =>
        new(op, engine, sha256 ?? Sha256(xslt), [xslt, xml]) { Limits = Limits() };

    public static XsltWorkerRequest Validate(string engine, string xslt, string? sha256 = null) =>
        new(WorkerOps.Validate, engine, sha256 ?? Sha256(xslt), [xslt]) { Limits = Limits() };

    /// <summary>Api'nin gerçek adaptörü (<see cref="WorkerXsltHost"/>) — uçtan uca yol.</summary>
    public static WorkerXsltHost Host(PoolHarness harness) =>
        new(new XsltWorkerPoolAdapter(harness.Pool), Options.Create(harness.Options));

    public static XsltHostRequest HostRender(XsltEngineKind engine, string xslt, string xml) =>
        new(XsltOperation.Render, engine, xslt, xml, XsltExecutionLimits.Default with { Deadline = TimeSpan.FromSeconds(10) });

    public static XsltHostRequest HostValidate(XsltEngineKind engine, string xslt) =>
        new(XsltOperation.Validate, engine, xslt, null, XsltExecutionLimits.Default with { Deadline = TimeSpan.FromSeconds(5) });
}
