using Microsoft.AspNetCore.Mvc;
using System.Xml;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Api.Controllers
{
    [ApiController]
    [Route("api/render")]
    public class RenderController : ControllerBase
    {
        private readonly ITemplateRepository _repo;
        private readonly IXsltTemplateRenderer _renderer;

        public RenderController(
            ITemplateRepository repo,
            IXsltTemplateRenderer renderer)
        {
            _repo = repo;
            _renderer = renderer;
        }

        [HttpPost("{templateId}")]
        public async Task<IActionResult> Render(string templateId)
        {
            var xml = await new StreamReader(Request.Body).ReadToEndAsync();

            var doc = new XmlDocument();
            doc.LoadXml(xml);

            var result = await _renderer.RenderAsync(templateId, doc);

            return Content(result, "text/html");
        }
    }
}