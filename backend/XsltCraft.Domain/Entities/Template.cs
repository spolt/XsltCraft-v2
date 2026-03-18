using System;
using System.Collections.Generic;
using System.Text;

namespace XsltCraft.Domain.Entities;

public class Template
{
    public string Id { get; set; }

    public string Name { get; set; }

    public string XsltContent { get; set; }
}