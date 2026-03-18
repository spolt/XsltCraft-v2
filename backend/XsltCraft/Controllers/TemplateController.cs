using Microsoft.AspNetCore.Mvc;
using System.Xml;
using XsltCraft.Api.DTO;
using XsltCraft.Application.DTO;
using XsltCraft.Application.Interfaces;
using XsltCraft.Application.Services;
using XsltCraft.Domain.Entities;

namespace XsltCraft.Api.Controllers;

[ApiController]
[Route("api/templates")]
public class TemplateController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ITemplateRepository _repo;
    private readonly ITemplateService _templateService;

    public TemplateController(IWebHostEnvironment env, ITemplateRepository repo)
    {
        _env = env;
        _repo = repo;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var templates = _repo.GetAll();

        return Ok(templates);
    }

    [HttpGet("{templateId}")]
    public IActionResult Get(string templateId)
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");
        var dir = Path.Combine(root, templateId);

        if (!Directory.Exists(dir))
            return NotFound();

        var files = Directory.GetFiles(dir, "*.xslt")
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();

        return Ok(new { id = templateId, files });
    }

    [HttpGet("{templateId}/{fileName}")]
    public IActionResult GetFile(string templateId, string fileName)
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");
        var path = Path.Combine(root, templateId, fileName);

        if (!System.IO.File.Exists(path))
            return NotFound();

        var content = System.IO.File.ReadAllText(path);

        return Ok(new { id = templateId, fileName, content });
    }


    [HttpPut("{templateId}")]
    public IActionResult Update(string templateId, [FromBody] UpdateTemplateDto dto)
    {
        _templateService.Update(templateId, dto.Content);

        return Ok();
    }

    [HttpPost("{templateId}")]
    public async Task<IActionResult> UploadTemplate(
        string templateId,
        IFormFile file)
    {
        var root = Path.Combine(_env.ContentRootPath, "templates");

        var dir = Path.Combine(root, templateId);

        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "template.xslt");

        using var stream = new FileStream(path, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok();
    }
}