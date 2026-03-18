using System;
using System.Collections.Generic;
using System.Text;

namespace XsltCraft.Application.DTO
{
    public class TransformPreviewRequest
    {
        public string Xml { get; set; } = "";
        public string Xslt { get; set; } = "";
    }
}
