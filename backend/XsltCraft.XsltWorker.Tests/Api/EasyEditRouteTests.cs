using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using XsltCraft.Api.Controllers;
using XsltCraft.Application.DTO;
using XsltCraft.Application.EasyEdit;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Membership;
using XsltCraft.Application.ThemeSlots;
using XsltCraft.Application.ThemeSlots.Analysis;
using XsltCraft.Application.ThemeSlots.Catalog;
using XsltCraft.Application.ThemeSlots.Compose;
using XsltCraft.Application.ThemeSlots.Overrides;
using XsltCraft.Application.Xslt.Engines;
using XsltCraft.Application.Xslt.Execution;
using XsltCraft.Domain.Entities;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>
/// <c>POST /api/easy-edit/analyze</c>: yüklenen XSLT kullanıcı kodudur → tüm yürütmeler <see cref="XsltRoute.Raw"/>.
/// Plan (402), yapısal sınır (400) ve kapı (429) yürütmeden önce denetlenir.
/// </summary>
public sealed class EasyEditRouteTests
{
    private const string Png =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static readonly Guid Me = Guid.NewGuid();

    /// <summary>XsltCraft.Application.Tests ThemeFixtures.CompanyImagesTheme kopyası (o proje buraya görünmüyor).</summary>
    private static readonly string Theme = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
            xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
            xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
            exclude-result-prefixes="n1 cac cbc">
          <xsl:output method="html" indent="no" encoding="UTF-8"/>
          <xsl:variable name="XML" select="/"/>
          <xsl:template match="/">
            <html>
              <head><title>e-Belge</title></head>
              <body style="width:800px;">
                <xsl:for-each select="$XML">
                  <!-- Başlık: satıcı - GİB logosu - karekod + firma logosu -->
                  <table style="width:100%;"><tbody><tr>
                    <td style="width:40%;">
                      <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                        <xsl:value-of select="cac:PartyName/cbc:Name"/><br/>
                        <xsl:text>VKN: </xsl:text><xsl:value-of select="cac:PartyIdentification/cbc:ID"/>
                      </xsl:for-each>
                    </td>
                    <td style="width:20%; text-align:center;">
                      <img style="width:91px;" alt="E-Fatura Logo" src="__PNG__"/>
                      <h1>e-FATURA</h1>
                    </td>
                    <td style="width:40%; text-align:center;">
                      <div id="qrcode" style="float:right;"/>
                      <img style="float:right; width: 175px; height: 75px" src="__PNG__" alt="company_logo"/>
                      <br/>
                      <img id="code128" style="width: 200px; height: 70px; float:right;"/>
                    </td>
                  </tr></tbody></table>
                  <!-- Alıcı - kaşe - belge bilgileri -->
                  <table><tbody><tr>
                    <td style="width:30%;">
                      <table id="customerPartyTable"><tbody><tr><td>
                        <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                          <xsl:value-of select="cac:PartyName/cbc:Name"/>
                        </xsl:for-each>
                      </td></tr></tbody></table>
                    </td>
                    <td style="width:30%; text-align:center;">
                      <img src="__PNG__" alt="company_logo" width="200" height="150"/>
                    </td>
                    <td style="width:30%;">
                      <table><tbody>
                        <tr><td>Özelleştirme No:</td><td><xsl:value-of select="n1:Invoice/cbc:CustomizationID"/></td></tr>
                        <tr><td>Senaryo:</td><td><xsl:value-of select="n1:Invoice/cbc:ProfileID"/></td></tr>
                        <tr><td>Fatura Tarihi:</td><td><xsl:value-of select="n1:Invoice/cbc:IssueDate"/></td></tr>
                      </tbody></table>
                    </td>
                  </tr></tbody></table>
                  <table><tr><td>ETTN: <xsl:value-of select="n1:Invoice/cbc:UUID"/></td></tr></table>
                  <table id="lineTable"><tbody>
                    <xsl:for-each select="n1:Invoice/cac:InvoiceLine"><tr><td><xsl:value-of select="cbc:ID"/></td></tr></xsl:for-each>
                  </tbody></table>
                </xsl:for-each>
                <table id="budgetContainerTable"><tbody><tr><td>
                  <table><tbody><tr>
                    <td>Ödenecek Tutar</td>
                    <td><xsl:value-of select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount"/></td>
                  </tr></tbody></table>
                </td></tr></tbody></table>
                <table id="notesTable"><tbody><tr><td>
                  <xsl:for-each select="//n1:Invoice/cbc:Note"><b>Not: </b><xsl:value-of select="."/><br/></xsl:for-each>
                </td></tr></tbody></table>
                <div id="qrvalue" style="display:none"><xsl:value-of select="//n1:Invoice/cbc:UUID"/></div>
              </body>
            </html>
          </xsl:template>
        </xsl:stylesheet>
        """.Replace("__PNG__", Png);

    public static TheoryData<UserRole, MembershipPlan, int?> NotPro => new()
    {
        { UserRole.User, MembershipPlan.Free, null },
        { UserRole.User, MembershipPlan.Pro, -1 }, // süresi geçmiş Pro → EffectivePlan=Free
    };

    [Theory]
    [MemberData(nameof(NotPro))]
    public async Task Analyze_NotEffectivelyPro_Returns402WithoutExecution(UserRole role, MembershipPlan plan, int? expiresInDays)
    {
        var execution = new RecordingExecution();
        var controller = Controller(execution, Entitlements(role, plan, expiresInDays));

        var result = Assert.IsType<ObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = Theme }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status402PaymentRequired, result.StatusCode);
        Assert.Equal("upgrade_required", Prop(result.Value, "error"));
        Assert.Equal(true, Prop(result.Value, "upgrade"));
        Assert.Empty(execution.Requests);
    }

    [Theory]
    [InlineData(UserRole.User, MembershipPlan.Pro)]
    [InlineData(UserRole.Editor, MembershipPlan.Free)]
    [InlineData(UserRole.Admin, MembershipPlan.Free)]
    public async Task Analyze_ProEditorAdmin_Returns200_AllExecutionsOnRawRoute(UserRole role, MembershipPlan plan)
    {
        var execution = new RecordingExecution();
        var controller = Controller(execution, Entitlements(role, plan, null));

        var ok = Assert.IsType<OkObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = Theme }, CancellationToken.None));

        var response = Assert.IsType<EasyEditAnalyzeResponse>(ok.Value);
        Assert.Equal("suitable", response.Verdict);
        Assert.NotEmpty(execution.Requests);
        Assert.All(execution.Requests, r => Assert.Equal(XsltRoute.Raw, r.Route));
    }

    public static TheoryData<string> OverLimitXslt => new()
    {
        string.Concat(Enumerable.Repeat("<b>", ThemeComposeXsltLimits.MaxDepth + 1)) + string.Concat(Enumerable.Repeat("</b>", ThemeComposeXsltLimits.MaxDepth + 1)),
        "<r>" + string.Concat(Enumerable.Repeat("<a/>", ThemeComposeXsltLimits.MaxElements)) + "</r>",
    };

    [Theory]
    [MemberData(nameof(OverLimitXslt))]
    public async Task Analyze_OverLimitXslt_Returns400WithoutExecution(string overLimit)
    {
        var execution = new RecordingExecution();
        var controller = Controller(execution, Entitlements(UserRole.User, MembershipPlan.Pro, null));

        var bad = Assert.IsType<BadRequestObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = overLimit }, CancellationToken.None));

        Assert.Equal("xslt", Prop(bad.Value, "field"));
        Assert.Empty(execution.Requests);
    }

    [Fact]
    public async Task Analyze_ThreatInFile_Returns200Rejected_WithoutExecution()
    {
        var execution = new RecordingExecution();
        var controller = Controller(execution, Entitlements(UserRole.User, MembershipPlan.Pro, null));
        var threat = Theme.Replace("<xsl:template match=\"/\">",
            "<msxsl:script xmlns:msxsl=\"urn:schemas-microsoft-com:xslt\" language=\"C#\">x</msxsl:script><xsl:template match=\"/\">");

        var ok = Assert.IsType<OkObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = threat }, CancellationToken.None));

        var response = Assert.IsType<EasyEditAnalyzeResponse>(ok.Value);
        Assert.Equal("rejected", response.Verdict);
        Assert.Null(response.Xslt);
        Assert.Empty(execution.Requests);
    }

    [Fact]
    public async Task Analyze_GateFull_Returns429WithRetryAfter_WithoutExecution()
    {
        var execution = new RecordingExecution();
        var controller = Controller(execution, Entitlements(UserRole.User, MembershipPlan.Pro, null), new FullGate());

        var busy = Assert.IsType<ObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = Theme }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status429TooManyRequests, busy.StatusCode);
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers.RetryAfter));
        Assert.Empty(execution.Requests);
    }

    [Fact]
    public async Task Analyze_UserBusyExecution_Returns429WithRetryAfter()
    {
        var controller = Controller(new UserBusyExecution(), Entitlements(UserRole.User, MembershipPlan.Pro, null));

        var busy = Assert.IsType<ObjectResult>(await controller.Analyze(new EasyEditAnalyzeRequest { Xslt = Theme }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status429TooManyRequests, busy.StatusCode);
        Assert.False(string.IsNullOrEmpty(controller.Response.Headers.RetryAfter));
    }

    private static object? Prop(object? value, string name) => value!.GetType().GetProperty(name)!.GetValue(value);

    private static UserEntitlements Entitlements(UserRole role, MembershipPlan plan, int? expiresInDays) =>
        EntitlementPolicy.Compute(role, plan, expiresInDays is { } d ? DateTime.UtcNow.AddDays(d) : null, new MembershipOptions(), DateTime.UtcNow);

    private static EasyEditController Controller(IXsltExecutionService execution, UserEntitlements entitlements, IEasyEditAnalyzeGate? gate = null)
    {
        var validator = new ThemeImageValidator();
        var analyzer = new UploadedXsltAnalyzer(new DefaultXsltSafetyScanner(), new ThemeOverridesProcessor(validator),
            new ThemeSlotAnalyzer(execution, new ThemeSampleXmlProvider()));
        return new EasyEditController(analyzer, new ThemeSlotProjector(), new FixedEntitlements(entitlements),
            gate ?? new EasyEditAnalyzeGate(2), NullLogger<EasyEditController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Me.ToString())], "test")),
                },
            },
        };
    }

    /// <summary>
    /// Rotayı kaydeden sahte yürütme: .NET seçilen temalar gerçek <see cref="DotNetXsltEngine"/>'le eşzamanlı çalışır
    /// (XsltCraft.Application.Tests DotNetThemeExecution kopyası); Saxon seçilirse EngineUnavailable.
    /// </summary>
    private sealed class RecordingExecution : IXsltExecutionService
    {
        private static readonly DotNetXsltEngine Engine = new();

        public List<XsltRunRequest> Requests { get; } = [];

        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            var engine = XsltEngineSelector.SelectByUsage(XsltEngineMode.Auto, XsltVersionSniffer.Sniff(request.Xslt), request.Xslt);
            if (engine != XsltEngineKind.DotNet)
                return Task.FromResult(Failed(new XsltDiagnostic("Saxon bu testte yok.", 0, 0,
                    XsltFailureKind.EngineUnavailable, engine, "WorkerDisabled"), engine));

            try
            {
                var compiled = Engine.Compile(request.Xslt, XsltExecutionLimits.Default);
                if (request.Op == XsltOperation.Validate)
                    return Task.FromResult(new XsltRunResult(null, engine, null, false, null));

                using var output = new StringWriter();
                var timings = compiled.Transform(request.Xml ?? "", output, XsltExecutionLimits.Default);
                return Task.FromResult(new XsltRunResult(output.ToString(), engine, timings, false, null));
            }
            catch (XsltProcessingException ex)
            {
                return Task.FromResult(Failed(ex.Diagnostic, engine));
            }
        }

        private static XsltRunResult Failed(XsltDiagnostic diagnostic, XsltEngineKind engine) => new(null, engine, null, false, diagnostic);
    }

    private sealed class UserBusyExecution : IXsltExecutionService
    {
        public Task<XsltRunResult> ExecuteAsync(XsltRunRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new XsltRunResult(null, XsltEngineKind.DotNet, null, false,
                new XsltDiagnostic("Kota dolu.", 0, 0, XsltFailureKind.UserBusy, XsltEngineKind.DotNet)));
    }

    private sealed class FullGate : IEasyEditAnalyzeGate
    {
        public IDisposable? TryEnter() => null;
    }

    private sealed class FixedEntitlements(UserEntitlements entitlements) : IEntitlementService
    {
        public Task<UserEntitlements> GetAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(entitlements);
    }
}
