using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using XsltCraft.Application.Preview;
using XsltCraft.Application.XPath;
using XsltCraft.Controllers;

namespace XsltCraft.XsltWorker.Tests.Api;

/// <summary>G4 K1: XPath konsolu — yetki, XXE ve maliyet kapısı.</summary>
public sealed class XPathEvaluateEndpointTests
{
    [Fact]
    public async Task Evaluate_Anonymous_401()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(XPathController).Assembly);
        builder.Services.AddSingleton<IXPathEvaluator>(_ => new XPathEvaluator());
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            o.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))),
                ValidateIssuer = false,
                ValidateAudience = false,
            });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        using var response = await client.PostAsync("/api/xpath/evaluate",
            new StringContent("""{"xpathExpression":"/r","xmlContent":"<r/>"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Evaluate_Doctype_Rejected()
    {
        const string xxe = """
            <?xml version="1.0"?>
            <!DOCTYPE r [<!ENTITY x SYSTEM "file:///c:/windows/win.ini"><!ENTITY y "SIZINTI">]>
            <r>&x;&y;</r>
            """;

        var ok = Assert.IsType<OkObjectResult>(await Controller().Evaluate(new XPathEvaluateRequest("string(/r)", xxe)));
        var body = Assert.IsType<XPathEvaluateResponse>(ok.Value);

        Assert.Equal("error", body.Kind);
        Assert.Equal(XPathEvaluator.XmlInvalid, body.Code);
        Assert.Empty(body.Items);
        Assert.StartsWith("XML okunamadı", body.Error);                  // sabit metin, ham XmlException mesajı değil
        Assert.DoesNotContain("security", body.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SIZINTI", body.Error);
    }

    [Fact]
    public async Task Evaluate_QuadraticPredicate_RejectedOrBounded()
    {
        var xml = "<r>" + string.Concat(Enumerable.Repeat("<i/>", 20_000)) + "</r>";

        var result = await Controller().Evaluate(new XPathEvaluateRequest("count(//i[count(//i) > 0])", xml));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var body = Assert.IsType<XPathEvaluateResponse>(bad.Value);
        Assert.Equal(XPathPortability.TooComplex, body.Code);
        Assert.Equal("error", body.Kind);
    }

    private static XPathController Controller()
    {
        var controller = new XPathController(new XPathEvaluator());
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }
}
