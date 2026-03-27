using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Xml;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Api.Controllers
{
    [ApiController]
    [Route("api/render")]
    [Authorize]
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

            var xmlReaderSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var doc = new XmlDocument { XmlResolver = null };
            using var xmlReader = XmlReader.Create(new StringReader(xml), xmlReaderSettings);
            doc.Load(xmlReader);

            var result = await _renderer.RenderAsync(templateId, doc);

            return Content(result, "text/html");
        }
    }
}