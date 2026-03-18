using System.Xml;
using System.Xml.Xsl;
using Microsoft.AspNetCore.Mvc;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Services;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PreviewController : ControllerBase
{
    private readonly RenderService _renderService;

    public PreviewController(RenderService renderService)
    {
        _renderService = renderService;
    }

    [HttpPost]
    public async Task<IActionResult> Transform([FromBody] TransformPreviewRequest request)
    {
        try
        {
            var html = await _renderService.RenderPreviewAsync(request.Xml, request.Xslt);
            return Content(html, "text/html");
        }
        catch (XsltException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                line = ex.LineNumber,
                column = ex.LinePosition
            });
        }
        catch (XmlException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                line = ex.LineNumber,
                column = ex.LinePosition
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                line = 0,
                column = 0
            });
        }
    }

    [HttpPost("validate-xslt")]
    public IActionResult ValidateXslt([FromBody] ValidateXsltRequest request)
    {
        try
        {
            var transform = new XslCompiledTransform();
            using var reader = XmlReader.Create(new StringReader(request.Xslt));
            transform.Load(reader);

            return Ok(new { valid = true });
        }
        catch (XsltException ex)
        {
            return Ok(new
            {
                valid = false,
                error = ex.Message,
                line = ex.LineNumber,
                column = ex.LinePosition
            });
        }
        catch (XmlException ex)
        {
            return Ok(new
            {
                valid = false,
                error = ex.Message,
                line = ex.LineNumber,
                column = ex.LinePosition
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                valid = false,
                error = ex.Message,
                line = 0,
                column = 0
            });
        }
    }
}

public class ValidateXsltRequest
{
    public string Xslt { get; set; } = "";
}
