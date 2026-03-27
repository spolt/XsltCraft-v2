using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XsltCraft.Application.Interfaces;

namespace XsltCraft.Api.Controllers
{
    [ApiController]
    [Route("api/document")]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentEngine _engine;

        public DocumentController(IDocumentEngine engine)
        {
            _engine = engine;
        }

        [HttpPost("{layout}")]
        public IActionResult Generate(string layout, [FromBody] string xml)
        {
            var result = _engine.Generate(layout, xml);

            return Content(result, "text/html");
        }
    }
}
