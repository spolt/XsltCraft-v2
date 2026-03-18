using System;
using System.Collections.Generic;
using System.Text;

namespace XsltCraft.Application.DTO
{
    public class TemplateDto
    {
        public string Id { get; set; }
        public List<string> Parameters { get; set; }
        public List<string> Templates { get; set; }
    }
}
